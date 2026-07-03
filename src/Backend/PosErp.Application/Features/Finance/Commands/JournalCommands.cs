using MediatR;
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using PosErp.Application.Features.Finance.Services;
using PosErp.Domain.Entities.Finance;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Finance.Commands;

public record CreateJournalCommand(
    Guid? StoreId,
    DateTime EntryDate,
    string Description,
    string ReferenceDocument,
    List<JournalLineInputDto> Lines,
    string? SourceModule,
    string? SourceDocumentType,
    Guid? SourceDocumentId,
    Guid? UserId
) : IRequest<Guid>;

public record JournalLineInputDto
{
    public string AccountCode { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public Guid? CostCenterId { get; set; }
}

public record PostJournalEntryCommand(Guid Id, Guid UserId) : IRequest<string>;

public record ApproveJournalStepCommand(Guid ApprovalRequestId, Guid ActionedBy, string? Comments) : IRequest<bool>;

public record VoidJournalEntryCommand(Guid Id) : IRequest<bool>;

public record ReverseJournalEntryCommand(Guid Id, Guid UserId) : IRequest<Guid>;

public class JournalCommandsHandler :
    IRequestHandler<CreateJournalCommand, Guid>,
    IRequestHandler<PostJournalEntryCommand, string>,
    IRequestHandler<ApproveJournalStepCommand, bool>,
    IRequestHandler<VoidJournalEntryCommand, bool>,
    IRequestHandler<ReverseJournalEntryCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly IFinancialPostingService _postingService;
    private readonly IPeriodLockService _periodLockService;
    private readonly IApprovalWorkflowService _approvalService;

    public JournalCommandsHandler(
        IApplicationDbContext context,
        IFinancialPostingService postingService,
        IPeriodLockService periodLockService,
        IApprovalWorkflowService approvalService)
    {
        _context = context;
        _postingService = postingService;
        _periodLockService = periodLockService;
        _approvalService = approvalService;
    }

    public async Task<Guid> Handle(CreateJournalCommand request, CancellationToken cancellationToken)
    {
        var dtos = request.Lines.Select(l => new JournalLineDto
        {
            AccountCode = l.AccountCode,
            Description = l.Description,
            Debit = l.Debit,
            Credit = l.Credit,
            CostCenterId = l.CostCenterId
        }).ToList();

        // Pass isDraft = true to save as draft journal
        return await _postingService.PostJournalEntryWithUserAsync(
            request.StoreId,
            request.EntryDate,
            request.Description,
            request.ReferenceDocument,
            dtos,
            request.UserId,
            isDraft: true,
            cancellationToken,
            request.SourceModule,
            request.SourceDocumentType,
            request.SourceDocumentId
        );
    }

    public async Task<string> Handle(PostJournalEntryCommand request, CancellationToken cancellationToken)
    {
        var strategy = ((DbContext)_context).Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await ((DbContext)_context).Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var entry = await _context.JournalEntries
                    .Include(e => e.Lines)
                    .ThenInclude(l => l.Account)
                    .FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken);

                if (entry == null) throw new InvalidOperationException("Journal entry not found.");
                if (entry.Status == "POSTED") throw new InvalidOperationException("Journal entry is already posted.");
                if (entry.Status == "VOID") throw new InvalidOperationException("Cannot post a void journal entry.");

                // 1. Enforce active validations and lock period
                Guid activeStoreId = entry.StoreId ?? Guid.Parse("00000000-0000-0000-0000-000000000000");
                
                // Store validation
                var store = await _context.Stores.FirstOrDefaultAsync(s => s.Id == activeStoreId, cancellationToken);
                if (store == null || !store.IsActive || store.IsDeleted)
                    throw new InvalidOperationException("Store is inactive or deleted.");

                // Date validation
                await _periodLockService.CheckPeriodLockAsync(activeStoreId, entry.EntryDate, cancellationToken);

                // Double entry sum
                decimal totalDebit = entry.Lines.Sum(l => l.DebitAmount);
                decimal totalCredit = entry.Lines.Sum(l => l.CreditAmount);

                if (totalDebit != totalCredit)
                    throw new InvalidOperationException($"Journal entry {entry.EntryNumber} is unbalanced.");

                // Validate lines
                foreach (var line in entry.Lines)
                {
                    if (!line.Account.IsActive)
                        throw new InvalidOperationException($"Account '{line.Account.Name}' is inactive.");

                    if (line.CostCenterId.HasValue)
                    {
                        var cc = await _context.CostCenters.FirstOrDefaultAsync(c => c.Id == line.CostCenterId.Value, cancellationToken);
                        if (cc == null || !cc.IsActive)
                            throw new InvalidOperationException("Cost center is inactive.");
                    }
                }

