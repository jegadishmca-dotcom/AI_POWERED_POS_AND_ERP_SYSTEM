using MediatR;
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Inventory.Queries.GetStockTakeDetails;

public record GetStockTakeDetailsQuery(Guid StockTakeId) : IRequest<StockTakeDetailDto>;

public class StockTakeDetailDto
{
    public Guid Id { get; set; }
    public string TakeNumber { get; set; } = string.Empty;
    public DateTime ScheduledDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string? ApprovedByName { get; set; }
    public List<StockTakeItemDto> Items { get; set; } = new();
}

public class StockTakeItemDto
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public Guid? BatchId { get; set; }
    public string? BatchNumber { get; set; }
    public decimal SystemQuantity { get; set; }
    public decimal PhysicalQuantity { get; set; }
    public decimal VarianceQuantity { get; set; }
}

public class GetStockTakeDetailsQueryHandler : IRequestHandler<GetStockTakeDetailsQuery, StockTakeDetailDto>
{
    private readonly IApplicationDbContext _context;

    public GetStockTakeDetailsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<StockTakeDetailDto> Handle(GetStockTakeDetailsQuery request, CancellationToken cancellationToken)
    {
        var take = await _context.StockTakeHeaders
            .Include(t => t.Items)
            .FirstOrDefaultAsync(t => t.Id == request.StockTakeId, cancellationToken);

        if (take == null) throw new Exception("Stock Take not found.");

        // Fetch products, batches, and approver details
        var productIds = take.Items.Select(i => i.ProductId).Distinct().ToList();
        var products = await _context.Products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Name, cancellationToken);

        var batchIds = take.Items.Where(i => i.BatchId.HasValue).Select(i => i.BatchId!.Value).Distinct().ToList();
        var batches = await _context.ProductBatches
            .Where(b => batchIds.Contains(b.Id))
            .ToDictionaryAsync(b => b.Id, b => b.BatchNumber, cancellationToken);

        string? approvedByName = null;
        if (take.ApprovedBy.HasValue)
        {
            var user = await _context.Users.FindAsync(new object[] { take.ApprovedBy.Value }, cancellationToken);
            approvedByName = user?.FullName;
        }

        return new StockTakeDetailDto
        {
            Id = take.Id,
            TakeNumber = take.TakeNumber,
            ScheduledDate = take.ScheduledDate,
            Status = take.Status,
            CreatedAt = take.CreatedAt,
            ApprovedByName = approvedByName,
            Items = take.Items.Select(i => new StockTakeItemDto
            {
                ProductId = i.ProductId,
                ProductName = products.TryGetValue(i.ProductId, out var prodName) ? prodName : "Unknown Product",
                BatchId = i.BatchId,
                BatchNumber = i.BatchId.HasValue && batches.TryGetValue(i.BatchId.Value, out var bNo) ? bNo : "No Batch",
                SystemQuantity = i.SystemQuantity,
                PhysicalQuantity = i.PhysicalQuantity,
                VarianceQuantity = i.PhysicalQuantity - i.SystemQuantity
            }).ToList()
        };
    }
}
