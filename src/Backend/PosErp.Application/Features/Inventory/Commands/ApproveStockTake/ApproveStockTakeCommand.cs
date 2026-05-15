using MediatR;
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using PosErp.Application.Features.Inventory.Services;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Inventory.Commands.ApproveStockTake;

public record ApproveStockTakeCommand(Guid StockTakeId, Guid? ApproverId) : IRequest<bool>;

public class ApproveStockTakeCommandHandler : IRequestHandler<ApproveStockTakeCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly IStockLedgerService _stockLedgerService;

    public ApproveStockTakeCommandHandler(IApplicationDbContext context, IStockLedgerService stockLedgerService)
    {
        _context = context;
        _stockLedgerService = stockLedgerService;
    }

    public async Task<bool> Handle(ApproveStockTakeCommand request, CancellationToken cancellationToken)
    {
        using var transaction = await ((DbContext)_context).Database.BeginTransactionAsync(cancellationToken);
        try
        {
            // Note: Uses DbSet mapped in DbContext
            // For scaffold, assume context.Set<StockTakeHeader>()
            var take = await _context.Set<PosErp.Domain.Entities.Inventory.StockTakeHeader>()
                .Include(t => t.Items)
                .FirstOrDefaultAsync(t => t.Id == request.StockTakeId, cancellationToken);
                
            if (take == null || take.Status != "REVIEW") throw new Exception("Stock Take not ready for approval.");

            take.Status = "APPROVED";
            take.ApprovedBy = request.ApproverId;

            foreach (var item in take.Items)
            {
                if (item.VarianceQuantity == 0) continue;

                // Create Auto-Adjustment for variance
                await _stockLedgerService.RecordMovementAsync(
                    storeId: take.StoreId ?? Guid.Empty,
                    warehouseId: null,
                    terminalId: null,
                    businessDate: DateTime.UtcNow,
                    productId: item.ProductId,
                    batchId: item.BatchId,
                    movementType: "ADJ", // Automated variance correction
                    quantity: item.VarianceQuantity, // Will be negative if missing stock
                    unitCost: 0, // Should be fetched from product/batch in real app
                    expiryDate: null,
                    referenceDocId: take.Id,
                    referenceNumber: $"TAKE-VAR-{take.TakeNumber}",
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
