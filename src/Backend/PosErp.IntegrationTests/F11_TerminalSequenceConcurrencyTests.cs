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
using PosErp.Domain.Entities.Auth;
using PosErp.Domain.Entities.Catalog;
using PosErp.Domain.Entities.Inventory;
using PosErp.Domain.Entities.Pos;
using PosErp.Domain.Entities.Finance;
using PosErp.Infrastructure.Identity;
using PosErp.Infrastructure.Persistence;
using Xunit;

namespace PosErp.IntegrationTests;

[Collection("Database Collection")]
public class F11_TerminalSequenceConcurrencyTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly CreateInvoiceCommandHandler _invoiceHandler;

    private readonly Guid _terminalId = Guid.NewGuid();
    private readonly Guid _cashierId = Guid.NewGuid();
    private readonly Guid _productId = Guid.NewGuid();

    public F11_TerminalSequenceConcurrencyTests()
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

    private async Task SeedDataAsync()
    {
        // Seed Terminal
        var terminal = new Terminal
        {
            Id = _terminalId,
            TerminalCode = "POS-C1",
            Name = "Concurrent Terminal",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        _context.Terminals.Add(terminal);

        // Seed Tax Slab
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
            Id = _productId,
            ProductCode = "P-CONC-01",
            Name = "Concurrent Test Product",
            TaxSlabId = taxSlab.Id,
            UnitOfMeasureId = uom.Id,
            Mrp = 100m,
            SellingPrice = 100m,
            PurchasePrice = 50m,
            IsActive = true
        };
        _context.Products.Add(product);

        // Seed Barcode
        var barcode = new Barcode
        {
            Id = Guid.NewGuid(),
            ProductId = _productId,
            BarcodeValue = "BAR-CONC-01",
            IsPrimary = true
        };
        _context.Barcodes.Add(barcode);

        // Seed Stock Ledger
        var stock = new StockLedgerEntry
        {
            Id = Guid.NewGuid(),
            StoreId = Guid.Empty,
            ProductId = _productId,
            MovementType = "GRN",
            Quantity = 1000m,
            UnitCost = 50m,
            RunningBalance = 1000m,
            BusinessDate = DateTime.UtcNow.Date,
            ReferenceDocumentId = Guid.NewGuid(),
            ReferenceNumber = "SEED-CONC"
        };
        _context.StockLedger.Add(stock);

        // Open Store Business Date
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

    private async Task<CreateInvoiceResponse> ExecuteCheckoutInNewContextAsync(CreateInvoiceCommand cmd)
    {
        using var context = IntegrationTestDbFactory.CreateNewContext();
        var cache      = new MemoryCache(new MemoryCacheOptions());
        var hasher     = new PasswordHasher();
        var periodLock = new PeriodLockService(context);
        var docSeq     = new DocumentSequenceService(context);
        var approval   = new ApprovalWorkflowService(context);
        var posting    = new FinancialPostingService(context, periodLock, docSeq, approval);
        var stockSvc   = new StockLedgerService(context);
        var walletSvc  = new WalletService(context);
        var loyaltySvc = new LoyaltyService(context);
        var offerEng   = new OfferEngine(context, cache);

        var accountRes = new PosErp.Infrastructure.Services.AccountResolutionService(context);

        var handler = new CreateInvoiceCommandHandler(
            context, offerEng, walletSvc, loyaltySvc, posting, stockSvc, hasher, accountRes, logger: null);

        return await handler.Handle(cmd, CancellationToken.None);
    }

    [Fact]
    public async Task ConcurrentTerminalSequenceAndInvoiceNumberTest()
    {
        // ── Arrange ───────────────────────────────────────────────────────────
        await SeedDataAsync();

        var cmd1 = new CreateInvoiceCommand(
            InvoiceNumber:        "INV-TEMP-1",
            TerminalId:           _terminalId,
            CashierId:            _cashierId,
            CustomerId:           null,
            PromoCode:            null,
            WalletAmountUsed:     0m,
            CashAmount:           100m,
            UpiAmount:            0m,
            CardAmount:           0m,
            RoundOff:             0m,
            NetPayable:           100m,
            PaymentMode:          "CASH",
            Items:                new List<InvoiceItemDto> { new(_productId, 1, 100m, null) },
            PointsRedeemed:       0,
            SupervisorOverridePin: null
        );

        var cmd2 = new CreateInvoiceCommand(
            InvoiceNumber:        "INV-TEMP-2",
            TerminalId:           _terminalId,
            CashierId:            _cashierId,
            CustomerId:           null,
            PromoCode:            null,
            WalletAmountUsed:     0m,
            CashAmount:           100m,
            UpiAmount:            0m,
            CardAmount:           0m,
            RoundOff:             0m,
            NetPayable:           100m,
            PaymentMode:          "CASH",
            Items:                new List<InvoiceItemDto> { new(_productId, 1, 100m, null) },
            PointsRedeemed:       0,
            SupervisorOverridePin: null
        );

        // ── Act ───────────────────────────────────────────────────────────────
        // Run concurrent execution of both checkout commands against the same terminal row
        var task1 = Task.Run(() => ExecuteCheckoutInNewContextAsync(cmd1));
        var task2 = Task.Run(() => ExecuteCheckoutInNewContextAsync(cmd2));

        await Task.WhenAll(task1, task2);

        var res1 = await task1;
        var res2 = await task2;

        // ── Assert ────────────────────────────────────────────────────────────
        Assert.NotNull(res1);
        Assert.NotNull(res2);
        Assert.NotEqual(Guid.Empty, res1.InvoiceId);
        Assert.NotEqual(Guid.Empty, res2.InvoiceId);
        Assert.NotEqual(res1.InvoiceId, res2.InvoiceId);

        // Fetch both invoices from DB using the active business date
        var businessDate = (await _context.StoreBusinessDates
            .FirstOrDefaultAsync(d => d.StoreId == Guid.Empty && d.Status == "OPEN"))?.BusinessDate 
            ?? DateTime.UtcNow.Date;

        var invoice1 = await _context.Invoices.FindAsync(res1.InvoiceId, businessDate);
        var invoice2 = await _context.Invoices.FindAsync(res2.InvoiceId, businessDate);

        Assert.NotNull(invoice1);
        Assert.NotNull(invoice2);

        // Verify that their sequences are serialized (one is 1, the other is 2)
        Assert.NotEqual(invoice1!.TerminalSequence, invoice2!.TerminalSequence);
        var sequences = new[] { invoice1.TerminalSequence, invoice2.TerminalSequence };
        Assert.Contains(1, sequences);
        Assert.Contains(2, sequences);

        // Verify that their server-derived invoice numbers are non-colliding and sequential
        Assert.NotEqual(invoice1.InvoiceNumber, invoice2.InvoiceNumber);
        Assert.StartsWith("INV-POS-C1-", invoice1.InvoiceNumber);
        Assert.StartsWith("INV-POS-C1-", invoice2.InvoiceNumber);
    }

    [Fact]
    public async Task OfflineSyncSequenceCollisionFailsCleanlyTest()
    {
        // ── Arrange ───────────────────────────────────────────────────────────
        await SeedDataAsync();

        var businessDate = (await _context.StoreBusinessDates
            .FirstOrDefaultAsync(d => d.StoreId == Guid.Empty && d.Status == "OPEN"))?.BusinessDate 
            ?? DateTime.UtcNow.Date;
        
        // Seed first invoice with TerminalSequence = 10
        var invoiceId1 = Guid.NewGuid();
        var invoice1 = new Invoice
        {
            Id = invoiceId1,
            InvoiceNumber = "INV-SEED-01",
            TerminalId = _terminalId,
            CashierId = _cashierId,
            TerminalSequence = 10,
            BusinessDate = businessDate,
            Status = "COMPLETED"
        };
        _context.Invoices.Add(invoice1);
        await _context.SaveChangesAsync();

        // Create sync request with a different ID but same composite key (terminal sequence = 10)
        var dtoColliding = new OfflineInvoiceDto(
            Guid.NewGuid(), // different ID
            businessDate,
            "INV-COLLIDE-01",
            _terminalId,
            10, // TerminalSequence
            _cashierId,
            SubTotal: 100m,
            DiscountAmount: 0m,
            TaxAmount: 0m,
            TotalAmount: 100m,
            RoundOff: 0m,
            NetPayable: 100m,
            PaymentMode: "Cash",
            Items: new List<OfflineInvoiceItemDto>
            {
                new(
                    Guid.NewGuid(),
                    _productId,
                    Barcode: "BAR-CONC-01",
                    ProductName: "Concurrent Test Product",
                    Quantity: 1,
                    UnitPrice: 100m,
                    DiscountAmount: 0,
                    CgstRate: 0,
                    CgstAmount: 0,
                    SgstRate: 0,
                    SgstAmount: 0,
                    CessRate: 0,
                    CessAmount: 0,
                    TotalAmount: 100m
                )
            }
        );

        var syncCmd = new SyncInvoicesCommand(new List<OfflineInvoiceDto> { dtoColliding });

        var cache = new MemoryCache(new MemoryCacheOptions());
        var offerEng = new OfferEngine(_context, cache);
        var periodLock = new PeriodLockService(_context);
        var docSeq = new DocumentSequenceService(_context);
        var approval = new ApprovalWorkflowService(_context);
        var posting = new FinancialPostingService(_context, periodLock, docSeq, approval);
        
        var accountRes = new PosErp.Infrastructure.Services.AccountResolutionService(_context);
        var stockSvc = new StockLedgerService(_context);
        var walletSvc = new WalletService(_context);
        var loyaltySvc = new LoyaltyService(_context);
        
        var syncHandler = new SyncInvoicesCommandHandler(_context, offerEng, posting, accountRes, stockSvc, walletSvc, loyaltySvc, logger: null);

        // ── Act ───────────────────────────────────────────────────────────────
        var result = await syncHandler.Handle(syncCmd, CancellationToken.None);

        // ── Assert ────────────────────────────────────────────────────────────
        Assert.Equal(0, result.Synced);
        Assert.Equal(1, result.Failed);
        Assert.Single(result.Errors);
        Assert.Contains("SEQUENCE_COLLISION", result.Errors[0]);
    }

    public void Dispose() => _context.Dispose();
}
