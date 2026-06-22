using MediatR;
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Finance.Queries;

public record GetJournalEntriesQuery(
    Guid? StoreId,
    DateTime? StartDate,
    DateTime? EndDate,
    string? Status
) : IRequest<List<JournalEntryDto>>;

public class JournalEntryDto
{
    public Guid Id { get; set; }
    public Guid? StoreId { get; set; }
    public string EntryNumber { get; set; } = string.Empty;
    public DateTime EntryDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public string ReferenceDocument { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool IsPosted { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public record GetJournalEntryByIdQuery(Guid Id) : IRequest<JournalEntryDetailDto?>;

public class JournalEntryDetailDto
{
    public Guid Id { get; set; }
    public Guid? StoreId { get; set; }
    public string EntryNumber { get; set; } = string.Empty;
    public DateTime EntryDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public string ReferenceDocument { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool IsPosted { get; set; }
    public string? SourceModule { get; set; }
    public string? SourceDocumentType { get; set; }
    public Guid? SourceDocumentId { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    
    public List<JournalLineDetailDto> Lines { get; set; } = new();
}

public class JournalLineDetailDto
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public string AccountCode { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal DebitAmount { get; set; }
    public decimal CreditAmount { get; set; }
    public Guid? CostCenterId { get; set; }
    public string? CostCenterName { get; set; }
}

public record GetPendingApprovalsQuery(Guid? StoreId) : IRequest<List<ApprovalRequestDto>>;

public class ApprovalRequestDto
{
    public Guid Id { get; set; }
    public Guid StoreId { get; set; }
    public string RequestType { get; set; } = string.Empty;
    public Guid TargetId { get; set; }
    public decimal Amount { get; set; }
    public Guid RequestedById { get; set; }
    public string RequestedByUsername { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    
    public List<ApprovalStepDto> Steps { get; set; } = new();
}

public class ApprovalStepDto
{
    public Guid Id { get; set; }
    public int Level { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public Guid? ActionedById { get; set; }
    public string? ActionedByUsername { get; set; }
    public DateTime? ActionedAt { get; set; }
    public string? Comments { get; set; }
}

public class JournalQueriesHandler :
    IRequestHandler<GetJournalEntriesQuery, List<JournalEntryDto>>,
    IRequestHandler<GetJournalEntryByIdQuery, JournalEntryDetailDto?>,
    IRequestHandler<GetPendingApprovalsQuery, List<ApprovalRequestDto>>
{
    private readonly IApplicationDbContext _context;

    public JournalQueriesHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<JournalEntryDto>> Handle(GetJournalEntriesQuery request, CancellationToken cancellationToken)
    {
        var query = _context.JournalEntries.AsNoTracking();

        if (request.StoreId.HasValue)
        {
            query = query.Where(e => e.StoreId == request.StoreId.Value);
        }

        if (request.StartDate.HasValue)
        {
            query = query.Where(e => e.EntryDate >= request.StartDate.Value.Date);
        }

        if (request.EndDate.HasValue)
        {
            query = query.Where(e => e.EntryDate <= request.EndDate.Value.Date);
        }

        if (!string.IsNullOrEmpty(request.Status))
        {
            query = query.Where(e => e.Status == request.Status);
        }

        return await query
            .OrderByDescending(e => e.EntryDate)
            .ThenByDescending(e => e.CreatedAt)
            .Select(e => new JournalEntryDto
            {
                Id = e.Id,
                StoreId = e.StoreId,
                EntryNumber = e.EntryNumber,
                EntryDate = e.EntryDate,
                Description = e.Description,
                ReferenceDocument = e.ReferenceDocument,
                Status = e.Status,
                IsPosted = e.IsPosted,
                TotalAmount = e.Lines.Sum(l => l.DebitAmount),
                CreatedAt = e.CreatedAt
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<JournalEntryDetailDto?> Handle(GetJournalEntryByIdQuery request, CancellationToken cancellationToken)
    {
        var entry = await _context.JournalEntries
            .AsNoTracking()
            .Include(e => e.Lines)
            .ThenInclude(l => l.Account)
            .FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken);

        if (entry == null) return null;

        var detail = new JournalEntryDetailDto
        {
            Id = entry.Id,
            StoreId = entry.StoreId,
            EntryNumber = entry.EntryNumber,
            EntryDate = entry.EntryDate,
            Description = entry.Description,
            ReferenceDocument = entry.ReferenceDocument,
            Status = entry.Status,
            IsPosted = entry.IsPosted,
            SourceModule = entry.SourceModule,
            SourceDocumentType = entry.SourceDocumentType,
            SourceDocumentId = entry.SourceDocumentId,
            CreatedAt = entry.CreatedAt,
            CreatedBy = entry.CreatedBy,
            Lines = new List<JournalLineDetailDto>()
        };

        foreach (var line in entry.Lines)
        {
            string? ccName = null;
            if (line.CostCenterId.HasValue)
            {
                var cc = await _context.CostCenters
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Id == line.CostCenterId.Value, cancellationToken);
                ccName = cc?.Name;
            }

            detail.Lines.Add(new JournalLineDetailDto
            {
                Id = line.Id,
                AccountId = line.AccountId,
                AccountCode = line.Account.AccountCode,
                AccountName = line.Account.Name,
                Description = line.Description,
                DebitAmount = line.DebitAmount,
                CreditAmount = line.CreditAmount,
                CostCenterId = line.CostCenterId,
                CostCenterName = ccName
            });
        }

        return detail;
    }

    public async Task<List<ApprovalRequestDto>> Handle(GetPendingApprovalsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.ApprovalRequests
            .AsNoTracking()
            .Include(r => r.Steps)
            .Where(r => r.Status == "PENDING");

        if (request.StoreId.HasValue)
        {
            query = query.Where(r => r.StoreId == request.StoreId.Value);
        }

        var results = await query
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);

        var list = new List<ApprovalRequestDto>();

        foreach (var req in results)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == req.RequestedById, cancellationToken);
            
            var dto = new ApprovalRequestDto
            {
                Id = req.Id,
                StoreId = req.StoreId,
                RequestType = req.RequestType,
                TargetId = req.TargetId,
                Amount = req.Amount,
                RequestedById = req.RequestedById,
                RequestedByUsername = user?.Username ?? "Unknown",
                Status = req.Status,
                CreatedAt = req.CreatedAt,
                Steps = new List<ApprovalStepDto>()
            };

            foreach (var step in req.Steps.OrderBy(s => s.Level))
            {
                var actionedUser = step.ActionedById.HasValue 
                    ? await _context.Users.FirstOrDefaultAsync(u => u.Id == step.ActionedById.Value, cancellationToken)
                    : null;

                dto.Steps.Add(new ApprovalStepDto
                {
                    Id = step.Id,
                    Level = step.Level,
                    RoleName = step.RoleName,
                    Status = step.Status,
                    ActionedById = step.ActionedById,
                    ActionedByUsername = actionedUser?.Username,
                    ActionedAt = step.ActionedAt,
                    Comments = step.Comments
                });
            }

            list.Add(dto);
        }

        return list;
    }
}
