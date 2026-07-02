using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using PosErp.Application.Features.Crm.Services;
using PosErp.Application.Features.Finance.Services;
using PosErp.Application.Features.Inventory.Services;
using PosErp.Application.Features.Offers.Services;
using PosErp.Application.Features.Pos.Commands.SyncInvoices;
using PosErp.Domain.Entities.Auth;
using PosErp.Domain.Entities.Catalog;
using PosErp.Domain.Entities.Crm;
using PosErp.Domain.Entities.Inventory;
using PosErp.Domain.Entities.Pos;
using PosErp.Infrastructure.Identity;
using PosErp.Infrastructure.Persistence;
using Xunit;

namespace PosErp.IntegrationTests;

/// <summary>
/// F03 — Gap #2: Loyalty points earned must use the cash paid basis
/// (NetPayable - pointsDiscountValue) to prevent circular points inflation.
///
/// Financial correctness assertion: total Debits == total Credits.
/// </summary>
[Collection("Database Collection")]
public class F03_LoyaltyPointTests : IDisposable
{
    static F03_LoyaltyPointTests()
    {
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
    }

    private readonly ApplicationDbContext _context;
    private readonly CreateInvoiceCommandHandler _invoiceHandler;

    private readonly Guid _terminalId  = Guid.NewGuid();
    private readonly Guid _cashierId   = Guid.NewGuid();
    private readonly Guid _customerId  = Guid.NewGuid();
    private readonly Guid _productId   = Guid.NewGuid();
    private readonly Guid _taxSlabId   = Guid.NewGuid();

