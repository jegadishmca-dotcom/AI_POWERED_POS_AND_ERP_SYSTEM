using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using PosErp.Domain.Entities.Inventory;
using PosErp.Domain.Entities.Catalog;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Inventory.Services;

public interface IProductBatchService
{
    Task<ProductBatch> CreateOrGetBatchAsync(
        Guid storeId,
        Guid productId,
        string? batchNumber,
        DateTime? mfgDate,
        DateTime? expiryDate,
        decimal unitCost,
        CancellationToken cancellationToken);
}

public class ProductBatchService : IProductBatchService
{
    private readonly IApplicationDbContext _context;

    public ProductBatchService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ProductBatch> CreateOrGetBatchAsync(
        Guid storeId,
        Guid productId,
        string? batchNumber,
        DateTime? mfgDate,
        DateTime? expiryDate,
        decimal unitCost,
        CancellationToken cancellationToken)
    {
        var product = await _context.Products.FindAsync(new object[] { productId }, cancellationToken);
        if (product == null) throw new Exception("Product not found.");

        if (product.HasExpiry && !expiryDate.HasValue)
        {
            throw new Exception($"Product '{product.Name}' requires an Expiry Date.");
        }

        string finalBatchNumber = string.IsNullOrWhiteSpace(batchNumber) 
            ? $"INT-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString().Substring(0,4).ToUpper()}" 
            : batchNumber;

        // Try to find existing batch
        var existingBatch = await _context.ProductBatches
            .FirstOrDefaultAsync(b => b.ProductId == productId && b.BatchNumber == finalBatchNumber, cancellationToken);

        if (existingBatch != null) return existingBatch;

        // Note: Margin calculation should ideally come from a pricing engine or PriceList. 
        // For Phase 1, we set Mrp = UnitCost initially; it must be updated by pricing module.
        var batch = new ProductBatch
        {
            StoreId = storeId,
            ProductId = productId,
            BatchNumber = finalBatchNumber,
            MfgDate = mfgDate,
            ExpiryDate = expiryDate,
            CostPrice = unitCost,
            Mrp = unitCost, // Replaces hardcoded 1.3m margin
            IsActive = true
        };

        _context.ProductBatches.Add(batch);
        await _context.SaveChangesAsync(cancellationToken);
        
        return batch;
    }
}
