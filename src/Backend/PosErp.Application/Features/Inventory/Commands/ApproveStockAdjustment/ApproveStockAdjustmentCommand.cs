using MediatR;
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using PosErp.Application.Features.Inventory.Services;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Inventory.Commands.ApproveStockAdjustment;

public record ApproveStockAdjustmentCommand(Guid AdjustmentId, Guid? ApproverId) : IRequest<bool>;

public class ApproveStockAdjustmentCommandHandler : IRequestHandler<ApproveStockAdjustmentCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly IStockLedgerService _stockLedgerService;

    public ApproveStockAdjustmentCommandHandler(IApplicationDbContext context, IStockLedgerService stockLedgerService)
    {
        _context = context;
        _stockLedgerService = stockLedgerService;
    }

    public async Task<bool> Handle(ApproveStockAdjustmentCommand request, CancellationToken cancellationToken)
    {
        using var transaction = await ((DbContext)_context).Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var adj = await _context.StockAdjustments
                .Include(a => a.Items)
                .FirstOrDefaultAsync(a => a.Id == request.AdjustmentId, cancellationToken);
                
            if (adj == null || adj.Status != "PENDING") throw new Exception("Invalid or already processed Adjustment.");

            adj.Status = "APPROVED";
            adj.ApprovedBy = request.ApproverId;

            foreach(var item in adj.Items)
            {
                if (item.AdjustedQuantity == 0) continue;

                await _stockLedgerService.RecordMovementAsync(
                    storeId: adj.StoreId ?? Guid.Empty,
                    warehouseId: null,
                    terminalId: null,
                    businessDate: DateTime.UtcNow,
                    productId: item.ProductId,
                    batchId: item.BatchId,
                    movementType: "ADJ", // Key movement type
                    quantity: item.AdjustedQuantity, // Can be -ve or +ve
                    unitCost: item.UnitCost,
                    expiryDate: null,
                    referenceDocId: adj.Id,
                    referenceNumber: adj.AdjustmentNumber,
                    userId: request.ApproverId,
                    cancellationToken: cancellationToken
                );
            }

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new Exception("Concurrency conflict. Please retry.", ex);
        }
        catch (Exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
