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
/// F02 — Gap #1: credit-sale invoice must post the AR debit to account 10400
/// (Trade Receivables — ASSET), NOT to 20200 (Customer Wallet Liabilities).
///
/// Financial correctness assertion: total Debits == total Credits.
/// </summary>
[Collection("Database Collection")]
public class F02_ArGlAccountTests : IDisposable
{
    static F02_ArGlAccountTests()
    {
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
    }

    private readonly ApplicationDbContext _context;
    private readonly CreateInvoiceCommandHandler _invoiceHandler;

    // All IDs are unique per test-class instance to avoid PK collisions when
    // tests are re-run without a schema wipe.
    private readonly Guid _terminalId  = Guid.NewGuid();
    private readonly Guid _cashierId   = Guid.NewGuid();
    private readonly Guid _customerId  = Guid.NewGuid();
    private readonly Guid _productId   = Guid.NewGuid();
    private readonly Guid _taxSlabId   = Guid.NewGuid();

    public F02_ArGlAccountTests()
    {
        // Full schema rebuild — identical to AccountingIntegrationTests.GetDbContext().
        // This guarantees loyalty_ledger, stock_ledger etc. have all columns.
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
    // Test: credit sale debits 10400 (AR Asset), not 20200 (Wallet Liability)
    //       and the journal entry is balanced (Debits == Credits).
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task CreditSaleUsesArAccountTest()
    {
        // ── Prerequisite: migration 37 must have run (factory runs all migrations) ──
        bool arExists = await _context.Accounts.AnyAsync(a => a.AccountCode == "10400");
        Assert.True(arExists,
            "Account 10400 (Trade Receivables AR) must exist — check migration 37.");

        // ── Arrange ───────────────────────────────────────────────────────────
        var cmd = new CreateInvoiceCommand(
            InvoiceNumber:        $"INV-F02-{Guid.NewGuid():N}",
            TerminalId:           _terminalId,
            CashierId:            _cashierId,
            CustomerId:           _customerId,
            PromoCode:            null,
            WalletAmountUsed:     0m,
            CashAmount:           0m,
            UpiAmount:            0m,
            CardAmount:           0m,
            RoundOff:             0m,
            NetPayable:           180m,
            PaymentMode:          "CREDIT",   // <── triggers AR path
            Items: new List<InvoiceItemDto>
            {
                new(_productId, Quantity: 1, UnitPrice: 180m, BatchId: null)
            },
            PointsRedeemed:       0,
            SupervisorOverridePin: null
        );

        // ── Act ───────────────────────────────────────────────────────────────
        var invoiceId = (await _invoiceHandler.Handle(cmd, CancellationToken.None)).InvoiceId;

        // ── Assert ────────────────────────────────────────────────────────────
        // CreateInvoiceCommand passes refDoc = $"INV-{invoice.Id}" to PostJournalEntryAsync
        var je = await _context.JournalEntries
            .Include(j => j.Lines).ThenInclude(l => l.Account)
            .FirstOrDefaultAsync(j => j.ReferenceDocument == $"INV-{invoiceId}");

        Assert.NotNull(je);
        Assert.True(je!.Lines.Count >= 2, "Journal must have at least 2 lines.");

        // 1) Balanced: total Debits == total Credits
        decimal totalDebits  = je.Lines.Sum(l => l.DebitAmount);
        decimal totalCredits = je.Lines.Sum(l => l.CreditAmount);
        var linesSummary = string.Join(", ",
            je.Lines.Select(l =>
                $"{l.Account?.AccountCode}:Dr{l.DebitAmount}:Cr{l.CreditAmount}"));
        Assert.Equal(totalDebits, totalCredits);

        // 2) Exactly one DEBIT line on 10400 (Trade Receivables — ASSET)
        var arDebits = je.Lines
            .Where(l => l.Account?.AccountCode == "10400" && l.DebitAmount > 0)
            .ToList();
        Assert.True(arDebits.Count == 1,
            $"Expected exactly 1 debit on 10400, found {arDebits.Count}. Lines: {linesSummary}");
        Assert.Equal(180m, arDebits[0].DebitAmount);

        // 3) No debit on 20200 (Customer Wallet Liabilities) — wallet was not used
        var walletDebits = je.Lines
            .Where(l => l.Account?.AccountCode == "20200" && l.DebitAmount > 0)
            .ToList();
        Assert.Empty(walletDebits);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Seed — minimal data required by CreateInvoiceCommandHandler
    // ─────────────────────────────────────────────────────────────────────────
    private async Task SeedAsync()
    {
        // The factory creates Guid.Empty store via migrations/seeds;
        // confirm it is active (some seed scripts set it inactive).
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
            Name         = "F02-Terminal",
            TerminalCode = $"F02T{_terminalId:N}"[..8],
            IsActive     = true
        });

        // Role + cashier user
        var role = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Cashier")
                   ?? new Role { Id = Guid.NewGuid(), Name = "Cashier", Description = "Cashier" };
        if (_context.Entry(role).State == EntityState.Detached)
            _context.Roles.Add(role);

        _context.Users.Add(new User
        {
            Id           = _cashierId,
            Username     = $"f02-{_cashierId:N}@test",
            PasswordHash = new PasswordHasher().HashPassword("Test@1234"),
            FullName     = "F02 Cashier",
            RoleId       = role.Id,
            IsActive     = true
        });

        // Customer (credit limit covers the 180 invoice)
        _context.Customers.Add(new Customer
        {
            Id          = _customerId,
            Name        = "F02 Credit Customer",
            Phone       = $"9{_customerId.ToString("N")[..9]}",
            CreditLimit = 50_000m
        });

        // Tax slab — use any existing one or create a zero-rate slab
        var taxSlab = await _context.TaxSlabs.FirstOrDefaultAsync();
        if (taxSlab == null)
        {
            taxSlab = new TaxSlab
            {
                Id = _taxSlabId, Name = "GST 18%",
                CgstRate = 9m, SgstRate = 9m, IgstRate = 18m, CessRate = 0m
            };
            _context.TaxSlabs.Add(taxSlab);
        }

        // UoM — the migration SQL seeds at least one UoM; re-use it.
        var uomId = await _context.UnitOfMeasures
            .Select(u => u.Id)
            .FirstOrDefaultAsync();
        if (uomId == Guid.Empty)
            throw new InvalidOperationException(
                "No UnitOfMeasure rows found — check migration seeds.");

        // Product
        _context.Products.Add(new Product
        {
            Id              = _productId,
            ProductCode     = $"F02{_productId:N}"[..10],
            Name            = "F02 Test Product",
            TaxSlabId       = taxSlab.Id,
            UnitOfMeasureId = uomId,
            Mrp             = 200m,
            SellingPrice    = 180m,
            PurchasePrice   = 120m,
            IsActive        = true,
            HasExpiry       = false
        });

        // Stock (so RowLevelLocking check doesn't block the sale)
        _context.StockLedger.Add(new StockLedgerEntry
        {
            StoreId             = Guid.Empty,
            ProductId           = _productId,
            MovementType        = "OPENING",
            Quantity            = 100m,
            UnitCost            = 120m,
            RunningBalance      = 100m,
            BusinessDate        = DateTime.UtcNow.Date,
            ReferenceDocumentId = Guid.NewGuid(),
            ReferenceNumber     = "OPENING-F02"
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

    public void Dispose() => _context.Dispose();
}
