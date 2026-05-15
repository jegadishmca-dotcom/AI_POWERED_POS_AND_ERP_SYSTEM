$backendDir = "d:\JEGADISH\AI_POWERED_POS_AND_ERP_SYSTEM\src\Backend"

# 1. Update PurchasingEntities to include RejectionReason
$purchasingEntitiesFile = "$backendDir\PosErp.Domain\Entities\Purchasing\PurchasingEntities.cs"
$content = Get-Content -Path $purchasingEntitiesFile -Raw
$content = $content -replace "public decimal RejectedQuantity \{ get; set; \}", "public decimal RejectedQuantity { get; set; }`n    public string? RejectionReason { get; set; }"
$content | Out-File -FilePath $purchasingEntitiesFile -Encoding utf8

# 2. Create ProductBatchService
@"
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
"@ | Out-File -FilePath "$backendDir\PosErp.Application\Features\Inventory\Services\ProductBatchService.cs" -Encoding utf8

# 3. Refine ConfirmGRNCommand
@"
using MediatR;
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using PosErp.Application.Features.Inventory.Services;
using PosErp.Domain.Entities.Inventory;
using PosErp.Domain.Entities.Purchasing;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Purchasing.Commands.ConfirmGRN;

public record ConfirmGRNCommand(Guid GrnId, Guid? UserId) : IRequest<bool>;

public class ConfirmGRNCommandHandler : IRequestHandler<ConfirmGRNCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly IStockLedgerService _stockLedgerService;
    private readonly IProductBatchService _batchService;

    public ConfirmGRNCommandHandler(IApplicationDbContext context, IStockLedgerService stockLedgerService, IProductBatchService batchService)
    {
        _context = context;
        _stockLedgerService = stockLedgerService;
        _batchService = batchService;
    }

    public async Task<bool> Handle(ConfirmGRNCommand request, CancellationToken cancellationToken)
    {
        using var transaction = await ((DbContext)_context).Database.BeginTransactionAsync(cancellationToken);
        try 
        {
            var grn = await _context.GRNHeaders
                .Include(g => g.Items)
                .FirstOrDefaultAsync(g => g.Id == request.GrnId, cancellationToken);

            if (grn == null || grn.Status != "DRAFT") throw new Exception("Invalid GRN or not in DRAFT status");

            var po = await _context.PurchaseOrders
                .Include(p => p.Items)
                .FirstOrDefaultAsync(p => p.Id == grn.PurchaseOrderHeaderId, cancellationToken);
                
            if (po == null) throw new Exception("Purchase Order not found");

            foreach (var item in grn.Items)
            {
                if (item.AcceptedQuantity <= 0) continue;

                // 1. Safe Batch Generation using proper Service (enforces HasExpiry check)
                var batch = await _batchService.CreateOrGetBatchAsync(
                    grn.StoreId ?? Guid.Empty,
                    item.ProductId,
                    item.BatchNumber,
                    item.MfgDate,
                    item.ExpiryDate,
                    item.UnitCost,
                    cancellationToken
                );

                item.BatchId = batch.Id; 

                // 2. Increment PO Received Quantity 
                var poItem = po.Items.FirstOrDefault(p => p.Id == item.PurchaseOrderItemId);
                if (poItem != null) poItem.ReceivedQuantity += item.AcceptedQuantity;

                // 3. Record stock movement
                await _stockLedgerService.RecordMovementAsync(
                    storeId: grn.StoreId ?? Guid.Empty,
                    warehouseId: null,
                    terminalId: null,
                    businessDate: grn.ReceivedDate,
                    productId: item.ProductId,
                    batchId: batch.Id,
                    movementType: "GRN",
                    quantity: item.AcceptedQuantity, 
                    unitCost: item.UnitCost,
                    expiryDate: item.ExpiryDate,
                    referenceDocId: grn.Id,
                    referenceNumber: grn.GrnNumber,
                    userId: request.UserId,
                    cancellationToken: cancellationToken
                );
            }

            grn.Status = "CONFIRMED";
            
            bool isFullReceipt = po.Items.All(p => p.ReceivedQuantity >= p.OrderedQuantity);
            po.Status = isFullReceipt ? "FULL_GRN" : "PARTIAL_GRN";

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new Exception("Concurrency conflict during GRN confirmation. Please retry.", ex);
        }
        catch (Exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
"@ | Out-File -FilePath "$backendDir\PosErp.Application\Features\Purchasing\Commands\ConfirmGRN\ConfirmGRNCommand.cs" -Encoding utf8

Write-Host "Backend GRN Refined"
