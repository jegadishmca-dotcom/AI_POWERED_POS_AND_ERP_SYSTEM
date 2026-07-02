using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using PosErp.Application.Features.Pos.Commands.SyncInvoices;
using PosErp.Application.Features.Offers.Services;
using PosErp.Application.Features.Crm.Services;
using PosErp.Application.Features.Finance.Services;
using PosErp.Application.Features.Inventory.Services;
using PosErp.Domain.Entities.Catalog;
using PosErp.Domain.Entities.Inventory;
using PosErp.Domain.Entities.Pos;
using PosErp.Domain.Entities.Finance;
using PosErp.Domain.Entities.Auth;
using PosErp.Infrastructure.Identity;
using PosErp.Infrastructure.Persistence;
using Xunit;

namespace PosErp.IntegrationTests;

[Collection("Database Collection")]
public class F06_CogsTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly CreateInvoiceCommandHandler _invoiceHandler;

    private readonly Guid _terminalId = Guid.NewGuid();
    private readonly Guid _cashierId = Guid.NewGuid();
    private readonly Guid _productId1 = Guid.NewGuid();
    private readonly Guid _productId2 = Guid.NewGuid();

    public F06_CogsTests()
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
            _context, offerEng, walletSvc, loyaltySvc, posting, stockSvc, hasher, accountRes, logger: null);
    }

    private async Task SeedProductAsync(Guid productId, decimal sellingPrice, decimal purchasePrice, decimal initialStock)
    {
        // Seed Terminal
        if (!await _context.Terminals.AnyAsync(t => t.Id == _terminalId))
        {
            _context.Terminals.Add(new Terminal
            {
                Id = _terminalId,
                TerminalCode = "F06T01",
                Name = "F06 Terminal",
                IsActive = true
            });
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

        // Seed Product
        var product = new Product
        {
            Id = productId,
            ProductCode = $"P-{productId.ToString()[..8]}",
            Name = $"Test Product {productId.ToString()[..4]}",
            TaxSlabId = taxSlab.Id,
            UnitOfMeasureId = uom.Id,
            Mrp = sellingPrice,
            SellingPrice = sellingPrice,
            PurchasePrice = purchasePrice,
            IsActive = true
        };
        _context.Products.Add(product);

        // Seed barcode
        var barcode = new Barcode
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            BarcodeValue = $"B-{productId.ToString()[..8]}",
            IsPrimary = true
        };
        _context.Barcodes.Add(barcode);

        // Seed stock inventory if initialStock > 0
        if (initialStock > 0)
        {
            var stockLedger = new StockLedgerEntry
            {
                Id = Guid.NewGuid(),
                StoreId = Guid.Empty,
                ProductId = productId,
                MovementType = "GRN",
                Quantity = initialStock,
                UnitCost = purchasePrice,
                RunningBalance = initialStock,
                BusinessDate = DateTime.UtcNow.Date,
                ReferenceDocumentId = Guid.NewGuid(),
                ReferenceNumber = "SEED-F06"
            };
            _context.StockLedger.Add(stockLedger);
        }

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
    public async Task NormalCogsPostingTest()
    {
        // ── Arrange ───────────────────────────────────────────────────────────
        // Product 1: SellingPrice = 100m, PurchasePrice = 60m, Stock = 10
        await SeedProductAsync(_productId1, sellingPrice: 100m, purchasePrice: 60m, initialStock: 10m);

        var cmd = new CreateInvoiceCommand(
            InvoiceNumber:        $"INV-COGS-1-{Guid.NewGuid():N}",
            TerminalId:           _terminalId,
            CashierId:            _cashierId,
            CustomerId:           null,
            PromoCode:            null,
            WalletAmountUsed:     0m,
            CashAmount:           200m,
            UpiAmount:            0m,
            CardAmount:           0m,
            RoundOff:             0m,
            NetPayable:           200m,
            PaymentMode:          "CASH",
            Items: new List<InvoiceItemDto> { new(_productId1, 2, 100m, null) },
            PointsRedeemed:       0,
            SupervisorOverridePin: null
        );

        // ── Act ───────────────────────────────────────────────────────────────
        var res = await _invoiceHandler.Handle(cmd, CancellationToken.None);
        var invoiceId = res.InvoiceId;

        // ── Assert ────────────────────────────────────────────────────────────
        Assert.NotEqual(Guid.Empty, invoiceId);

        // Retrieve Journal Entry and Lines
        var refDoc = $"INV-{invoiceId}";
        var journalEntry = await _context.JournalEntries
            .Include(j => j.Lines)
            .ThenInclude(l => l.Account)
            .FirstOrDefaultAsync(j => j.ReferenceDocument == refDoc);

        Assert.NotNull(journalEntry);
        
        // Cost of Goods Sold resolves to 50100 (Dr) and Inventory Asset to 10300 (Cr)
        // Total COGS = 2 * 60m = 120m
        var cogsLine = journalEntry.Lines.FirstOrDefault(l => l.Account.AccountCode == "50100");
        var inventoryLine = journalEntry.Lines.FirstOrDefault(l => l.Account.AccountCode == "10300");

        Assert.NotNull(cogsLine);
        Assert.NotNull(inventoryLine);

        // Assert Dr 50100 == Cr 10300
        Assert.Equal(120m, cogsLine.DebitAmount);
        Assert.Equal(0m, cogsLine.CreditAmount);

        Assert.Equal(0m, inventoryLine.DebitAmount);
        Assert.Equal(120m, inventoryLine.CreditAmount);

        Assert.Equal(cogsLine.DebitAmount, inventoryLine.CreditAmount); // Dr 50100 == Cr 10300

        // Verify total debits equal total credits
        decimal totalDebits = journalEntry.Lines.Sum(l => l.DebitAmount);
        decimal totalCredits = journalEntry.Lines.Sum(l => l.CreditAmount);
        Assert.Equal(totalDebits, totalCredits);
    }

    [Fact]
    public async Task ZeroPurchasePriceSkippedCogsTest()
    {
        // ── Arrange ───────────────────────────────────────────────────────────
        // Product 2: SellingPrice = 100m, PurchasePrice = 0m, Stock = 10
        await SeedProductAsync(_productId2, sellingPrice: 100m, purchasePrice: 0m, initialStock: 10m);

        var cmd = new CreateInvoiceCommand(
            InvoiceNumber:        $"INV-COGS-2-{Guid.NewGuid():N}",
            TerminalId:           _terminalId,
            CashierId:            _cashierId,
            CustomerId:           null,
            PromoCode:            null,
            WalletAmountUsed:     0m,
            CashAmount:           200m,
            UpiAmount:            0m,
            CardAmount:           0m,
            RoundOff:             0m,
            NetPayable:           200m,
            PaymentMode:          "CASH",
            Items: new List<InvoiceItemDto> { new(_productId2, 2, 100m, null) },
            PointsRedeemed:       0,
            SupervisorOverridePin: null
        );

        // ── Act ───────────────────────────────────────────────────────────────
        var res = await _invoiceHandler.Handle(cmd, CancellationToken.None);
        var invoiceId = res.InvoiceId;

        // ── Assert ────────────────────────────────────────────────────────────
        Assert.NotEqual(Guid.Empty, invoiceId);

        var refDoc = $"INV-{invoiceId}";
        var journalEntry = await _context.JournalEntries
            .Include(j => j.Lines)
            .ThenInclude(l => l.Account)
            .FirstOrDefaultAsync(j => j.ReferenceDocument == refDoc);

        Assert.NotNull(journalEntry);
        
        // Assert no COGS lines (50100 / 10300) are posted in journal
        var cogsLine = journalEntry.Lines.FirstOrDefault(l => l.Account.AccountCode == "50100");
        var inventoryLine = journalEntry.Lines.FirstOrDefault(l => l.Account.AccountCode == "10300");

        Assert.Null(cogsLine);
        Assert.Null(inventoryLine);

        // Verify total debits equal total credits
        decimal totalDebits = journalEntry.Lines.Sum(l => l.DebitAmount);
        decimal totalCredits = journalEntry.Lines.Sum(l => l.CreditAmount);
        Assert.Equal(totalDebits, totalCredits);
    }

    public void Dispose() => _context.Dispose();
}
