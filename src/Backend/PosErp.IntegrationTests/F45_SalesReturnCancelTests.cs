using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using PosErp.Api.Controllers;
using PosErp.Application.Interfaces;
using PosErp.Application.Features.Audit.Services;
using PosErp.Application.Features.Crm.Services;
using PosErp.Application.Features.Finance.Commands;
using PosErp.Application.Features.Finance.Services;
using PosErp.Application.Features.Inventory.Services;
using PosErp.Application.Features.Offers.Services;
using PosErp.Application.Features.Pos.Commands;
using PosErp.Application.Features.Pos.Commands.SyncInvoices;
using PosErp.Domain.Entities.Auth;
using PosErp.Domain.Entities.Catalog;
using PosErp.Domain.Entities.Crm;
using PosErp.Domain.Entities.Inventory;
using PosErp.Domain.Entities.Pos;
using PosErp.Domain.Entities.Finance;
using PosErp.Infrastructure.Identity;
using PosErp.Infrastructure.Persistence;
using Xunit;

namespace PosErp.IntegrationTests;

[Collection("Database Collection")]
public class F45_SalesReturnCancelTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly CreateInvoiceCommandHandler _invoiceHandler;
    private readonly CancelSalesReturnCommandHandler _cancelReturnHandler;
    private readonly CancelInvoiceCommandHandler _cancelInvoiceHandler;
    private readonly HttpContextAccessorMock _httpContextAccessor;

    private readonly Guid _storeId = Guid.NewGuid();
    private readonly Guid _terminalId = Guid.NewGuid();
    private readonly Guid _cashierId = Guid.NewGuid();
    private readonly Guid _managerId = Guid.NewGuid();
    private readonly Guid _customerId = Guid.NewGuid();
    private readonly Guid _productId = Guid.NewGuid();
    private readonly Guid _taxSlabId = Guid.NewGuid();

    public F45_SalesReturnCancelTests()
    {
        _context = IntegrationTestDbFactory.Build();
        _httpContextAccessor = new HttpContextAccessorMock();

        var cache = new MemoryCache(new MemoryCacheOptions());
        var hasher = new PasswordHasher();
        var periodLock = new PeriodLockService(_context);
        var docSeq = new DocumentSequenceService(_context);
        var approval = new ApprovalWorkflowService(_context);
        var posting = new FinancialPostingService(_context, periodLock, docSeq, approval);
        var stockSvc = new StockLedgerService(_context);
        var walletSvc = new WalletService(_context);
        var loyaltySvc = new LoyaltyService(_context);
        var offerEng = new OfferEngine(_context, cache);
        var accountRes = new PosErp.Infrastructure.Services.AccountResolutionService(_context);
        var auditLogger = new AuditLoggingService(_context, new TenantProviderMock(), _httpContextAccessor);

        _invoiceHandler = new CreateInvoiceCommandHandler(
            _context, offerEng, walletSvc, loyaltySvc, posting, stockSvc, hasher, accountRes, _httpContextAccessor);

        _cancelReturnHandler = new CancelSalesReturnCommandHandler(
            _context, stockSvc, posting, auditLogger, periodLock, _httpContextAccessor);

        _cancelInvoiceHandler = new CancelInvoiceCommandHandler(
            _context, posting, stockSvc, walletSvc, loyaltySvc, periodLock);

        SeedAsync().GetAwaiter().GetResult();
    }

    private async Task SeedAsync()
    {
        // 1. Roles
        var ownerRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Owner")
            ?? new Role { Id = Guid.NewGuid(), Name = "Owner", Description = "Owner role" };
        if (_context.Entry(ownerRole).State == EntityState.Detached)
            _context.Roles.Add(ownerRole);

        var managerRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Manager")
            ?? new Role { Id = Guid.NewGuid(), Name = "Manager", Description = "Manager role" };
        if (_context.Entry(managerRole).State == EntityState.Detached)
            _context.Roles.Add(managerRole);

        var cashierRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Cashier")
            ?? new Role { Id = Guid.NewGuid(), Name = "Cashier", Description = "Cashier role" };
        if (_context.Entry(cashierRole).State == EntityState.Detached)
            _context.Roles.Add(cashierRole);

        // 2. Users
        var hasher = new PasswordHasher();
        _context.Users.Add(new User
        {
            Id = _managerId,
            Username = "owner_test",
            PasswordHash = hasher.HashPassword("Test@1234"),
            RoleId = ownerRole.Id,
            IsActive = true,
            FullName = "Test Owner"
        });

        _context.Users.Add(new User
        {
            Id = _cashierId,
            Username = "cashier_test",
            PasswordHash = hasher.HashPassword("Test@1234"),
            RoleId = cashierRole.Id,
            IsActive = true,
            FullName = "Test Cashier"
        });

        // 3. Store
        var store = new Store { Id = _storeId, StoreName = "Test Store", StoreCode = "TSTSTORE", IsActive = true };
        _context.Stores.Add(store);

        // 4. Terminal
        var terminal = new Terminal { Id = _terminalId, Name = "T01", TerminalCode = "TST-T01", IsActive = true };
        _context.Terminals.Add(terminal);

        // 5. Customer
        var customer = new Customer { Id = _customerId, Name = "Loyal Customer", Phone = "9999999999", CreditLimit = 50000m };
        _context.Customers.Add(customer);

        // 6. Tax Slab & Product
        var taxSlab = new TaxSlab { Id = _taxSlabId, Name = "18% GST", CgstRate = 9m, SgstRate = 9m, CessRate = 0m };
        _context.TaxSlabs.Add(taxSlab);

        var uomId = await _context.UnitOfMeasures.Select(u => u.Id).FirstOrDefaultAsync();
        if (uomId == Guid.Empty)
        {
            var uom = new UnitOfMeasure { Id = Guid.NewGuid(), Symbol = "PCS", Name = "Pieces" };
            _context.UnitOfMeasures.Add(uom);
            uomId = uom.Id;
        }

        var product = new Product
        {
            Id = _productId,
            Name = "Audit Product",
            ProductCode = "PROD-AUDIT",
            TaxSlabId = _taxSlabId,
            UnitOfMeasureId = uomId,
            Mrp = 118m,
            SellingPrice = 118m,
            PurchasePrice = 60m,
            IsActive = true
        };
        _context.Products.Add(product);

        // 7. Accounts
        var accountsToAdd = new List<Account>();
        var codesToSeed = new[] { "10100", "10300", "10400", "20200", "22010", "22020", "40100", "50100" };
        foreach (var code in codesToSeed)
        {
            if (!await _context.Accounts.AnyAsync(a => a.AccountCode == code))
            {
                accountsToAdd.Add(new Account
                {
                    Id = Guid.NewGuid(),
                    AccountCode = code,
                    Name = $"Account {code}",
                    AccountType = code.StartsWith("1") ? "ASSET" : code.StartsWith("2") ? "LIABILITY" : code.StartsWith("4") ? "REVENUE" : "EXPENSE",
                    IsActive = true
                });
            }
        }
        if (accountsToAdd.Count > 0)
        {
            _context.Accounts.AddRange(accountsToAdd);
        }

        await _context.SaveChangesAsync();

        // 8. Product Batch (10 items stock)
        var batch = new ProductBatch
        {
            Id = Guid.NewGuid(),
            StoreId = _storeId,
            ProductId = _productId,
            BatchNumber = "B-001",
            MfgDate = DateTime.UtcNow.AddDays(-10),
            ExpiryDate = DateTime.UtcNow.AddDays(100),
            Mrp = 118m,
            CostPrice = 60m,
            AvailableQuantity = 10m,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        _context.ProductBatches.Add(batch);

        // 9. Document sequences
        var seqInvoice = new DocumentSequence { StoreId = _storeId, DocumentType = "INVOICE", Prefix = "INV", CurrentNumber = 100, Padding = 6 };
        var seqReturn = new DocumentSequence { StoreId = _storeId, DocumentType = "SALES_RETURN", Prefix = "RET", CurrentNumber = 50, Padding = 6 };
        _context.DocumentSequences.AddRange(seqInvoice, seqReturn);

        // 10. Open business date
        _context.StoreBusinessDates.Add(new StoreBusinessDate
        {
            StoreId = _storeId,
            BusinessDate = DateTime.UtcNow.Date,
            Status = "OPEN"
        });

        await _context.SaveChangesAsync();
    }

    // =========================================================================
    // R-03: Security validation check on AiJobValidationController
    // =========================================================================
    [Fact]
    public void AiJobValidationController_RunAllJobs_MustBeGatedByAuthorizeOwnerDeveloper()
    {
        var method = typeof(AiJobValidationController).GetMethod("RunAllJobs");
        Assert.NotNull(method);

        var authorizeAttr = method.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(authorizeAttr);
        Assert.Equal("Owner,Developer", authorizeAttr.Roles);

        var allowAnonymousAttr = method.GetCustomAttribute<AllowAnonymousAttribute>();
        Assert.Null(allowAnonymousAttr);
    }

    // =========================================================================
    // R-04: Test Invoice role check tests
    // =========================================================================
    [Fact]
    public async Task CreateInvoice_WithTestPrefix_ThrowsUnauthorizedForCashier()
    {
        // Arrange HTTP Context as Cashier
        _httpContextAccessor.SetUser(_cashierId, "Cashier");

        var cmd = new CreateInvoiceCommand(
            InvoiceNumber: "TEST-001",
            TerminalId: _terminalId,
            CashierId: _cashierId,
            CustomerId: _customerId,
            PromoCode: null,
            WalletAmountUsed: 0m,
            CashAmount: 118m,
            UpiAmount: 0m,
            CardAmount: 0m,
            RoundOff: 0m,
            NetPayable: 118m,
            PaymentMode: "CASH",
            Items: new List<InvoiceItemDto>
            {
                new(_productId, 1m, 118m, _context.ProductBatches.First().Id)
            }
        );

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
        {
            await _invoiceHandler.Handle(cmd, CancellationToken.None);
        });
    }

    [Fact]
    public async Task CreateInvoice_WithTestPrefix_SucceedsForOwner()
    {
        // Arrange HTTP Context as Owner
        _httpContextAccessor.SetUser(_managerId, "Owner");

        var batchId = _context.ProductBatches.First().Id;
        var cmd = new CreateInvoiceCommand(
            InvoiceNumber: "TEST-F45-OWNER",
            TerminalId: _terminalId,
            CashierId: _managerId,
            CustomerId: _customerId,
            PromoCode: null,
            WalletAmountUsed: 0m,
            CashAmount: 118m,
            UpiAmount: 0m,
            CardAmount: 0m,
            RoundOff: 0m,
            NetPayable: 118m,
            PaymentMode: "CASH",
            Items: new List<InvoiceItemDto>
            {
                new(_productId, 1m, 118m, batchId)
            }
        );

        // Act
        var res = await _invoiceHandler.Handle(cmd, CancellationToken.None);

        // Assert
        Assert.Equal("TEST-F45-OWNER", res.InvoiceNumber);
        var createdInvoice = await _context.Invoices.FirstOrDefaultAsync(i => i.Id == res.InvoiceId);
        Assert.NotNull(createdInvoice);
        Assert.Equal("TEST-F45-OWNER", createdInvoice.InvoiceNumber);
    }

    // =========================================================================
    // GAP-002: Cancel Sales Return Tests
    // =========================================================================
    [Fact]
    public async Task CancelSalesReturn_Succeeds_ReversesTransactionsAndRestoresInvoiceCancellability()
    {
        // ── Arrange ───────────────────────────────────────────────────────────
        _httpContextAccessor.SetUser(_managerId, "Owner");
        var batch = _context.ProductBatches.First();
        var initialStock = batch.AvailableQuantity; // Should be 10 (or lower if other tests mutated DB, but IntegrationTestDbFactory rebuilds clean schema each call)

        // 1. Create original checkout invoice
        var checkoutCmd = new CreateInvoiceCommand(
            InvoiceNumber: "",
            TerminalId: _terminalId,
            CashierId: _managerId,
            CustomerId: _customerId,
            PromoCode: null,
            WalletAmountUsed: 0m,
            CashAmount: 236m,
            UpiAmount: 0m,
            CardAmount: 0m,
            RoundOff: 0m,
            NetPayable: 236m,
            PaymentMode: "CASH",
            Items: new List<InvoiceItemDto>
            {
                new(_productId, 2m, 118m, batch.Id) // Unit price is tax-inclusive 118
            }
        );
        var invoiceRes = await _invoiceHandler.Handle(checkoutCmd, CancellationToken.None);
        var invoiceId = invoiceRes.InvoiceId;

        // Reload batch to check stock (it should decrease by 2)
        await _context.Entry(batch).ReloadAsync();
        Assert.Equal(initialStock - 2, batch.AvailableQuantity);

        // 2. Perform a Sales Return for 1 item
        var returnHandler = new ReturnCommandsHandler(
            _context,
            new FinancialPostingService(_context, new PeriodLockService(_context), new DocumentSequenceService(_context), new ApprovalWorkflowService(_context)),
            new DocumentSequenceService(_context),
            new StockLedgerService(_context),
            new PasswordHasher()
        );

        var returnCmd = new ProcessSalesReturnCommand(
            StoreId: _storeId,
            InvoiceId: invoiceId,
            ReturnDate: DateTime.UtcNow,
            RefundMode: "CREDIT_NOTE",
            Items: new List<SalesReturnItemInputDto>
            {
                new(_productId, batch.Id, 1m)
            },
            UserId: _managerId
        );

        var returnId = await returnHandler.Handle(returnCmd, CancellationToken.None);

        // Verify status and restock
        var salesReturnObj = await _context.SalesReturns.FirstAsync(r => r.Id == returnId);
        Assert.Equal("COMPLETED", salesReturnObj.Status);
        Assert.NotNull(salesReturnObj.JournalEntryId);

        await _context.Entry(batch).ReloadAsync();
        Assert.Equal(initialStock - 1, batch.AvailableQuantity); // Restocked by 1

        // Confirm Invoice cannot be cancelled because of the active return
        var cancelInvoiceCmd = new CancelInvoiceCommand(invoiceId, _managerId);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await _cancelInvoiceHandler.Handle(cancelInvoiceCmd, CancellationToken.None);
        });
        Assert.Contains("active sales return", ex.Message);

        // 3. Cancel the Sales Return
        var cancelReturnCmd = new CancelSalesReturnCommand(returnId, "Incorrect return items");
        var cancelRes = await _cancelReturnHandler.Handle(cancelReturnCmd, CancellationToken.None);
        Assert.True(cancelRes);

        // Reload return and verify Cancelled status
        await _context.Entry(salesReturnObj).ReloadAsync();
        Assert.Equal("CANCELLED", salesReturnObj.Status);

        // Reload batch and verify stock is decremented back down by 1 (total stock = initial - 2)
        await _context.Entry(batch).ReloadAsync();
        Assert.Equal(initialStock - 2, batch.AvailableQuantity);

        // Verify audit logs were written
        var auditLog = await _context.AuditLogs
            .FirstOrDefaultAsync(a => a.EntityId == returnId.ToString() && a.Action == "CANCEL_SALES_RETURN");
        Assert.NotNull(auditLog);
        Assert.Contains("Incorrect return items", auditLog.Details);

        // Verify direct journal lines reversal (offsets original debits and credits)
        var cancelJeExists = await _context.JournalEntries
            .AnyAsync(je => je.ReferenceDocument == $"CAN-{salesReturnObj.ReturnNumber}");
        Assert.True(cancelJeExists);

        // 4. Confirm Invoice is now cancellable and cancels successfully
        var invoiceCancelled = await _cancelInvoiceHandler.Handle(cancelInvoiceCmd, CancellationToken.None);
        Assert.True(invoiceCancelled);

        var cancelledInvoice = await _context.Invoices.FirstAsync(i => i.Id == invoiceId);
        Assert.Equal("CANCELLED", cancelledInvoice.Status);
    }

    [Fact]
    public async Task CancelSalesReturn_ThrowsIfJournalEntryMissing()
    {
        _httpContextAccessor.SetUser(_managerId, "Owner");

        var badReturn = new SalesReturn
        {
            Id = Guid.NewGuid(),
            StoreId = _storeId,
            InvoiceId = Guid.NewGuid(),
            BusinessDate = DateTime.UtcNow.Date,
            ReturnNumber = "RET-BAD-JE",
            ReturnDate = DateTime.UtcNow.Date,
            SubTotal = 100m,
            TaxAmount = 18m,
            TotalAmount = 118m,
            RefundAmount = 118m,
            RefundMode = "CASH",
            Status = "COMPLETED",
            JournalEntryId = null // Null JE!
        };
        _context.SalesReturns.Add(badReturn);
        await _context.SaveChangesAsync();

        var cancelCmd = new CancelSalesReturnCommand(badReturn.Id);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await _cancelReturnHandler.Handle(cancelCmd, CancellationToken.None);
        });
        Assert.Contains("no linked journal entry found", ex.Message);
    }

    [Fact]
    public async Task CancelSalesReturn_ThrowsIfAlreadyCancelled()
    {
        _httpContextAccessor.SetUser(_managerId, "Owner");

        var je = new JournalEntry
        {
            Id = Guid.NewGuid(),
            StoreId = _storeId,
            EntryDate = DateTime.UtcNow.Date,
            Description = "Fake JE",
            ReferenceDocument = "RET-ALREADY",
            Status = "POSTED",
            CreatedAt = DateTime.UtcNow
        };
        _context.JournalEntries.Add(je);
        await _context.SaveChangesAsync();

        var cancelledReturn = new SalesReturn
        {
            Id = Guid.NewGuid(),
            StoreId = _storeId,
            InvoiceId = Guid.NewGuid(),
            BusinessDate = DateTime.UtcNow.Date,
            ReturnNumber = "RET-ALREADY",
            ReturnDate = DateTime.UtcNow.Date,
            SubTotal = 100m,
            TaxAmount = 18m,
            TotalAmount = 118m,
            RefundAmount = 118m,
            RefundMode = "CASH",
            Status = "CANCELLED",
            JournalEntryId = je.Id
        };
        _context.SalesReturns.Add(cancelledReturn);
        await _context.SaveChangesAsync();

        var cancelCmd = new CancelSalesReturnCommand(cancelledReturn.Id);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await _cancelReturnHandler.Handle(cancelCmd, CancellationToken.None);
        });
        Assert.Contains("already cancelled", ex.Message);
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Helpers/Mocks
// ─────────────────────────────────────────────────────────────────────────────
public class HttpContextAccessorMock : IHttpContextAccessor
{
    public HttpContext? HttpContext { get; set; } = new DefaultHttpContext();

    public void SetUser(Guid userId, string roleName)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Role, roleName)
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity)
        };
    }
}

public class TenantProviderMock : ITenantProvider
{
    public Guid TenantId => Guid.Parse("00000000-0000-0000-0000-000000000001");
    public void SetTenantId(Guid tenantId) { }
}
