using MediatR;
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Inventory.Queries.GetStockAdjustments;

public record GetStockAdjustmentsQuery(Guid? StoreId) : IRequest<List<StockAdjustmentDto>>;

public class StockAdjustmentDto
{
    public Guid Id { get; set; }
    public string AdjustmentNumber { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string? ApprovedByName { get; set; }
    public List<StockAdjustmentItemDto> Items { get; set; } = new();
}

public class StockAdjustmentItemDto
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public Guid? BatchId { get; set; }
    public string? BatchNumber { get; set; }
    public decimal AdjustedQuantity { get; set; }
    public decimal UnitCost { get; set; }
}

public class GetStockAdjustmentsQueryHandler : IRequestHandler<GetStockAdjustmentsQuery, List<StockAdjustmentDto>>
{
    private readonly IApplicationDbContext _context;

    public GetStockAdjustmentsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<StockAdjustmentDto>> Handle(GetStockAdjustmentsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.StockAdjustments
            .Include(a => a.Items)
            .AsQueryable();

        if (request.StoreId.HasValue)
        {
            query = query.Where(a => a.StoreId == request.StoreId);
        }

        var adjustments = await query
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(cancellationToken);

        // Fetch users, products, and batches to populate details
        var productIds = adjustments.SelectMany(a => a.Items).Select(i => i.ProductId).Distinct().ToList();
        var products = await _context.Products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Name, cancellationToken);

        var batchIds = adjustments.SelectMany(a => a.Items).Where(i => i.BatchId.HasValue).Select(i => i.BatchId!.Value).Distinct().ToList();
        var batches = await _context.ProductBatches
            .Where(b => batchIds.Contains(b.Id))
            .ToDictionaryAsync(b => b.Id, b => b.BatchNumber, cancellationToken);

        var userIds = adjustments.Where(a => a.ApprovedBy.HasValue).Select(a => a.ApprovedBy!.Value).Distinct().ToList();
        var users = await _context.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);

        return adjustments.Select(a => new StockAdjustmentDto
        {
            Id = a.Id,
            AdjustmentNumber = a.AdjustmentNumber,
            Reason = a.Reason,
            Status = a.Status,
            CreatedAt = a.CreatedAt,
            ApprovedByName = a.ApprovedBy.HasValue && users.TryGetValue(a.ApprovedBy.Value, out var name) ? name : null,
            Items = a.Items.Select(i => new StockAdjustmentItemDto
            {
                ProductId = i.ProductId,
                ProductName = products.TryGetValue(i.ProductId, out var prodName) ? prodName : "Unknown Product",
                BatchId = i.BatchId,
                BatchNumber = i.BatchId.HasValue && batches.TryGetValue(i.BatchId.Value, out var bNo) ? bNo : null,
                AdjustedQuantity = i.AdjustedQuantity,
                UnitCost = i.UnitCost
            }).ToList()
        }).ToList();
    }
}
