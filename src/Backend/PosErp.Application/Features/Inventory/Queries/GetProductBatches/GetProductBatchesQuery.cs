using MediatR;
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Inventory.Queries.GetProductBatches;

public record GetProductBatchesQuery(Guid ProductId) : IRequest<List<ProductBatchDto>>;

public class ProductBatchDto
{
    public Guid Id { get; set; }
    public string BatchNumber { get; set; } = string.Empty;
    public DateTime? ExpiryDate { get; set; }
    public decimal CurrentStock { get; set; }
    public decimal Mrp { get; set; }
    public decimal SellingPrice { get; set; }
    public decimal CostPrice { get; set; }
}

public class GetProductBatchesQueryHandler : IRequestHandler<GetProductBatchesQuery, List<ProductBatchDto>>
{
    private readonly IApplicationDbContext _context;

    public GetProductBatchesQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<ProductBatchDto>> Handle(GetProductBatchesQuery request, CancellationToken cancellationToken)
    {
        var product = await _context.Products
            .Include(p => p.Barcodes)
            .FirstOrDefaultAsync(p => p.Id == request.ProductId, cancellationToken);

        if (product == null) return new List<ProductBatchDto>();

        // 1. Fetch explicit active ProductBatches
        var explicitBatches = await _context.ProductBatches
            .Where(b => b.ProductId == request.ProductId && b.IsActive)
            .ToListAsync(cancellationToken);

        var resultDict = new Dictionary<string, ProductBatchDto>(StringComparer.OrdinalIgnoreCase);

        // Derive price difference between master MRP and SellingPrice
        decimal discount = Math.Max(0, product.Mrp - product.SellingPrice);

        foreach (var b in explicitBatches)
        {
            var stockLedgerQty = await _context.StockLedger
                .Where(sl => sl.ProductId == request.ProductId && sl.BatchId == b.Id)
                .SumAsync(sl => (decimal?)sl.Quantity, cancellationToken) ?? 0;

            // Prioritize StockLedger if tracked, otherwise fallback to AvailableQuantity
            var currentStock = stockLedgerQty != 0 ? stockLedgerQty : b.AvailableQuantity;

            decimal mrpVal = b.Mrp > 0 ? b.Mrp : product.Mrp;
            
            // Calculate SellingPrice matching product discount ratio or difference
            decimal sellingPriceVal = mrpVal > 0 
                ? (product.Mrp > 0 && discount > 0 ? Math.Max(b.CostPrice > 0 ? b.CostPrice : 0, mrpVal - discount) : mrpVal)
                : product.SellingPrice;

            if (!string.IsNullOrWhiteSpace(b.BatchNumber))
            {
                resultDict[b.BatchNumber.Trim()] = new ProductBatchDto
                {
                    Id = b.Id,
                    BatchNumber = b.BatchNumber.Trim(),
                    ExpiryDate = b.ExpiryDate,
                    CurrentStock = currentStock,
                    Mrp = mrpVal,
                    SellingPrice = sellingPriceVal,
                    CostPrice = b.CostPrice
                };
            }
        }

        // 2. Only include an "UNBATCHED (General Stock)" option if NO explicit batches exist
        if (resultDict.Count == 0)
        {
            var totalStock = await _context.StockLedger
                .Where(sl => sl.ProductId == request.ProductId)
                .SumAsync(sl => (decimal?)sl.Quantity, cancellationToken) ?? 0;

            resultDict["UNBATCHED"] = new ProductBatchDto
            {
                Id = Guid.Empty,
                BatchNumber = "UNBATCHED (General Stock)",
                ExpiryDate = null,
                CurrentStock = totalStock,
                Mrp = product.Mrp,
                SellingPrice = product.SellingPrice,
                CostPrice = product.PurchasePrice
            };
        }

        return resultDict.Values
            .OrderByDescending(b => b.CurrentStock > 0 ? 1 : 0)
            .ThenBy(b => b.Mrp)
            .ThenBy(b => b.BatchNumber)
            .ToList();
    }
}
