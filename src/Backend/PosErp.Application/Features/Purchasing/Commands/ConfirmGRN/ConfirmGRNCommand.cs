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
