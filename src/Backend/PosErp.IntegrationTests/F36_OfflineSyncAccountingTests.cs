using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using PosErp.Application.Features.Offers.Services;
using PosErp.Application.Features.Finance.Services;
using PosErp.Application.Features.Inventory.Services;
using PosErp.Application.Features.Crm.Services;
using PosErp.Application.Features.Pos.Commands;
using PosErp.Application.Features.Pos.Commands.SyncInvoices;
using PosErp.Domain.Entities.Catalog;
using PosErp.Domain.Entities.Finance;
using PosErp.Domain.Entities.Pos;
using PosErp.Domain.Entities.Auth;
using PosErp.Domain.Entities.Inventory;
using PosErp.Infrastructure.Persistence;
using PosErp.Infrastructure.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace PosErp.IntegrationTests;

[Collection("Database Collection")]
public class F36_OfflineSyncAccountingTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Guid _terminalId = Guid.NewGuid();
    private readonly Guid _cashierId = Guid.NewGuid();
    private readonly Guid _productId1 = Guid.NewGuid();
    private readonly Guid _productId2 = Guid.NewGuid();

    public F36_OfflineSyncAccountingTests()
    {
        _context = IntegrationTestDbFactory.Build();
        SeedDataAsync().GetAwaiter().GetResult();
    }

    private async Task SeedDataAsync()
    {
        // Seed Terminal
        var terminal = new Terminal
        {
            Id = _terminalId,
            TerminalCode = "POS-SYNC-1",
            Name = "Sync Terminal 1",
            IsActive = true
        };
        _context.Terminals.Add(terminal);

        // Seed Store Business Date
        if (!await _context.StoreBusinessDates.AnyAsync(b => b.StoreId == Guid.Empty && b.BusinessDate == DateTime.UtcNow.Date))
        {
            _context.StoreBusinessDates.Add(new StoreBusinessDate
            {
                StoreId = Guid.Empty,
                BusinessDate = DateTime.UtcNow.Date,
                Status = "OPEN"
            });
        }

        // Seed Accounts
        var accounts = new List<Account>
        {
            new() { AccountCode = "10100", Name = "Main Cash Register", AccountType = "ASSET", IsActive = true },
            new() { AccountCode = "10200", Name = "HDFC Current A/C", AccountType = "ASSET", IsActive = true },
            new() { AccountCode = "40100", Name = "Retail Sales Revenue", AccountType = "REVENUE", IsActive = true },
            new() { AccountCode = "22010", Name = "Output CGST Ledger", AccountType = "LIABILITY", IsActive = true },
            new() { AccountCode = "22020", Name = "Output SGST Ledger", AccountType = "LIABILITY", IsActive = true },
            new() { AccountCode = "10300", Name = "Store Inventory Asset", AccountType = "ASSET", IsActive = true },
            new() { AccountCode = "50100", Name = "Retail Cost of Goods Sold", AccountType = "EXPENSE", IsActive = true }
        };

        foreach (var acc in accounts)
        {
            if (!await _context.Accounts.AnyAsync(a => a.AccountCode == acc.AccountCode))
            {
                _context.Accounts.Add(acc);
            }
        }

        // Seed Tax Slab (0% to make calculations simple)
        var taxSlab = new TaxSlab
        {
            Id = Guid.NewGuid(),
            Name = "GST 0%",
            CgstRate = 0m,
            SgstRate = 0m,
            IgstRate = 0m,
            CessRate = 0m,
            IsDeleted = false
        };
        _context.TaxSlabs.Add(taxSlab);

        // Seed UOM
        var uom = new UnitOfMeasure
        {
            Id = Guid.NewGuid(),
            Name = "Pieces",
            Symbol = "PCS",
            IsDeleted = false
        };
        _context.UnitOfMeasures.Add(uom);

        // Seed Product 1 (With positive cost)
        var product1 = new Product
        {
            Id = _productId1,
            ProductCode = "P-SYNC-01",
            Name = "Sync Product Positive Cost",
            TaxSlabId = taxSlab.Id,
            UnitOfMeasureId = uom.Id,
            Mrp = 100m,
            SellingPrice = 100m,
            PurchasePrice = 60m, // 60 Rs cost
            IsActive = true
        };
        _context.Products.Add(product1);

        // Seed Product 2 (Zero cost)
        var product2 = new Product
        {
            Id = _productId2,
            ProductCode = "P-SYNC-02",
            Name = "Sync Product Zero Cost",
            TaxSlabId = taxSlab.Id,
            UnitOfMeasureId = uom.Id,
            Mrp = 50m,
            SellingPrice = 50m,
            PurchasePrice = 0m, // 0 Rs cost
            IsActive = true
        };
        _context.Products.Add(product2);

        // Seed barcodes
        _context.Barcodes.Add(new Barcode
        {
            Id = Guid.NewGuid(),
            ProductId = _productId1,
            BarcodeValue = "BAR-SYNC-01",
            IsPrimary = true
        });

        _context.Barcodes.Add(new Barcode
        {
            Id = Guid.NewGuid(),
            ProductId = _productId2,
            BarcodeValue = "BAR-SYNC-02",
            IsPrimary = true
        });

        await _context.SaveChangesAsync();
    }

    [Fact]
    public async Task OfflineSyncFinancialPostingCorrectnessTest()
    {
        // ── Arrange ───────────────────────────────────────────────────────────
        var businessDate = DateTime.UtcNow.Date;
        var invoiceId = Guid.NewGuid();

        // 2 items: 1 positive cost (qty=2), 1 zero cost (qty=1)
        var dto = new OfflineInvoiceDto(
            invoiceId,
            businessDate,
            "INV-SYNC-T1-01",
            _terminalId,
            1, // sequence
            _cashierId,
            SubTotal: 250m,
            DiscountAmount: 0m,
            TaxAmount: 0m,
            TotalAmount: 250m,
            RoundOff: 0m,
            NetPayable: 250m,
            PaymentMode: "Cash",
            Items: new List<OfflineInvoiceItemDto>
            {
                new(Guid.NewGuid(), _productId1, "BAR-SYNC-01", "Sync Product Positive Cost", 2, 100m, 0, 0, 0, 0, 0, 0, 0, 200m),
                new(Guid.NewGuid(), _productId2, "BAR-SYNC-02", "Sync Product Zero Cost", 1, 50m, 0, 0, 0, 0, 0, 0, 0, 50m)
            }
        );

        var syncCmd = new SyncInvoicesCommand(new List<OfflineInvoiceDto> { dto });

        var cache = new MemoryCache(new MemoryCacheOptions());
        var offerEng = new OfferEngine(_context, cache);
        var periodLock = new PeriodLockService(_context);
        var docSeq = new DocumentSequenceService(_context);
        var approval = new ApprovalWorkflowService(_context);
        var posting = new FinancialPostingService(_context, periodLock, docSeq, approval);
        var accountRes = new AccountResolutionService(_context);
        var testLogger = new TestLogger<SyncInvoicesCommandHandler>();
        var stockSvc = new StockLedgerService(_context);
        var walletSvc = new WalletService(_context);
        var loyaltySvc = new LoyaltyService(_context);

        var syncHandler = new SyncInvoicesCommandHandler(_context, offerEng, posting, accountRes, stockSvc, walletSvc, loyaltySvc, testLogger);

        // ── Act ───────────────────────────────────────────────────────────────
        var result = await syncHandler.Handle(syncCmd, CancellationToken.None);

        // ── Assert ────────────────────────────────────────────────────────────
        if (result.Synced != 1)
        {
            Assert.Fail("Sync failed: " + string.Join("; ", result.Errors));
        }

        // Retrieve generated invoice
        var invoice = await _context.Invoices.FindAsync(invoiceId, businessDate);
        Assert.NotNull(invoice);

        // Retrieve journal entry
        var journalEntry = await _context.JournalEntries
            .Include(j => j.Lines)
                .ThenInclude(l => l.Account)
            .FirstOrDefaultAsync(j => j.ReferenceDocument == $"INV-{invoiceId}");
        Assert.NotNull(journalEntry);

        // Verify dynamic GL account resolution
        // Debit: Cash Tender should resolve to leaf account "10100" (Main Cash Register)
        var cashLine = journalEntry.Lines.FirstOrDefault(l => l.Account.AccountCode == "10100");
        Assert.NotNull(cashLine);
        Assert.Equal(250m, cashLine.DebitAmount);

        // Credit: Sales Revenue should resolve to leaf account "40100"
        var salesLine = journalEntry.Lines.FirstOrDefault(l => l.Account.AccountCode == "40100");
        Assert.NotNull(salesLine);
        Assert.Equal(250m, salesLine.CreditAmount);

        // Verify COGS entries
        // Expected COGS: Product 1 (qty=2, cost=60) = 120 Rs. Product 2 cost is skipped.
        var cogsLine = journalEntry.Lines.FirstOrDefault(l => l.Account.AccountCode == "50100");
        Assert.NotNull(cogsLine);
        Assert.Equal(120m, cogsLine.DebitAmount);

        var inventoryLine = journalEntry.Lines.FirstOrDefault(l => l.Account.AccountCode == "10300");
        Assert.NotNull(inventoryLine);
        Assert.Equal(120m, inventoryLine.CreditAmount);

        // Verify double-entry balance
        Assert.Equal(journalEntry.Lines.Sum(l => l.DebitAmount), journalEntry.Lines.Sum(l => l.CreditAmount));

        // Verify warning log for zero-price item
        Assert.Contains(testLogger.Logs, log => 
            log.Level == LogLevel.Warning && 
            log.Message.Contains("Skipping COGS calculation") && 
            log.Message.Contains("Zero Cost"));
    }

    [Fact]
    public async Task Test2_OfflineSyncStockDecrementationTest()
    {
        // ── Arrange ───────────────────────────────────────────────────────────
        var businessDate = DateTime.UtcNow.Date;
        var invoiceId = Guid.NewGuid();

        // Seed Batch with Stock = 5
        var batchId = Guid.NewGuid();
        var batch = new ProductBatch
        {
            Id = batchId,
            ProductId = _productId1,
            BatchNumber = "B-SYNC-STOCK-5",
            ExpiryDate = DateTime.UtcNow.AddMonths(12),
            AvailableQuantity = 5m,
            IsActive = true
        };
        _context.ProductBatches.Add(batch);

        // Seed initial positive stock entry in StockLedger to match AvailableQuantity
        _context.StockLedger.Add(new StockLedgerEntry
        {
            Id = Guid.NewGuid(),
            StoreId = Guid.Empty,
            ProductId = _productId1,
            BatchId = batchId,
            MovementType = "GRN",
            Quantity = 5m,
            UnitCost = 60m,
            RunningBalance = 5m,
            BusinessDate = businessDate,
            ReferenceDocumentId = Guid.NewGuid(),
            ReferenceNumber = "SEED-TEST2"
        });
        await _context.SaveChangesAsync();

        var dto = new OfflineInvoiceDto(
            invoiceId,
            businessDate,
            "INV-SYNC-T1-02",
            _terminalId,
            2,
            _cashierId,
            SubTotal: 200m,
            DiscountAmount: 0m,
            TaxAmount: 0m,
            TotalAmount: 200m,
            RoundOff: 0m,
            NetPayable: 200m,
            PaymentMode: "Cash",
            Items: new List<OfflineInvoiceItemDto>
            {
                new(Guid.NewGuid(), _productId1, "BAR-SYNC-01", "Sync Product Positive Cost", 2, 100m, 0, 0, 0, 0, 0, 0, 0, 200m)
            }
        );

        var syncCmd = new SyncInvoicesCommand(new List<OfflineInvoiceDto> { dto });

        var cache = new MemoryCache(new MemoryCacheOptions());
        var offerEng = new OfferEngine(_context, cache);
        var periodLock = new PeriodLockService(_context);
        var docSeq = new DocumentSequenceService(_context);
        var approval = new ApprovalWorkflowService(_context);
        var posting = new FinancialPostingService(_context, periodLock, docSeq, approval);
        var accountRes = new AccountResolutionService(_context);
        var stockSvc = new StockLedgerService(_context);
        var testLogger = new TestLogger<SyncInvoicesCommandHandler>();
        var walletSvc = new WalletService(_context);
        var loyaltySvc = new LoyaltyService(_context);

        var syncHandler = new SyncInvoicesCommandHandler(_context, offerEng, posting, accountRes, stockSvc, walletSvc, loyaltySvc, testLogger);

        // ── Act ───────────────────────────────────────────────────────────────
        var result = await syncHandler.Handle(syncCmd, CancellationToken.None);

        // ── Assert ────────────────────────────────────────────────────────────
        Assert.Equal(1, result.Synced);
        Assert.Equal(0, result.Failed);

        // Check ProductBatch decrement: AvailableQuantity should be 5 - 2 = 3
        var freshBatch = await _context.ProductBatches.FindAsync(batchId);
        Assert.NotNull(freshBatch);
        Assert.Equal(3m, freshBatch.AvailableQuantity);

        // Check StockLedgerEntry
        var ledgerEntry = await _context.StockLedger
            .FirstOrDefaultAsync(sl => sl.ReferenceDocumentId == invoiceId && sl.MovementType == "SALE_OFFLINE_FORCED");
        Assert.NotNull(ledgerEntry);
        Assert.Equal(-2m, ledgerEntry.Quantity);
        Assert.Equal(3m, ledgerEntry.RunningBalance);
        Assert.Equal(batchId, ledgerEntry.BatchId);
    }

    [Fact]
    public async Task Test3_OfflineSyncStockDiscrepancyAlertingTest()
    {
        // ── Arrange ───────────────────────────────────────────────────────────
        var businessDate = DateTime.UtcNow.Date;
        var invoiceId = Guid.NewGuid();

        // Seed Batch with Stock = 1
        var batchId = Guid.NewGuid();
        var batch = new ProductBatch
        {
            Id = batchId,
            ProductId = _productId1,
            BatchNumber = "B-SYNC-STOCK-1",
            ExpiryDate = DateTime.UtcNow.AddMonths(12),
            AvailableQuantity = 1m,
            IsActive = true
        };
        _context.ProductBatches.Add(batch);

        _context.StockLedger.Add(new StockLedgerEntry
        {
            Id = Guid.NewGuid(),
            StoreId = Guid.Empty,
            ProductId = _productId1,
            BatchId = batchId,
            MovementType = "GRN",
            Quantity = 1m,
            UnitCost = 60m,
            RunningBalance = 1m,
            BusinessDate = businessDate,
            ReferenceDocumentId = Guid.NewGuid(),
            ReferenceNumber = "SEED-TEST3"
        });
        await _context.SaveChangesAsync();

        // Checkout 3 units (1 - 3 = -2 balance)
        var dto = new OfflineInvoiceDto(
            invoiceId,
            businessDate,
            "INV-SYNC-T1-03",
            _terminalId,
            3,
            _cashierId,
            SubTotal: 300m,
            DiscountAmount: 0m,
            TaxAmount: 0m,
            TotalAmount: 300m,
            RoundOff: 0m,
            NetPayable: 300m,
            PaymentMode: "Cash",
            Items: new List<OfflineInvoiceItemDto>
            {
                new(Guid.NewGuid(), _productId1, "BAR-SYNC-01", "Sync Product Positive Cost", 3, 100m, 0, 0, 0, 0, 0, 0, 0, 300m)
            }
        );

        var syncCmd = new SyncInvoicesCommand(new List<OfflineInvoiceDto> { dto });

        var cache = new MemoryCache(new MemoryCacheOptions());
        var offerEng = new OfferEngine(_context, cache);
        var periodLock = new PeriodLockService(_context);
        var docSeq = new DocumentSequenceService(_context);
        var approval = new ApprovalWorkflowService(_context);
        var posting = new FinancialPostingService(_context, periodLock, docSeq, approval);
        var accountRes = new AccountResolutionService(_context);
        var stockSvc = new StockLedgerService(_context);
        var testLogger = new TestLogger<SyncInvoicesCommandHandler>();
        var walletSvc = new WalletService(_context);
        var loyaltySvc = new LoyaltyService(_context);

        var syncHandler = new SyncInvoicesCommandHandler(_context, offerEng, posting, accountRes, stockSvc, walletSvc, loyaltySvc, testLogger);

        // ── Act ───────────────────────────────────────────────────────────────
        var result = await syncHandler.Handle(syncCmd, CancellationToken.None);

        // ── Assert ────────────────────────────────────────────────────────────
        Assert.Equal(1, result.Synced);
        Assert.Equal(0, result.Failed);

        // Check ProductBatch goes negative to -2
        var freshBatch = await _context.ProductBatches.FindAsync(batchId);
        Assert.NotNull(freshBatch);
        Assert.Equal(-2m, freshBatch.AvailableQuantity);

        // Check StockLedgerEntry
        var ledgerEntry = await _context.StockLedger
            .FirstOrDefaultAsync(sl => sl.ReferenceDocumentId == invoiceId && sl.MovementType == "SALE_OFFLINE_FORCED");
        Assert.NotNull(ledgerEntry);
        Assert.Equal(-3m, ledgerEntry.Quantity);
        Assert.Equal(-2m, ledgerEntry.RunningBalance);

        // Verify Error-level STOCK_DISCREPANCY warning
        Assert.Contains(testLogger.Logs, log => 
            log.Level == LogLevel.Error && 
            log.Message.Contains("STOCK_DISCREPANCY") && 
            log.Message.Contains("SALE_OFFLINE_FORCED") &&
            log.Message.Contains("-2"));
    }

    [Fact]
    public async Task Test4_CancelSyncedOfflineInvoiceStockRestorationTest()
    {
        // ── Arrange ───────────────────────────────────────────────────────────
        var businessDate = DateTime.UtcNow.Date;
        var invoiceId = Guid.NewGuid();

        // Seed Batch with Stock = 5
        var batchId = Guid.NewGuid();
        var batch = new ProductBatch
        {
            Id = batchId,
            ProductId = _productId1,
            BatchNumber = "B-SYNC-STOCK-CANCEL",
            ExpiryDate = DateTime.UtcNow.AddMonths(12),
            AvailableQuantity = 5m,
            IsActive = true
        };
        _context.ProductBatches.Add(batch);

        _context.StockLedger.Add(new StockLedgerEntry
        {
            Id = Guid.NewGuid(),
            StoreId = Guid.Empty,
            ProductId = _productId1,
            BatchId = batchId,
            MovementType = "GRN",
            Quantity = 5m,
            UnitCost = 60m,
            RunningBalance = 5m,
            BusinessDate = businessDate,
            ReferenceDocumentId = Guid.NewGuid(),
            ReferenceNumber = "SEED-TEST4"
        });
        await _context.SaveChangesAsync();

        var dto = new OfflineInvoiceDto(
            invoiceId,
            businessDate,
            "INV-SYNC-T1-04",
            _terminalId,
            4,
            _cashierId,
            SubTotal: 200m,
            DiscountAmount: 0m,
            TaxAmount: 0m,
            TotalAmount: 200m,
            RoundOff: 0m,
            NetPayable: 200m,
            PaymentMode: "Cash",
            Items: new List<OfflineInvoiceItemDto>
            {
                new(Guid.NewGuid(), _productId1, "BAR-SYNC-01", "Sync Product Positive Cost", 2, 100m, 0, 0, 0, 0, 0, 0, 0, 200m)
            }
        );

        var syncCmd = new SyncInvoicesCommand(new List<OfflineInvoiceDto> { dto });

        var cache = new MemoryCache(new MemoryCacheOptions());
        var offerEng = new OfferEngine(_context, cache);
        var periodLock = new PeriodLockService(_context);
        var docSeq = new DocumentSequenceService(_context);
        var approval = new ApprovalWorkflowService(_context);
        var posting = new FinancialPostingService(_context, periodLock, docSeq, approval);
        var accountRes = new AccountResolutionService(_context);
        var stockSvc = new StockLedgerService(_context);
        var walletSvc = new WalletService(_context);
        var loyaltySvc = new LoyaltyService(_context);

        var syncHandler = new SyncInvoicesCommandHandler(_context, offerEng, posting, accountRes, stockSvc, walletSvc, loyaltySvc, logger: null);

        // Sync it first
        await syncHandler.Handle(syncCmd, CancellationToken.None);

        // Cancel it using CancelInvoiceCommand
        var cancelCmd = new CancelInvoiceCommand(invoiceId, _cashierId);
        var cancelHandler = new CancelInvoiceCommandHandler(_context, posting, stockSvc, walletSvc, loyaltySvc, periodLock);

        // ── Act ───────────────────────────────────────────────────────────────
        var cancelResult = await cancelHandler.Handle(cancelCmd, CancellationToken.None);

        // ── Assert ────────────────────────────────────────────────────────────
        Assert.True(cancelResult);

        // Assert that the batch available quantity is restored to 5 (from 3)
        var freshBatch = await _context.ProductBatches.FindAsync(batchId);
        Assert.NotNull(freshBatch);
        Assert.Equal(5m, freshBatch.AvailableQuantity);

        // Assert that a "SALE_CANCEL" ledger entry exists
        var cancelLedger = await _context.StockLedger
            .FirstOrDefaultAsync(sl => sl.ReferenceDocumentId == invoiceId && sl.MovementType == "SALE_CANCEL");
        Assert.NotNull(cancelLedger);
        Assert.Equal(2m, cancelLedger.Quantity); // Positive to restore
        Assert.Equal(5m, cancelLedger.RunningBalance);
    }

    public void Dispose() => _context.Dispose();
}

public class TestLogger<T> : ILogger<T>
{
    public List<(LogLevel Level, string Message)> Logs { get; } = new();

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        var msg = formatter(state, exception);
        Logs.Add((logLevel, msg));
    }
}