                // 2. Check workflow approvals
                bool requiresApproval = await _approvalService.RequiresApprovalAsync(activeStoreId, "JOURNAL_ADJUSTMENT", totalDebit, cancellationToken);
                if (requiresApproval)
                {
                    // Maintain status as DRAFT, submit approval request
                    await _approvalService.SubmitForApprovalAsync(activeStoreId, "JOURNAL_ADJUSTMENT", entry.Id, totalDebit, request.UserId, cancellationToken);
                    await _context.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                    return "APPROVAL_REQUIRED";
                }

                // 3. Post Directly
                entry.Status = "POSTED";
                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return "POSTED";
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    public async Task<bool> Handle(ApproveJournalStepCommand request, CancellationToken cancellationToken)
    {
        var strategy = ((DbContext)_context).Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await ((DbContext)_context).Database.BeginTransactionAsync(cancellationToken);
            try
            {
                bool fullyApproved = await _approvalService.ApproveStepAsync(request.ApprovalRequestId, request.ActionedBy, request.Comments, cancellationToken);
                
                if (fullyApproved)
                {
                    var appRequest = await _context.ApprovalRequests.FirstOrDefaultAsync(r => r.Id == request.ApprovalRequestId, cancellationToken);
                    if (appRequest != null && appRequest.RequestType == "JOURNAL_ADJUSTMENT")
                    {
                        var entry = await _context.JournalEntries.FirstOrDefaultAsync(e => e.Id == appRequest.TargetId, cancellationToken);
                        if (entry != null && entry.Status == "DRAFT")
                        {
                            entry.Status = "POSTED";
                            await _context.SaveChangesAsync(cancellationToken);
                        }
                    }
                }

                await transaction.CommitAsync(cancellationToken);
                return fullyApproved;
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    public async Task<bool> Handle(VoidJournalEntryCommand request, CancellationToken cancellationToken)
    {
        var entry = await _context.JournalEntries.FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken);
        if (entry == null) throw new InvalidOperationException("Journal entry not found.");
        
        if (entry.Status == "POSTED")
            throw new InvalidOperationException("Posted journal entries are immutable and cannot be cancelled or deleted. Use the Reversal workflow instead.");

        entry.Status = "VOID";
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<Guid> Handle(ReverseJournalEntryCommand request, CancellationToken cancellationToken)
    {
        var strategy = ((DbContext)_context).Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await ((DbContext)_context).Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var original = await _context.JournalEntries
                    .Include(e => e.Lines)
                    .ThenInclude(l => l.Account)
                    .FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken);

                if (original == null) throw new InvalidOperationException("Original journal entry not found.");
                if (original.Status != "POSTED") throw new InvalidOperationException("Only posted journals can be reversed.");

                // Verify period lock for original entry date (reversals are posted at the current date or original entry date? 
                // In retail ERPs, reversals are typically posted as of the current business date, which must be validated.)
                DateTime currentDate = DateTime.UtcNow.Date;
                await _periodLockService.CheckPeriodLockAsync(original.StoreId ?? Guid.Parse("00000000-0000-0000-0000-000000000000"), currentDate, cancellationToken);

                // Construct reversed lines (swap debits and credits)
                var reversedLines = original.Lines.Select(l => new JournalLineDto
                {
                    AccountCode = l.Account.AccountCode,
                    Description = $"Reversal of {original.EntryNumber}: {l.Description}",
                    Debit = l.CreditAmount,  // Swap Credit to Debit
                    Credit = l.DebitAmount,  // Swap Debit to Credit
                    CostCenterId = l.CostCenterId
                }).ToList();

                // Post reversal entry
                Guid reversalId = await _postingService.PostJournalEntryWithUserAsync(
                    original.StoreId,
                    currentDate,
                    $"Reversal journal for entry {original.EntryNumber}",
                    original.EntryNumber,
                    reversedLines,
                    request.UserId,
                    isDraft: false, // Reversals are directly posted
                    cancellationToken,
                    sourceModule: "FINANCE",
                    sourceDocType: "REVERSAL",
                    sourceDocId: original.Id
                );

                // Mark original entry status as VOID/CANCELLED or reference it. The prompt says "Reversals must create new journals."
                // We can append details to the description.
                original.Description += $" (Reversed by reversal entry)";
                await _context.SaveChangesAsync(cancellationToken);

                await transaction.CommitAsync(cancellationToken);
                return reversalId;
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }
}
