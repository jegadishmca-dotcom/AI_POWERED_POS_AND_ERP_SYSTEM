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

        foreach (var b in explicitBatches)
        {
            var currentStock = await _context.StockLedger
                .Where(sl => sl.ProductId == request.ProductId && sl.BatchId == b.Id)
                .SumAsync(sl => (decimal?)sl.Quantity, cancellationToken) ?? 0;

            decimal mrpVal = b.Mrp > 0 ? b.Mrp : product.Mrp;
            decimal sellingPriceVal = b.Mrp > 0 ? b.Mrp : product.SellingPrice;

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

        // 2. ALSO check all registered Barcodes for this product so every barcode variant acts as a batch selection
        if (product.Barcodes != null)
        {
            foreach (var bc in product.Barcodes)
            {
                if (!string.IsNullOrWhiteSpace(bc.BarcodeValue) && !resultDict.ContainsKey(bc.BarcodeValue.Trim()))
                {
                    var currentStock = await _context.StockLedger
                        .Where(sl => sl.ProductId == request.ProductId)
                        .SumAsync(sl => (decimal?)sl.Quantity, cancellationToken) ?? 0;

                    resultDict[bc.BarcodeValue.Trim()] = new ProductBatchDto
                    {
                        Id = Guid.Empty, // Set Guid.Empty so barcode fallbacks don't pass ProductBarcode.Id as ProductBatch.Id
                        BatchNumber = bc.BarcodeValue.Trim(),
                        ExpiryDate = null,
                        CurrentStock = currentStock,
                        Mrp = product.Mrp,
                        SellingPrice = product.SellingPrice,
                        CostPrice = product.PurchasePrice
                    };
                }
            }
        }

        return resultDict.Values
            .OrderBy(b => b.ExpiryDate.HasValue ? 0 : 1)
            .ThenBy(b => b.ExpiryDate)
            .ThenBy(b => b.BatchNumber)
            .ToList();
    }
}