    public F03_LoyaltyPointTests()
    {
        _context = IntegrationTestDbFactory.Build();

        var cache      = new MemoryCache(new MemoryCacheOptions());
        var hasher     = new PasswordHasher();
        var periodLock = new PeriodLockService(_context);
        var docSeq     = new DocumentSequenceService(_context);
        var approval   = new ApprovalWorkflowService(_context);
        var posting    = new FinancialPostingService(_context, periodLock, docSeq, approval);
        var stockSvc   = new StockLedgerService(_context);
        var walletSvc  = new WalletService(_context);
        var loyaltySvc = new LoyaltyService(_context);
        var offerEng   = new OfferEngine(_context, cache);

        var accountRes = new PosErp.Infrastructure.Services.AccountResolutionService(_context);

        _invoiceHandler = new CreateInvoiceCommandHandler(
            _context, offerEng, walletSvc, loyaltySvc, posting, stockSvc, hasher, accountRes);

        SeedAsync().GetAwaiter().GetResult();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test: loyalty points calculation uses cash-paid basis (NetPayable - discount)
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task LoyaltyEarnedPointsUseCashPaidBasisTest()
    {
        // ── Arrange ───────────────────────────────────────────────────────────
        // Points config:
        // - Earn: 1 point per 10 Rs spent (config set below).
        // - Redeem: 100 points = 10 Rs discount.
        // Cash paid basis = NetPayable = 490 Rs.
        // Expected earned points = floor(490 / 10) = 49 points.
        var cmd = new CreateInvoiceCommand(
            InvoiceNumber:        $"INV-F03-{Guid.NewGuid():N}",
            TerminalId:           _terminalId,
            CashierId:            _cashierId,
            CustomerId:           _customerId,
            PromoCode:            null,
            WalletAmountUsed:     0m,
            CashAmount:           490m,         // Cash actually paid
            UpiAmount:            0m,
            CardAmount:           0m,
            RoundOff:             0m,
            NetPayable:           490m,         // NetPayable post-points-redemption (actual cash paid)
            PaymentMode:          "CASH",
            Items: new List<InvoiceItemDto>
            {
                new(_productId, Quantity: 1, UnitPrice: 500m, BatchId: null)
            },
            PointsRedeemed:       100,          // Redeeming 100 points (₹10 discount)
            SupervisorOverridePin: null
        );

        // ── Act ───────────────────────────────────────────────────────────────
        var invoiceId = (await _invoiceHandler.Handle(cmd, CancellationToken.None)).InvoiceId;

        // ── Assert ────────────────────────────────────────────────────────────
        // 1) Verify customer's running points
        var customer = await _context.Customers.FindAsync(_customerId);
        Assert.NotNull(customer);
        // Starting balance: 1000. Burned: 100. Earned: 49 (from ₹490 cash paid).
        // Final balance must be 949, NOT 948 (which would be a double-subtraction of points discount).
        Assert.Equal(949m, customer!.RunningLoyaltyPoints);

        // 2) Verify ledger entries
        var entries = await _context.LoyaltyLedger
            .Where(l => l.CustomerId == _customerId && l.InvoiceId == invoiceId)
            .OrderBy(l => l.CreatedAt)
            .ToListAsync();

        // Must be exactly one BURN ("Redeem Points") and one EARN ("Earn Points") entry
        Assert.Equal(2, entries.Count);

        var burn = entries.FirstOrDefault(e => e.TransactionType == "Redeem Points");
        Assert.NotNull(burn);
        Assert.Equal(100m, burn!.PointsRedeemed);
        Assert.Equal(0m, burn.PointsEarned);
        Assert.Equal(-100m, burn.Points);

        var earn = entries.FirstOrDefault(e => e.TransactionType == "Earn Points");
        Assert.NotNull(earn);
        Assert.Equal(0m, earn!.PointsRedeemed);
        Assert.Equal(49m, earn.PointsEarned);
        Assert.Equal(49m, earn.Points);

        // 3) Verify double-entry GL journal posting balances
        var refDoc = $"INV-{invoiceId}";
        var je = await _context.JournalEntries
            .Include(j => j.Lines).ThenInclude(l => l.Account)
            .FirstOrDefaultAsync(j => j.ReferenceDocument == refDoc);

        Assert.NotNull(je);
        Assert.True(je!.Lines.Count >= 2);
        decimal totalDebits  = je.Lines.Sum(l => l.DebitAmount);
        decimal totalCredits = je.Lines.Sum(l => l.CreditAmount);
        Assert.Equal(totalDebits, totalCredits);

        var linesSummary = string.Join(", ", je.Lines.Select(l => $"{l.Account?.AccountCode}:Dr{l.DebitAmount}:Cr{l.CreditAmount}"));

        // Dynamically resolve expected account codes to match DB Chart of Accounts
        var cashAcc = await _context.Accounts
            .Where(a => a.IsActive && a.AccountType == "ASSET" && a.Name.Contains("Cash"))
            .OrderByDescending(a => a.AccountCode.Length)
            .ThenBy(a => a.AccountCode)
            .Select(a => a.AccountCode)
            .FirstOrDefaultAsync() ?? "10100";

        var loyaltyPointsAcc = await _context.Accounts
            .Where(a => a.IsActive && a.AccountType == "LIABILITY" && a.Name.Contains("Loyalty Points"))
            .OrderByDescending(a => a.AccountCode.Length)
            .ThenBy(a => a.AccountCode)
            .Select(a => a.AccountCode)
            .FirstOrDefaultAsync() ?? "20300";

        // Cash Tender line: Debit ₹490
        var cashDebit = je.Lines.FirstOrDefault(l => l.Account?.AccountCode == cashAcc);
        Assert.True(cashDebit != null, $"Expected cash debit line on {cashAcc}. Journal lines: {linesSummary}");
        Assert.Equal(490m, cashDebit!.DebitAmount);

        // Points Redemption line: Debit ₹10 to 20300 (Loyalty Points Liability)
        var pointsDebit = je.Lines.FirstOrDefault(l => l.Account?.AccountCode == loyaltyPointsAcc);
        Assert.True(pointsDebit != null, $"Expected points debit line on {loyaltyPointsAcc}. Journal lines: {linesSummary}");
        Assert.Equal(10m, pointsDebit!.DebitAmount);
    }

    [Fact]
    public async Task LoyaltyEarnedPointsHandlesUnevenPointsRedemptionRatioTest()
    {
        // ── Arrange ───────────────────────────────────────────────────────────
        // RedeemRatioPoints = 30, RedeemRatioDiscountAmount = 1 (i.e. 30 points = ₹1)
        // Customer redeems 100 points → discount value = (100 / 30) * 1 = ₹3.3333...
        // Total bill total = ₹200.
        // NetPayable (already rounded by frontend to ₹196.67).
        // Cash paid basis = NetPayable = 196.67.
        // Expected earned points = floor(196.67 / 10) = 19 points.
        // Expected discount posted to GL (rounded to 2 decimals) = 3.33.
        
        var loyaltyConfig = await _context.LoyaltyProgramConfigs.FirstOrDefaultAsync();
        if (loyaltyConfig != null)
        {
            loyaltyConfig.RedeemRatioPoints = 30m;
            loyaltyConfig.RedeemRatioDiscountAmount = 1m;
            await _context.SaveChangesAsync();
        }

        var cmd = new CreateInvoiceCommand(
            InvoiceNumber:        $"INV-F03-UNEVEN-{Guid.NewGuid():N}",
            TerminalId:           _terminalId,
            CashierId:            _cashierId,
            CustomerId:           _customerId,
            PromoCode:            null,
            WalletAmountUsed:     0m,
            CashAmount:           196.67m,      // Cash paid matching NetPayable
            UpiAmount:            0m,
            CardAmount:           0m,
            RoundOff:             0m,
            NetPayable:           196.67m,      // NetPayable (post points discount)
            PaymentMode:          "CASH",
            Items: new List<InvoiceItemDto>
            {
                new(_productId, Quantity: 1, UnitPrice: 200m, BatchId: null)
            },
            PointsRedeemed:       100,          // Redeeming 100 points
            SupervisorOverridePin: null
        );

        // ── Act ───────────────────────────────────────────────────────────────
        var invoiceId = (await _invoiceHandler.Handle(cmd, CancellationToken.None)).InvoiceId;

        // ── Assert ────────────────────────────────────────────────────────────
        var customer = await _context.Customers.FindAsync(_customerId);
        Assert.NotNull(customer);
        // Starting: 1000. Burned: 100. Earned: 19. Final: 919.
        Assert.Equal(919m, customer!.RunningLoyaltyPoints);

        var refDoc = $"INV-{invoiceId}";
        var je = await _context.JournalEntries
            .Include(j => j.Lines).ThenInclude(l => l.Account)
            .FirstOrDefaultAsync(j => j.ReferenceDocument == refDoc);

        Assert.NotNull(je);
        
        decimal totalDebits  = je!.Lines.Sum(l => l.DebitAmount);
        decimal totalCredits = je.Lines.Sum(l => l.CreditAmount);
        Assert.Equal(totalDebits, totalCredits);

        var loyaltyPointsAcc = await _context.Accounts
            .Where(a => a.IsActive && a.AccountType == "LIABILITY" && a.Name.Contains("Loyalty Points"))
            .OrderByDescending(a => a.AccountCode.Length)
            .ThenBy(a => a.AccountCode)
            .Select(a => a.AccountCode)
            .FirstOrDefaultAsync() ?? "20300";

        var pointsDebit = je.Lines.FirstOrDefault(l => l.Account?.AccountCode == loyaltyPointsAcc);
        Assert.NotNull(pointsDebit);
        // Assert points discount was not truncated to 0 or 3: it must be exactly ₹3.33
        Assert.Equal(3.33m, pointsDebit!.DebitAmount);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Seed
    // ─────────────────────────────────────────────────────────────────────────
    private async Task SeedAsync()
    {
        // Global store setup
        var store = await _context.Stores.FindAsync(Guid.Empty);
        if (store == null)
        {
            _context.Stores.Add(new Store
            {
                Id = Guid.Empty, StoreCode = "GLOBAL",
                StoreName = "Global Store", IsActive = true
            });
        }
        else if (!store.IsActive)
        {
            store.IsActive = true;
        }

        // Terminal
        _context.Terminals.Add(new Terminal
        {
            Id           = _terminalId,
            Name         = "F03-Terminal",
            TerminalCode = $"F03T{_terminalId:N}"[..8],
            IsActive     = true
        });

        // Role + Cashier
        var role = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Cashier")
                   ?? new Role { Id = Guid.NewGuid(), Name = "Cashier", Description = "Cashier" };
        if (_context.Entry(role).State == EntityState.Detached)
            _context.Roles.Add(role);

        _context.Users.Add(new User
        {
            Id           = _cashierId,
            Username     = $"f03-{_cashierId:N}@test",
            PasswordHash = new PasswordHasher().HashPassword("Test@1234"),
            FullName     = "F03 Cashier",
            RoleId       = role.Id,
            IsActive     = true
        });

        // Customer with 1000 starting points
        _context.Customers.Add(new Customer
        {
            Id                    = _customerId,
            Name                  = "F03 Loyalty Customer",
            Phone                 = $"9{_customerId.ToString("N")[..9]}",
            RunningLoyaltyPoints  = 1000m,
            LifetimePointsEarned  = 1000m,
            CreditLimit           = 0m
        });

        // Loyalty Config:
        // Earn: 1 pt per ₹10 spend.
        // Redeem: 100 pts = ₹10 discount.
        var loyaltyConfig = await _context.LoyaltyProgramConfigs.FirstOrDefaultAsync();
        if (loyaltyConfig == null)
        {
            loyaltyConfig = new LoyaltyProgramConfig();
            _context.LoyaltyProgramConfigs.Add(loyaltyConfig);
        }
        loyaltyConfig.EarnRatioSpendAmount = 10m;
        loyaltyConfig.EarnRatioPoints = 1m;
        loyaltyConfig.RedeemRatioPoints = 100m;
        loyaltyConfig.RedeemRatioDiscountAmount = 10m;
        loyaltyConfig.MaxRedemptionPercentagePerInvoice = 50m; // allow up to 50% discount

        // Tax slab (0% tax for simple math in test)
        var taxSlab = new TaxSlab
        {
            Id = _taxSlabId, Name = "Zero Tax",
            CgstRate = 0m, SgstRate = 0m, IgstRate = 0m, CessRate = 0m
        };
        _context.TaxSlabs.Add(taxSlab);

        // UoM
        var uomId = await _context.UnitOfMeasures.Select(u => u.Id).FirstOrDefaultAsync();

        // Product
        _context.Products.Add(new Product
        {
            Id              = _productId,
            ProductCode     = $"F03{_productId:N}"[..10],
            Name            = "F03 Test Product",
            TaxSlabId       = taxSlab.Id,
            UnitOfMeasureId = uomId,
            Mrp             = 500m,
            SellingPrice    = 500m,
            PurchasePrice   = 300m,
            IsActive        = true,
            HasExpiry       = false
        });

        // Stock
        _context.StockLedger.Add(new StockLedgerEntry
        {
            StoreId             = Guid.Empty,
            ProductId           = _productId,
            MovementType        = "OPENING",
            Quantity            = 100m,
            UnitCost            = 300m,
            RunningBalance      = 100m,
            BusinessDate        = DateTime.UtcNow.Date,
            ReferenceDocumentId = Guid.NewGuid(),
            ReferenceNumber     = "OPENING-F03"
        });

        // Open business date
        if (!await _context.StoreBusinessDates.AnyAsync(
                b => b.StoreId == Guid.Empty && b.BusinessDate == DateTime.UtcNow.Date))
        {
            _context.StoreBusinessDates.Add(new StoreBusinessDate
            {
                StoreId      = Guid.Empty,
                BusinessDate = DateTime.UtcNow.Date,
                Status       = "OPEN"
            });
        }

        await _context.SaveChangesAsync();
    }

    [Fact]
    public async Task LoyaltyDailyRedemptionLimitBoundaryInclusivePassTest()
    {
        // ── Arrange ───────────────────────────────────────────────────────────
        var loyaltyConfig = await _context.LoyaltyProgramConfigs.FirstOrDefaultAsync();
        if (loyaltyConfig != null)
        {
            loyaltyConfig.MaxRedemptionPerDay = 500m;
            await _context.SaveChangesAsync();
        }

        // Setup customer points balance to cover redemption
        var customer = await _context.Customers.FindAsync(_customerId);
        customer!.RunningLoyaltyPoints = 1000m;
        await _context.SaveChangesAsync();

        var cmd1 = new CreateInvoiceCommand(
            InvoiceNumber:        $"INV-INC-1-{Guid.NewGuid():N}",
            TerminalId:           _terminalId,
            CashierId:            _cashierId,
            CustomerId:           _customerId,
            PromoCode:            null,
            WalletAmountUsed:     0m,
            CashAmount:           470m,
            UpiAmount:            0m,
            CardAmount:           0m,
            RoundOff:             0m,
            NetPayable:           470m,
            PaymentMode:          "CASH",
            Items: new List<InvoiceItemDto> { new(_productId, 1, 500m, null) },
            PointsRedeemed:       300,
            SupervisorOverridePin: null
        );

        var cmd2 = new CreateInvoiceCommand(
            InvoiceNumber:        $"INV-INC-2-{Guid.NewGuid():N}",
            TerminalId:           _terminalId,
            CashierId:            _cashierId,
            CustomerId:           _customerId,
            PromoCode:            null,
            WalletAmountUsed:     0m,
            CashAmount:           480m,
            UpiAmount:            0m,
            CardAmount:           0m,
            RoundOff:             0m,
            NetPayable:           480m,
            PaymentMode:          "CASH",
            Items: new List<InvoiceItemDto> { new(_productId, 1, 500m, null) },
            PointsRedeemed:       200, // brings cumulative to exactly 500
            SupervisorOverridePin: null
        );

        // ── Act ───────────────────────────────────────────────────────────────
        var id1 = await _invoiceHandler.Handle(cmd1, CancellationToken.None);
        var id2 = await _invoiceHandler.Handle(cmd2, CancellationToken.None);

        // ── Assert ────────────────────────────────────────────────────────────
        var finalCustomer = await _context.Customers.FindAsync(_customerId);
        // Start: 1000. Burned: 500. Earned: (470/10) + (480/10) = 47 + 48 = 95. Final: 1000 - 500 + 95 = 595.
        Assert.Equal(595m, finalCustomer!.RunningLoyaltyPoints);
    }

    [Fact]
    public async Task LoyaltyDailyRedemptionLimitBoundaryExclusiveFailTest()
    {
        // ── Arrange ───────────────────────────────────────────────────────────
        var loyaltyConfig = await _context.LoyaltyProgramConfigs.FirstOrDefaultAsync();
        if (loyaltyConfig != null)
        {
            loyaltyConfig.MaxRedemptionPerDay = 500m;
            await _context.SaveChangesAsync();
        }

        var customer = await _context.Customers.FindAsync(_customerId);
        customer!.RunningLoyaltyPoints = 1000m;
        await _context.SaveChangesAsync();

        var cmd1 = new CreateInvoiceCommand(
            InvoiceNumber:        $"INV-EXC-1-{Guid.NewGuid():N}",
            TerminalId:           _terminalId,
            CashierId:            _cashierId,
            CustomerId:           _customerId,
            PromoCode:            null,
            WalletAmountUsed:     0m,
            CashAmount:           470m,
            UpiAmount:            0m,
            CardAmount:           0m,
            RoundOff:             0m,
            NetPayable:           470m,
            PaymentMode:          "CASH",
            Items: new List<InvoiceItemDto> { new(_productId, 1, 500m, null) },
            PointsRedeemed:       300,
            SupervisorOverridePin: null
        );

        var cmd2 = new CreateInvoiceCommand(
            InvoiceNumber:        $"INV-EXC-2-{Guid.NewGuid():N}",
            TerminalId:           _terminalId,
            CashierId:            _cashierId,
            CustomerId:           _customerId,
            PromoCode:            null,
            WalletAmountUsed:     0m,
            CashAmount:           479m,
            UpiAmount:            0m,
            CardAmount:           0m,
            RoundOff:             0m,
            NetPayable:           479.9m,
            PaymentMode:          "CASH",
            Items: new List<InvoiceItemDto> { new(_productId, 1, 500m, null) },
            PointsRedeemed:       201, // brings cumulative to 501, exceeding the 500 limit
            SupervisorOverridePin: null
        );

        // ── Act & Assert ──────────────────────────────────────────────────────
        await _invoiceHandler.Handle(cmd1, CancellationToken.None);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await _invoiceHandler.Handle(cmd2, CancellationToken.None);
        });

        Assert.Contains("DAILY_REDEMPTION_LIMIT_EXCEEDED", ex.Message);
    }

