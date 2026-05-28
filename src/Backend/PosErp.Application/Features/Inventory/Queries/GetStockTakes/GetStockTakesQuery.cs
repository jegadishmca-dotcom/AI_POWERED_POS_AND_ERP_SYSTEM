using MediatR;
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Inventory.Queries.GetStockTakes;

public record GetStockTakesQuery(Guid? StoreId) : IRequest<List<StockTakeDto>>;

public class StockTakeDto
{
    public Guid Id { get; set; }
    public string TakeNumber { get; set; } = string.Empty;
    public DateTime ScheduledDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public int TotalItemsCount { get; set; }
    public string? ApprovedByName { get; set; }
}

public class GetStockTakesQueryHandler : IRequestHandler<GetStockTakesQuery, List<StockTakeDto>>
{
    private readonly IApplicationDbContext _context;

    public GetStockTakesQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<StockTakeDto>> Handle(GetStockTakesQuery request, CancellationToken cancellationToken)
    {
        var query = _context.StockTakeHeaders
            .Include(t => t.Items)
            .AsQueryable();

        if (request.StoreId.HasValue)
        {
            query = query.Where(t => t.StoreId == request.StoreId);
        }

        var takes = await query
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(cancellationToken);

        // Fetch users to populate ApprovedByName
        var userIds = takes.Where(t => t.ApprovedBy.HasValue).Select(t => t.ApprovedBy!.Value).Distinct().ToList();
        var users = await _context.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);

        return takes.Select(t => new StockTakeDto
        {
            Id = t.Id,
            TakeNumber = t.TakeNumber,
            ScheduledDate = t.ScheduledDate,
            Status = t.Status,
            CreatedAt = t.CreatedAt,
            TotalItemsCount = t.Items.Count,
            ApprovedByName = t.ApprovedBy.HasValue && users.TryGetValue(t.ApprovedBy.Value, out var name) ? name : null
        }).ToList();
    }
}
