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

        // Fetch all active batches for the given ProductId
        var batches = await _context.ProductBatches
            .Where(b => b.ProductId == request.ProductId && b.IsActive)
            .ToListAsync(cancellationToken);

        var result = new List<ProductBatchDto>();

        if (batches.Any())
        {
            foreach (var b in batches)
            {
                var currentStock = await _context.StockLedger
                    .Where(sl => sl.ProductId == request.ProductId && sl.BatchId == b.Id)
                    .SumAsync(sl => (decimal?)sl.Quantity, cancellationToken) ?? 0;

                decimal mrpVal = b.Mrp > 0 ? b.Mrp : (product?.Mrp ?? 0);
                decimal sellingPriceVal = b.Mrp > 0 ? b.Mrp : (product?.SellingPrice ?? 0);

                result.Add(new ProductBatchDto
                {
                    Id = b.Id,
                    BatchNumber = b.BatchNumber,
                    ExpiryDate = b.ExpiryDate,
                    CurrentStock = currentStock,
                    Mrp = mrpVal,
                    SellingPrice = sellingPriceVal,
                    CostPrice = b.CostPrice
                });
            }
        }
        else if (product != null && product.Barcodes.Any())
        {
            // Fallback: If no explicit ProductBatch records exist yet, generate batch entries from registered Barcodes
            foreach (var bc in product.Barcodes)
            {
                var currentStock = await _context.StockLedger
                    .Where(sl => sl.ProductId == request.ProductId)
                    .SumAsync(sl => (decimal?)sl.Quantity, cancellationToken) ?? 0;

                result.Add(new ProductBatchDto
                {
                    Id = bc.Id,
                    BatchNumber = bc.BarcodeValue,
                    ExpiryDate = null,
                    CurrentStock = currentStock,
                    Mrp = product.Mrp,
                    SellingPrice = product.SellingPrice,
                    CostPrice = product.PurchasePrice
                });
            }
        }

        return result
            .OrderBy(b => b.ExpiryDate.HasValue ? 0 : 1)
            .ThenBy(b => b.ExpiryDate)
            .ThenBy(b => b.BatchNumber)
            .ToList();
    }
}
