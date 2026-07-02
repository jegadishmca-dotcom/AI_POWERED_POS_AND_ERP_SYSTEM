using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Features.Inventory.Services;
using PosErp.Domain.Entities.Auth;
using PosErp.Domain.Entities.Catalog;
using PosErp.Domain.Entities.Inventory;
using PosErp.Infrastructure.Persistence;
using Xunit;

namespace PosErp.IntegrationTests;

[Collection("Database Collection")]
public class F04_StockLockTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly StockLedgerService _stockService;

    private readonly Guid _storeId = Guid.NewGuid();
    private readonly Guid _productId = Guid.NewGuid();

    public F04_StockLockTests()
    {
        // Provision the test database and get the main context
        _context = IntegrationTestDbFactory.Build();
        _stockService = new StockLedgerService(_context);

        // Explicitly enable stock prevention rules for this test run
        var rules = InventoryRulesManager.GetRules();
        rules.PreventNegativeStock = true;
        rules.RowLevelLocking = true;
        InventoryRulesManager.SaveRules(rules);
    }

    public void Dispose()
    {
        // Restore rules to defaults
        var rules = InventoryRulesManager.GetRules();
        rules.PreventNegativeStock = false;
        rules.RowLevelLocking = true;
        InventoryRulesManager.SaveRules(rules);

        _context.Dispose();
    }

    [Fact]
    public async Task StockCheckBlocksSaleWhenInsufficientTest()
    {
        // ── Arrange ───────────────────────────────────────────────────────────
        await SeedProductAndStoreAsync(initialStock: 0);

        // ── Act & Assert ──────────────────────────────────────────────────────
        // Attempting to record a sale of 1 unit when stock is 0 should throw INSUFFICIENT_STOCK
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await _stockService.RecordMovementAsync(
                storeId: _storeId,
                warehouseId: null,
                terminalId: null,
                businessDate: DateTime.Today,
                productId: _productId,
                batchId: null,
                movementType: "SALE",
                quantity: -1m,
                unitCost: 10m,
                expiryDate: null,
                referenceDocId: Guid.NewGuid(),
                referenceNumber: "INV-F04-TEST-1",
                userId: null,
                cancellationToken: CancellationToken.None
            );
        });

        Assert.Contains("INSUFFICIENT_STOCK", ex.Message);

        // Verify no ledger entries were saved to database beyond seed
        var entriesCount = await _context.StockLedger
            .CountAsync(sl => sl.ProductId == _productId && sl.StoreId == _storeId);
        Assert.Equal(0, entriesCount);
    }

    [Fact]
    public async Task StockCheckIsAtomicUnderConcurrentSalesTest()
    {
        // ── Arrange ───────────────────────────────────────────────────────────
        // Seed the product with exactly 1 unit of stock
        await SeedProductAndStoreAsync(initialStock: 1);

        // Prepare the concurrent tasks using separate contexts
        var task1 = Task.Run(() => RecordSaleConcurrentAsync("INV-CONC-1"));
        var task2 = Task.Run(() => RecordSaleConcurrentAsync("INV-CONC-2"));

        // ── Act ───────────────────────────────────────────────────────────────
        // Execute both tasks concurrently and catch results
        var results = await Task.WhenAll(task1, task2);

        // ── Assert ────────────────────────────────────────────────────────────
        // Exactly one task should succeed (returning true) and one should throw / return false
        var successCount = results.Count(r => r.Success);
        var failureCount = results.Count(r => !r.Success);

        Assert.Equal(1, successCount);
        Assert.Equal(1, failureCount);

        // Exactly one failure must be due to INSUFFICIENT_STOCK exception
        var failedResult = results.First(r => !r.Success);
        Assert.NotNull(failedResult.Error);
        Assert.Contains("INSUFFICIENT_STOCK", failedResult.Error.Message);

        // Verify the database state:
        // Main context should see exactly two ledger entries for this product:
        // 1) The initial stock-in entry (Quantity = +1)
        // 2) The single successful sale entry (Quantity = -1)
        var finalEntries = await _context.StockLedger
            .Where(sl => sl.ProductId == _productId && sl.StoreId == _storeId)
            .OrderBy(sl => sl.CreatedAt)
            .ToListAsync();

        Assert.Equal(2, finalEntries.Count);
        
        // Initial Entry
        Assert.Equal(1m, finalEntries[0].Quantity);
        Assert.Equal(1m, finalEntries[0].RunningBalance);

        // Sale Entry
        Assert.Equal(-1m, finalEntries[1].Quantity);
        Assert.Equal(0m, finalEntries[1].RunningBalance); // Final balance must be 0, not -1
    }

    // ─── Concurrent Helpers ────────────────────────────────────────────────────

    private class SaleResult
    {
        public bool Success { get; set; }
        public Exception? Error { get; set; }
    }

    private async Task<SaleResult> RecordSaleConcurrentAsync(string refNum)
    {
        // Each task MUST use its own context instance to be thread-safe
        using var threadContext = IntegrationTestDbFactory.CreateNewContext();
        var threadService = new StockLedgerService(threadContext);

        try
        {
            await threadService.RecordMovementAsync(
                storeId: _storeId,
                warehouseId: null,
                terminalId: null,
                businessDate: DateTime.Today,
                productId: _productId,
                batchId: null,
                movementType: "SALE",
                quantity: -1m,
                unitCost: 10m,
                expiryDate: null,
                referenceDocId: Guid.NewGuid(),
                referenceNumber: refNum,
                userId: null,
                cancellationToken: CancellationToken.None
            );
            return new SaleResult { Success = true };
        }
        catch (Exception ex)
        {
            return new SaleResult { Success = false, Error = ex };
        }
    }

    private async Task SeedProductAndStoreAsync(decimal initialStock)
    {
        // Seed store
        var store = new Store
        {
            Id = _storeId,
            StoreCode = $"S-{_storeId.ToString("N")[..5]}",
            StoreName = "Stock Lock Test Store",
            IsActive = true
        };
        _context.Stores.Add(store);

        // Seed tax slab
        var taxSlab = await _context.TaxSlabs.FirstOrDefaultAsync();
        if (taxSlab == null)
        {
            taxSlab = new TaxSlab
            {
                Id = Guid.NewGuid(),
                Name = "GST 18%",
                CgstRate = 9m,
                SgstRate = 9m,
                IgstRate = 18m,
                CessRate = 0m
            };
            _context.TaxSlabs.Add(taxSlab);
        }

        // Seed Unit of Measure
        var uomId = await _context.UnitOfMeasures.Select(u => u.Id).FirstOrDefaultAsync();
        if (uomId == Guid.Empty)
        {
            var uom = new UnitOfMeasure
            {
                Id = Guid.NewGuid(),
                Symbol = "PCS",
                Name = "Pieces"
            };
            _context.UnitOfMeasures.Add(uom);
            uomId = uom.Id;
        }

        // Seed product
        var product = new Product
        {
            Id = _productId,
            ProductCode = $"P-{_productId.ToString("N")[..5]}",
            Name = "Stock Lock Test Product",
            TaxSlabId = taxSlab.Id,
            UnitOfMeasureId = uomId,
            Mrp = 100m,
            SellingPrice = 80m,
            PurchasePrice = 50m,
            IsActive = true,
            HasExpiry = false
        };
        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        if (initialStock > 0)
        {
            // Record an initial stock movement to establish positive inventory balance
            await _stockService.RecordMovementAsync(
                storeId: _storeId,
                warehouseId: null,
                terminalId: null,
                businessDate: DateTime.Today,
                productId: _productId,
                batchId: null,
                movementType: "STOCK_TAKE",
                quantity: initialStock,
                unitCost: 50m,
                expiryDate: null,
                referenceDocId: Guid.NewGuid(),
                referenceNumber: "SEED-STOCK",
                userId: null,
                cancellationToken: CancellationToken.None
            );
        }
    }
}
