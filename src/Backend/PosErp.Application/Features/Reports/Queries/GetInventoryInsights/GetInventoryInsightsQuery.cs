using MediatR;
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Reports.Queries.GetInventoryInsights;

public record GetInventoryInsightsQuery : IRequest<InventoryInsightsDto>;

public class InventoryInsightsDto
{
    public decimal TotalValuation { get; set; }
    public int LowStockCount { get; set; }
    public int NearExpiryCount { get; set; }
    public List<LowStockItemDto> LowStockItems { get; set; } = new();
    public List<NearExpiryBatchDto> NearExpiryBatches { get; set; } = new();
}

public class LowStockItemDto
{
    public Guid ProductId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal CurrentStock { get; set; }
    public decimal ReorderPoint { get; set; }
}

public class NearExpiryBatchDto
{
    public Guid BatchId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string BatchNumber { get; set; } = string.Empty;
    public DateTime? ExpiryDate { get; set; }
    public int DaysRemaining { get; set; }
}

public class GetInventoryInsightsQueryHandler : IRequestHandler<GetInventoryInsightsQuery, InventoryInsightsDto>
{
    private readonly IApplicationDbContext _context;

    public GetInventoryInsightsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<InventoryInsightsDto> Handle(GetInventoryInsightsQuery request, CancellationToken cancellationToken)
    {
        var today = DateTime.UtcNow.Date;
        var thirtyDaysFromNow = today.AddDays(30);

        // Fetch sum of quantities from the StockLedger grouped by ProductId to get current stock levels
        var stockLedgerSums = await _context.StockLedger
            .GroupBy(le => le.ProductId)
            .Select(g => new {
                ProductId = g.Key,
                CurrentStock = g.Sum(le => le.Quantity)
            })
            .ToListAsync(cancellationToken);

        var stockTotals = stockLedgerSums.ToDictionary(x => x.ProductId, x => x.CurrentStock);

        // Fetch all active products
        var activeProducts = await _context.Products
            .Where(p => p.IsActive && !p.IsDeleted)
            .Select(p => new {
                p.Id,
                p.ProductCode,
                p.Name,
                p.ReorderPoint,
                p.PurchasePrice
            })
            .ToListAsync(cancellationToken);

        decimal totalValuation = 0;
        var lowStockList = new List<LowStockItemDto>();
        int lowStockCount = 0;

        foreach (var p in activeProducts)
        {
            stockTotals.TryGetValue(p.Id, out var currentStock);
            totalValuation += currentStock * p.PurchasePrice;

            if (currentStock < p.ReorderPoint)
            {
                lowStockCount++;
                lowStockList.Add(new LowStockItemDto
                {
                    ProductId = p.Id,
                    ProductCode = p.ProductCode,
                    ProductName = p.Name,
                    CurrentStock = currentStock,
                    ReorderPoint = p.ReorderPoint
                });
            }
        }

        // Limit list to top 15 low stock items
        var orderedLowStockList = lowStockList
            .OrderBy(p => p.CurrentStock)
            .Take(15)
            .ToList();

        // Get expired and near expiry batches (expiry <= 30 days)
        var nearExpiryBatches = await _context.ProductBatches
            .Include(b => b.Product)
            .Where(b => b.ExpiryDate != null && b.ExpiryDate <= thirtyDaysFromNow && b.IsActive)
            .Select(b => new NearExpiryBatchDto
            {
                BatchId = b.Id,
                ProductCode = b.Product.ProductCode,
                ProductName = b.Product.Name,
                BatchNumber = b.BatchNumber,
                ExpiryDate = b.ExpiryDate,
                DaysRemaining = b.ExpiryDate.HasValue ? (b.ExpiryDate.Value.Date - today).Days : 0
            })
            .OrderBy(b => b.ExpiryDate)
            .Take(15) // Limit to top 15 expiring batches
            .ToListAsync(cancellationToken);

        return new InventoryInsightsDto
        {
            TotalValuation = totalValuation,
            LowStockCount = lowStockCount,
            NearExpiryCount = nearExpiryBatches.Count,
            LowStockItems = orderedLowStockList,
            NearExpiryBatches = nearExpiryBatches
        };
    }
}
