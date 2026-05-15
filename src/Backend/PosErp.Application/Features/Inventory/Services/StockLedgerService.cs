using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using PosErp.Domain.Entities.Inventory;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Inventory.Services;

public interface IStockLedgerService
{
    Task RecordMovementAsync(
        Guid storeId,
        Guid? warehouseId,
        Guid? terminalId,
        DateTime businessDate,
        Guid productId,
        Guid? batchId,
        string movementType,
        decimal quantity,
        decimal unitCost,
        DateTime? expiryDate,
        Guid referenceDocId,
        string referenceNumber,
        Guid? userId,
        CancellationToken cancellationToken);
}

public class StockLedgerService : IStockLedgerService
{
    private readonly IApplicationDbContext _context;

    public StockLedgerService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task RecordMovementAsync(
        Guid storeId,
        Guid? warehouseId,
        Guid? terminalId,
        DateTime businessDate,
        Guid productId,
        Guid? batchId,
        string movementType,
        decimal quantity,
        decimal unitCost,
        DateTime? expiryDate,
        Guid referenceDocId,
        string referenceNumber,
        Guid? userId,
        CancellationToken cancellationToken)
    {
        // Enforce transaction for atomic ledger update
        using var transaction = await ((DbContext)_context).Database.BeginTransactionAsync(cancellationToken);
        
        try 
        {
            // 1. Get the latest running balance directly instead of summing (Optimized)
            var lastEntry = await _context.StockLedger
                .Where(x => x.ProductId == productId && x.StoreId == storeId)
                .OrderByDescending(x => x.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            decimal currentBalance = lastEntry?.RunningBalance ?? 0;
            decimal newBalance = currentBalance + quantity;

            // 2. Create Immutable Entry
            var entry = new StockLedgerEntry
            {
                StoreId = storeId,
                WarehouseId = warehouseId,
                TerminalId = terminalId,
                BusinessDate = businessDate,
                ProductId = productId,
                BatchId = batchId,
                MovementType = movementType,
                Quantity = quantity,
                UnitCost = unitCost,
                ExpiryDate = expiryDate,
                ReferenceDocumentId = referenceDocId,
                ReferenceNumber = referenceNumber,
                RunningBalance = newBalance,
                CreatedBy = userId
            };

            _context.StockLedger.Add(entry);
            
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            // Handle Postgres xmin concurrency failures
            throw new Exception("Stock movement concurrency conflict. Please retry.", ex);
        }
        catch (Exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
