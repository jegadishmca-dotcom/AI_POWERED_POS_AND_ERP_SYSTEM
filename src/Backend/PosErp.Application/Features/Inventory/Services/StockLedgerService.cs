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
        var db = (DbContext)_context;
        var hasExistingTransaction = db.Database.CurrentTransaction != null;

        if (hasExistingTransaction)
        {
            await ExecuteMovementAsync(null);
        }
        else
        {
            var strategy = db.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
                await ExecuteMovementAsync(transaction);
            });
        }

        async Task ExecuteMovementAsync(Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? transaction)
        {
            try 
            {
                var rules = InventoryRulesManager.GetRules();
                if (rules.RowLevelLocking)
                {
                    await db.Database.ExecuteSqlRawAsync(
                        "SELECT 1 FROM products WHERE id = {0} FOR UPDATE", 
                        new object[] { productId }, 
                        cancellationToken);
                }

                // 1. Get the latest running balance directly instead of summing (Optimized)
                var lastEntry = await _context.StockLedger
                    .Where(x => x.ProductId == productId && x.StoreId == storeId)
                    .OrderByDescending(x => x.CreatedAt)
                    .FirstOrDefaultAsync(cancellationToken);

                decimal currentBalance = lastEntry?.RunningBalance ?? 0;
                decimal newBalance = currentBalance + quantity;

                if (newBalance < 0 && movementType == "SALE" && rules.PreventNegativeStock)
                {
                    var productName = await _context.Products
                        .Where(p => p.Id == productId)
                        .Select(p => p.Name)
                        .FirstOrDefaultAsync(cancellationToken) ?? "Unknown Product";
                    throw new InvalidOperationException($"INSUFFICIENT_STOCK: Item '{productName}' is out of stock. Available: {currentBalance}, Requested: {-quantity}. Scan a supervisor PIN to override.");
                }

                string finalMovementType = movementType;
                if (movementType == "SALE" || movementType == "SALE_OVERRIDE")
                {
                    finalMovementType = (movementType == "SALE_OVERRIDE" && newBalance < 0) ? "SALE_OVERRIDE" : "SALE";
                }

                var finalExpiryDate = expiryDate;
                if (!finalExpiryDate.HasValue && batchId.HasValue)
                {
                    var batch = await _context.ProductBatches.FindAsync(new object[] { batchId.Value }, cancellationToken);
                    finalExpiryDate = batch?.ExpiryDate;
                }

                // 2. Create Immutable Entry
                var entry = new StockLedgerEntry
                {
                    StoreId = storeId,
                    WarehouseId = warehouseId,
                    TerminalId = terminalId,
                    BusinessDate = businessDate,
                    ProductId = productId,
                    BatchId = batchId,
                    MovementType = finalMovementType,
                    Quantity = quantity,
                    UnitCost = unitCost,
                    ExpiryDate = finalExpiryDate,
                    ReferenceDocumentId = referenceDocId,
                    ReferenceNumber = referenceNumber,
                    RunningBalance = newBalance,
                    CreatedBy = userId
                };

                _context.StockLedger.Add(entry);
                
                if (transaction != null)
                {
                    await _context.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                }
            }
            catch (DbUpdateConcurrencyException ex)
            {
                if (transaction != null) await transaction.RollbackAsync(cancellationToken);
                // Handle Postgres xmin concurrency failures
                throw new Exception("Stock movement concurrency conflict. Please retry.", ex);
            }
            catch (Exception)
            {
                if (transaction != null) await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }
    }
}
