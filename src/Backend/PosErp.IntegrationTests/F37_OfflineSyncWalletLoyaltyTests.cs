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
using PosErp.Domain.Entities.Crm;
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
public class F37_OfflineSyncWalletLoyaltyTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Guid _terminalId = Guid.NewGuid();
    private readonly Guid _cashierId = Guid.NewGuid();
    private readonly Guid _productId1 = Guid.NewGuid();
    private readonly Guid _customerId = Guid.NewGuid();

    public F37_OfflineSyncWalletLoyaltyTests()
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
            TerminalCode = "POS-SYNC-37",
            Name = "Sync Terminal 37",
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
            new() { AccountCode = "50100", Name = "Retail Cost of Goods Sold", AccountType = "EXPENSE", IsActive = true },
            new() { AccountCode = "20200", Name = "Customer Wallet Liabilities", AccountType = "LIABILITY", IsActive = true },
            new() { AccountCode = "20300", Name = "Loyalty Points Liabilities", AccountType = "LIABILITY", IsActive = true }
        };

        foreach (var acc in accounts)
        {
            if (!await _context.Accounts.AnyAsync(a => a.AccountCode == acc.AccountCode))
            {
                _context.Accounts.Add(acc);
            }
        }

        // Seed Customer
        var customer = new Customer
        {
            Id = _customerId,
            Name = "Alice Sync-Test",
            Phone = "9900990099",
            MembershipStatus = "Active",
            RunningWalletBalance = 200m,
            RunningLoyaltyPoints = 1000m,
            LifetimeSpend = 1000m
        };
        _context.Customers.Add(customer);

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

        // Seed Product
        var product = new Product
        {
            Id = _productId1,
            ProductCode = "P-SYNC-37",
            Name = "Sync Product 37",
            TaxSlabId = taxSlab.Id,
            UnitOfMeasureId = uom.Id,
            Mrp = 100m,
            SellingPrice = 100m,
            PurchasePrice = 60m,
            IsActive = true
        };
        _context.Products.Add(product);

        // Seed barcode
        _context.Barcodes.Add(new Barcode
        {
            Id = Guid.NewGuid(),
            ProductId = _productId1,
            BarcodeValue = "BAR-SYNC-37",
            IsPrimary = true
        });

        // Seed Loyalty config (10 points = 1 Rs discount)
        var config = await _context.LoyaltyProgramConfigs.FirstOrDefaultAsync();
        if (config == null)
        {
            _context.LoyaltyProgramConfigs.Add(new LoyaltyProgramConfig
            {
                EarnRatioSpendAmount = 100m,
                EarnRatioPoints = 10m, // 10 points per ₹100 spent
                RedeemRatioPoints = 10m, // 10 points = ₹1 discount
                RedeemRatioDiscountAmount = 1m,
                MaxRedemptionPerDay = 500m,
                EnableAutoTierEvaluation = false
            });
        }
        else
        {
            config.EarnRatioSpendAmount = 100m;
            config.EarnRatioPoints = 10m;
            config.RedeemRatioPoints = 10m;
            config.RedeemRatioDiscountAmount = 1m;
            config.MaxRedemptionPerDay = 500m;
        }

        await _context.SaveChangesAsync();
    }

    [Fact]
    public async Task Test1_SplitPaymentJournalPostingsTest()
    {
        // ── Arrange ───────────────────────────────────────────────────────────
        var businessDate = DateTime.UtcNow.Date;
        var invoiceId = Guid.NewGuid();

        // 10 units at ₹100 = ₹1000 SubTotal
        // Redeem 1000 points = ₹100 pointsDiscount
        // NetPayable = ₹900
        // Tenders split: Cash = 100, UPI = 500, Card = 0, Wallet = 300, PointsRedeemed = 1000
        // Total debits = Cash (100) + Bank/UPI (500) + Wallet (300) + Loyalty (100) = ₹1000
        // Total credits = Sales (1000) + Tax (0) = ₹1000
        var dto = new OfflineInvoiceDto(
            Id: invoiceId,
            BusinessDate: businessDate,
            InvoiceNumber: "INV-SYNC-37-01",
            TerminalId: _terminalId,
            TerminalSequence: 1,
            CashierId: _cashierId,
            SubTotal: 1000m,
            DiscountAmount: 0m,
            TaxAmount: 0m,
            TotalAmount: 1000m,
            RoundOff: 0m,
            NetPayable: 900m,
            PaymentMode: "SPLIT",
            CustomerId: _customerId,
            CustomerName: "Alice Sync-Test",
            CustomerPhone: "9900990099",
            CashAmount: 100m,
            UpiAmount: 500m,
            CardAmount: 0m,
            WalletAmountUsed: 300m,
            PointsRedeemed: 1000m,
            LoyaltyPointsEarned: 90,
            LoyaltyPointsBalance: 90,
            Items: new List<OfflineInvoiceItemDto>
            {
                new(Guid.NewGuid(), _productId1, "BAR-SYNC-37", "Sync Product 37", 10, 100m, 0, 0, 0, 0, 0, 0, 0, 1000m)
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
        var testLogger = new TestLogger<SyncInvoicesCommandHandler>();

        var syncHandler = new SyncInvoicesCommandHandler(_context, offerEng, posting, accountRes, stockSvc, walletSvc, loyaltySvc, testLogger);

        // ── Act ───────────────────────────────────────────────────────────────
        var result = await syncHandler.Handle(syncCmd, CancellationToken.None);

        // ── Assert ────────────────────────────────────────────────────────────
        if (result.Synced != 1)
        {
            Assert.Fail("Sync failed: " + string.Join("; ", result.Errors));
        }

        // Check mapping of CustomerId on synced invoice
        var freshInvoice = await _context.Invoices.FindAsync(invoiceId, businessDate);
        Assert.NotNull(freshInvoice);
        Assert.Equal(_customerId, freshInvoice.CustomerId);
        Assert.Equal(100m, freshInvoice.CashAmount);
        Assert.Equal(500m, freshInvoice.UpiAmount);
        Assert.Equal(300m, freshInvoice.WalletAmount);

        // Fetch journal entries
        var journalEntry = await _context.JournalEntries
            .Include(j => j.Lines)
                .ThenInclude(l => l.Account)
            .FirstOrDefaultAsync(j => j.ReferenceDocument == $"INV-{invoiceId}");
        Assert.NotNull(journalEntry);

        // Verify split debits
        var cashLine = journalEntry.Lines.FirstOrDefault(l => l.Account?.AccountCode == "10100");
        var upiLine = journalEntry.Lines.FirstOrDefault(l => l.Account?.AccountCode == "10000");
        if (cashLine == null || upiLine == null)
        {
            var linesDetail = string.Join(", ", journalEntry.Lines.Select(l => $"{l.Account?.AccountCode ?? "null"}(dr={l.DebitAmount},cr={l.CreditAmount})"));
            Assert.Fail($"Tender lines missing. Actual lines: {linesDetail}");
        }
        Assert.Equal(100m, cashLine.DebitAmount);
        Assert.Equal(500m, upiLine.DebitAmount);

        var walletLine = journalEntry.Lines.FirstOrDefault(l => l.Account?.AccountCode == "20200");
        Assert.NotNull(walletLine);
        Assert.Equal(300m, walletLine.DebitAmount);

        var loyaltyLine = journalEntry.Lines.FirstOrDefault(l => l.Account?.AccountCode == "20300");
        Assert.NotNull(loyaltyLine);
        Assert.Equal(100m, loyaltyLine.DebitAmount);

        // Verify double-entry equality
        Assert.Equal(journalEntry.Lines.Sum(l => l.DebitAmount), journalEntry.Lines.Sum(l => l.CreditAmount));
    }

    [Fact]
    public async Task Test2_WalletSpendSyncAndDiscrepancyTest()
    {
        // ── Arrange ───────────────────────────────────────────────────────────
        var businessDate = DateTime.UtcNow.Date;
        var invoiceId = Guid.NewGuid();

        // Customer has Wallet balance = ₹200. Spend ₹500 (balance will go to -₹300)
        var dto = new OfflineInvoiceDto(
            Id: invoiceId,
            BusinessDate: businessDate,
            InvoiceNumber: "INV-SYNC-37-02",
            TerminalId: _terminalId,
            TerminalSequence: 2,
            CashierId: _cashierId,
            SubTotal: 500m,
            DiscountAmount: 0m,
            TaxAmount: 0m,
            TotalAmount: 500m,
            RoundOff: 0m,
            NetPayable: 0m, // Paid fully via wallet
            PaymentMode: "SPLIT",
            CustomerId: _customerId,
            CustomerName: "Alice Sync-Test",
            CustomerPhone: "9900990099",
            CashAmount: 0m,
            UpiAmount: 0m,
            CardAmount: 0m,
            WalletAmountUsed: 500m,
            PointsRedeemed: 0m,
            LoyaltyPointsEarned: 0,
            LoyaltyPointsBalance: 0,
            Items: new List<OfflineInvoiceItemDto>
            {
                new(Guid.NewGuid(), _productId1, "BAR-SYNC-37", "Sync Product 37", 5, 100m, 0, 0, 0, 0, 0, 0, 0, 500m)
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
        var testLogger = new TestLogger<SyncInvoicesCommandHandler>();

        // Re-set wallet balance to 200 before sync
        var customer = await _context.Customers.FindAsync(_customerId);
        if (customer != null)
        {
            customer.RunningWalletBalance = 200m;
            await _context.SaveChangesAsync();
        }

        var syncHandler = new SyncInvoicesCommandHandler(_context, offerEng, posting, accountRes, stockSvc, walletSvc, loyaltySvc, testLogger);

        // ── Act ───────────────────────────────────────────────────────────────
        var result = await syncHandler.Handle(syncCmd, CancellationToken.None);

        // ── Assert ────────────────────────────────────────────────────────────
        if (result.Synced != 1)
        {
            Assert.Fail("Sync failed: " + string.Join("; ", result.Errors));
        }

        // Verify customer wallet balance is deducted to -300
        var freshCustomer = await _context.Customers.FindAsync(_customerId);
        Assert.NotNull(freshCustomer);
        Assert.Equal(-300m, freshCustomer.RunningWalletBalance);

        // Verify ledger entry
        var walletLedger = await _context.WalletLedger
            .FirstOrDefaultAsync(w => w.ReferenceDocument == "INV-SYNC-37-02");
        Assert.NotNull(walletLedger);
        Assert.Equal(-500m, walletLedger.Amount);
        Assert.Equal(-300m, walletLedger.RunningBalance);

        // Verify WALLET_DISCREPANCY warning log
        Assert.Contains(testLogger.Logs, log => 
            log.Level == LogLevel.Warning && 
            log.Message.Contains("WALLET_DISCREPANCY") && 
            log.Message.Contains("-300"));
    }

    [Fact]
    public async Task Test3_LoyaltySyncLimitBreachAndDriftTest()
    {
        // ── Arrange ───────────────────────────────────────────────────────────
        var businessDate = DateTime.UtcNow.Date;
        var invoiceId = Guid.NewGuid();

        // Customer has 1000 points. Redeem 800 (Limit is 500 per day).
        // Cash paid NetPayable = ₹1000
        // Base points earned = (₹1000 / 100) * 10 = 100 points
        // Client estimated earned points = 5 points (forces a loyalty drift)
        var dto = new OfflineInvoiceDto(
            Id: invoiceId,
            BusinessDate: businessDate,
            InvoiceNumber: "INV-SYNC-37-03",
            TerminalId: _terminalId,
            TerminalSequence: 3,
            CashierId: _cashierId,
            SubTotal: 1000m,
            DiscountAmount: 0m,
            TaxAmount: 0m,
            TotalAmount: 1000m,
            RoundOff: 0m,
            NetPayable: 920m,
            PaymentMode: "Cash",
            CustomerId: _customerId,
            CustomerName: "Alice Sync-Test",
            CustomerPhone: "9900990099",
            CashAmount: 920m,
            UpiAmount: 0m,
            CardAmount: 0m,
            WalletAmountUsed: 0m,
            PointsRedeemed: 800m,
            LoyaltyPointsEarned: 5, // Estimated differs from re-calculated 100
            LoyaltyPointsBalance: 200,
            Items: new List<OfflineInvoiceItemDto>
            {
                new(Guid.NewGuid(), _productId1, "BAR-SYNC-37", "Sync Product 37", 10, 100m, 0, 0, 0, 0, 0, 0, 0, 1000m)
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
        var testLogger = new TestLogger<SyncInvoicesCommandHandler>();

        // Re-set loyalty balance to 1000 before sync
        var customer = await _context.Customers.FindAsync(_customerId);
        if (customer != null)
        {
            customer.RunningLoyaltyPoints = 1000m;
            await _context.SaveChangesAsync();
        }

        var syncHandler = new SyncInvoicesCommandHandler(_context, offerEng, posting, accountRes, stockSvc, walletSvc, loyaltySvc, testLogger);

        // ── Act ───────────────────────────────────────────────────────────────
        var result = await syncHandler.Handle(syncCmd, CancellationToken.None);

        // ── Assert ────────────────────────────────────────────────────────────
        if (result.Synced != 1)
        {
            Assert.Fail("Sync failed: " + string.Join("; ", result.Errors));
        }

        // Verify customer points balance is: 1000 - 800 + 92 (earned) = 292
        var freshCustomer = await _context.Customers.FindAsync(_customerId);
        Assert.NotNull(freshCustomer);
        Assert.Equal(292m, freshCustomer.RunningLoyaltyPoints);

        // Verify LOYALTY_LIMIT_BREACH warning log
        Assert.Contains(testLogger.Logs, log => 
            log.Level == LogLevel.Warning && 
            log.Message.Contains("LOYALTY_LIMIT_BREACH") && 
            log.Message.Contains("800"));

        // Verify LOYALTY_DRIFT warning log
        Assert.Contains(testLogger.Logs, log => 
            log.Level == LogLevel.Warning && 
            log.Message.Contains("LOYALTY_DRIFT") && 
            log.Message.Contains("5") && 
            log.Message.Contains("92"));
    }

    public void Dispose() => _context.Dispose();
}