    [Fact]
    public async Task LoyaltyDailyRedemptionLimitDateResetTest()
    {
        // ── Arrange ───────────────────────────────────────────────────────────
        var loyaltyConfig = await _context.LoyaltyProgramConfigs.FirstOrDefaultAsync();
        if (loyaltyConfig != null)
        {
            loyaltyConfig.MaxRedemptionPerDay = 500m;
            await _context.SaveChangesAsync();
        }

        var customer = await _context.Customers.FindAsync(_customerId);
        customer!.RunningLoyaltyPoints = 1000m;
        await _context.SaveChangesAsync();

        var cmd1 = new CreateInvoiceCommand(
            InvoiceNumber:        $"INV-RST-1-{Guid.NewGuid():N}",
            TerminalId:           _terminalId,
            CashierId:            _cashierId,
            CustomerId:           _customerId,
            PromoCode:            null,
            WalletAmountUsed:     0m,
            CashAmount:           470m,
            UpiAmount:            0m,
            CardAmount:           0m,
            RoundOff:             0m,
            NetPayable:           470m,
            PaymentMode:          "CASH",
            Items: new List<InvoiceItemDto> { new(_productId, 1, 500m, null) },
            PointsRedeemed:       300,
            SupervisorOverridePin: null
        );

        var cmd2 = new CreateInvoiceCommand(
            InvoiceNumber:        $"INV-RST-2-{Guid.NewGuid():N}",
            TerminalId:           _terminalId,
            CashierId:            _cashierId,
            CustomerId:           _customerId,
            PromoCode:            null,
            WalletAmountUsed:     0m,
            CashAmount:           480m,
            UpiAmount:            0m,
            CardAmount:           0m,
            RoundOff:             0m,
            NetPayable:           480m,
            PaymentMode:          "CASH",
            Items: new List<InvoiceItemDto> { new(_productId, 1, 500m, null) },
            PointsRedeemed:       200, // brings cumulative to exactly 500 limit for today
            SupervisorOverridePin: null
        );

        // Process today's checkouts
        await _invoiceHandler.Handle(cmd1, CancellationToken.None);
        await _invoiceHandler.Handle(cmd2, CancellationToken.None);

        // Verify that trying to redeem 1 more point on today throws limit exceeded
        var cmdTodayFail = new CreateInvoiceCommand(
            InvoiceNumber:        $"INV-RST-FAIL-{Guid.NewGuid():N}",
            TerminalId:           _terminalId,
            CashierId:            _cashierId,
            CustomerId:           _customerId,
            PromoCode:            null,
            WalletAmountUsed:     0m,
            CashAmount:           499.9m,
            UpiAmount:            0m,
            CardAmount:           0m,
            RoundOff:             0m,
            NetPayable:           499.9m,
            PaymentMode:          "CASH",
            Items: new List<InvoiceItemDto> { new(_productId, 1, 500m, null) },
            PointsRedeemed:       1,
            SupervisorOverridePin: null
        );

        var exToday = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await _invoiceHandler.Handle(cmdTodayFail, CancellationToken.None);
        });
        Assert.Contains("DAILY_REDEMPTION_LIMIT_EXCEEDED", exToday.Message);

        // ── Simulating Date Shift ─────────────────────────────────────────────
        var activeSession = await _context.StoreBusinessDates
            .FirstOrDefaultAsync(d => d.StoreId == Guid.Empty && d.Status == "OPEN");
        if (activeSession != null)
        {
            activeSession.Status = "CLOSED";
        }
        var tomorrow = DateTime.UtcNow.Date.AddDays(1);
        _context.StoreBusinessDates.Add(new StoreBusinessDate
        {
            StoreId = Guid.Empty,
            BusinessDate = tomorrow,
            Status = "OPEN"
        });
        await _context.SaveChangesAsync();

        // ── Act ──
        // Execute checkout on tomorrow's date with 300 points redeemed. Since date has shifted, this should succeed.
        var cmdTomorrow = new CreateInvoiceCommand(
            InvoiceNumber:        $"INV-RST-TOMORROW-{Guid.NewGuid():N}",
            TerminalId:           _terminalId,
            CashierId:            _cashierId,
            CustomerId:           _customerId,
            PromoCode:            null,
            WalletAmountUsed:     0m,
            CashAmount:           470m,
            UpiAmount:            0m,
            CardAmount:           0m,
            RoundOff:             0m,
            NetPayable:           470m,
            PaymentMode:          "CASH",
            Items: new List<InvoiceItemDto> { new(_productId, 1, 500m, null) },
            PointsRedeemed:       300,
            SupervisorOverridePin: null
        );

        var idTomorrow = (await _invoiceHandler.Handle(cmdTomorrow, CancellationToken.None)).InvoiceId;

        // ── Assert ──
        Assert.NotEqual(Guid.Empty, idTomorrow);
        
        var finalCustomer = await _context.Customers.FindAsync(_customerId);
        // Start: 1000
        // Redeemed today: 500, earned: 47 + 48 = 95 -> Balance: 595
        // Redeemed tomorrow: 300, earned: 47 -> Balance: 595 - 300 + 47 = 342
        Assert.Equal(342m, finalCustomer!.RunningLoyaltyPoints);
    }

    public void Dispose() => _context.Dispose();
}
