using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using PosErp.Domain.Entities.Finance;
using PosErp.Domain.Entities.Auth;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Finance.Services;

public interface IFinancialPostingService
{
    Task<Guid> PostJournalEntryAsync(
        Guid? storeId, 
        DateTime date, 
        string description, 
        string refDoc, 
        List<JournalLineDto> lines, 
        CancellationToken cancellationToken,
        string? sourceModule = null,
        string? sourceDocType = null,
        Guid? sourceDocId = null);

    Task<Guid> PostJournalEntryWithUserAsync(
        Guid? storeId, 
        DateTime date, 
        string description, 
        string refDoc, 
        List<JournalLineDto> lines, 
        Guid? userId, 
        bool isDraft, 
        CancellationToken cancellationToken,
        string? sourceModule = null,
        string? sourceDocType = null,
        Guid? sourceDocId = null);

    Task RecordGstTransactionAsync(Guid? storeId, string type, string docNumber, DateTime date, decimal taxable, decimal cgst, decimal sgst, decimal cess, string? gstin, CancellationToken cancellationToken);
}

public class JournalLineDto
{
    public string AccountCode { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public Guid? CostCenterId { get; set; }
}

public class FinancialPostingService : IFinancialPostingService
{
    private readonly IApplicationDbContext _context;
    private readonly IPeriodLockService _periodLockService;
    private readonly IDocumentSequenceService _sequenceService;
    private readonly IApprovalWorkflowService _approvalService;

    public FinancialPostingService(
        IApplicationDbContext context,
        IPeriodLockService periodLockService,
        IDocumentSequenceService sequenceService,
        IApprovalWorkflowService approvalService)
    {
        _context = context;
        _periodLockService = periodLockService;
        _sequenceService = sequenceService;
        _approvalService = approvalService;
    }

    public async Task<Guid> PostJournalEntryAsync(
        Guid? storeId, 
        DateTime date, 
        string description, 
        string refDoc, 
        List<JournalLineDto> lines, 
        CancellationToken cancellationToken,
        string? sourceModule = null,
        string? sourceDocType = null,
        Guid? sourceDocId = null)
    {
        return await PostJournalEntryWithUserAsync(storeId, date, description, refDoc, lines, null, false, cancellationToken, sourceModule, sourceDocType, sourceDocId);
    }

    public async Task<Guid> PostJournalEntryWithUserAsync(
        Guid? storeId, 
        DateTime date, 
        string description, 
        string refDoc, 
        List<JournalLineDto> lines, 
        Guid? userId, 
        bool isDraft, 
        CancellationToken cancellationToken,
        string? sourceModule = null,
        string? sourceDocType = null,
        Guid? sourceDocId = null)
    {
        Guid activeStoreId = storeId ?? Guid.Parse("00000000-0000-0000-0000-000000000000");

        // 1. Active Store Validation
        var store = await _context.Stores.FirstOrDefaultAsync(s => s.Id == activeStoreId, cancellationToken);
        if (store == null) 
            throw new InvalidOperationException($"Store with ID {activeStoreId} not found.");
        if (!store.IsActive || store.IsDeleted) 
            throw new InvalidOperationException($"Store '{store.StoreName}' is inactive or deleted.");

        // 2. Financial Period Lock Validation
        await _periodLockService.CheckPeriodLockAsync(activeStoreId, date, cancellationToken);

        // 3. Debit/Credit Balance Validation
        decimal totalDebit = lines.Sum(l => l.Debit);
        decimal totalCredit = lines.Sum(l => l.Credit);

        if (totalDebit != totalCredit)
            throw new InvalidOperationException($"Journal is unbalanced. Total Debit: {totalDebit}, Total Credit: {totalCredit}");

        if (totalDebit <= 0)
            throw new InvalidOperationException("Journal entry amount must be greater than zero.");

        // 4. Line-level Validation (Account, Cost Center, normal balance rules)
        foreach (var dto in lines)
        {
            if (dto.Debit < 0 || dto.Credit < 0)
                throw new InvalidOperationException("Debit and Credit amounts cannot be negative.");

            if (dto.Debit > 0 && dto.Credit > 0)
                throw new InvalidOperationException("A single line cannot have both a Debit and Credit amount.");

            // Active Account Validation
            var account = await _context.Accounts.FirstOrDefaultAsync(a => a.AccountCode == dto.AccountCode, cancellationToken);
            if (account == null) 
                throw new InvalidOperationException($"Account code {dto.AccountCode} not found in Chart of Accounts.");
            if (!account.IsActive) 
                throw new InvalidOperationException($"Account '{account.Name}' is inactive.");

            // Enforce Account Type validation
            var allowedTypes = new[] { "ASSET", "LIABILITY", "EQUITY", "REVENUE", "EXPENSE" };
            if (!allowedTypes.Contains(account.AccountType.ToUpper()))
                throw new InvalidOperationException($"Account '{account.Name}' has an invalid account type: '{account.AccountType}'.");

            // Active Cost Center Validation
            if (dto.CostCenterId.HasValue)
            {
                var cc = await _context.CostCenters.FirstOrDefaultAsync(c => c.Id == dto.CostCenterId.Value, cancellationToken);
                if (cc == null) 
                    throw new InvalidOperationException($"Cost Center with ID {dto.CostCenterId.Value} not found.");
                if (!cc.IsActive) 
                    throw new InvalidOperationException($"Cost Center '{cc.Name}' is inactive.");
            }
        }

        // 5. Workflow Approval Check (only if user tries to post directly)
        bool requiresApproval = false;
        if (!isDraft)
        {
            requiresApproval = await _approvalService.RequiresApprovalAsync(activeStoreId, "JOURNAL_ADJUSTMENT", totalDebit, cancellationToken);
        }

        // 6. Generate Sequence Number atomically
        string entryNumber = await _sequenceService.GenerateNextNumberAsync(activeStoreId, "JOURNAL_ENTRY", cancellationToken);

        var entry = new JournalEntry
        {
            StoreId = activeStoreId,
            EntryNumber = entryNumber,
            EntryDate = date.Date,
            Description = description,
            ReferenceDocument = refDoc,
            Status = (isDraft || requiresApproval) ? "DRAFT" : "POSTED",
            SourceModule = sourceModule,
            SourceDocumentType = sourceDocType,
            SourceDocumentId = sourceDocId,
            CreatedBy = userId
        };

        foreach (var dto in lines)
        {
            var account = await _context.Accounts.FirstAsync(a => a.AccountCode == dto.AccountCode, cancellationToken);
            entry.Lines.Add(new JournalEntryLine
            {
                StoreId = activeStoreId,
                AccountId = account.Id,
                Description = dto.Description,
                DebitAmount = dto.Debit,
                CreditAmount = dto.Credit,
                CostCenterId = dto.CostCenterId
            });
        }

        _context.JournalEntries.Add(entry);
        await _context.SaveChangesAsync(cancellationToken);

        // 7. Trigger sequential approvals if threshold is met
        if (requiresApproval && userId.HasValue)
        {
            await _approvalService.SubmitForApprovalAsync(activeStoreId, "JOURNAL_ADJUSTMENT", entry.Id, totalDebit, userId.Value, cancellationToken);
        }

        return entry.Id;
    }

    public async Task RecordGstTransactionAsync(Guid? storeId, string type, string docNumber, DateTime date, decimal taxable, decimal cgst, decimal sgst, decimal cess, string? gstin, CancellationToken cancellationToken)
    {
        Guid activeStoreId = storeId ?? Guid.Parse("00000000-0000-0000-0000-000000000000");
        
        // Enforce Period Lock Check
        await _periodLockService.CheckPeriodLockAsync(activeStoreId, date, cancellationToken);

        var tax = new TaxTransaction
        {
            StoreId = activeStoreId,
            TransactionType = type,
            DocumentNumber = docNumber,
            TransactionDate = date.Date,
            TaxableAmount = taxable,
            CgstAmount = cgst,
            SgstAmount = sgst,
            CessAmount = cess,
            Gstin = gstin
        };

        _context.TaxTransactions.Add(tax);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
