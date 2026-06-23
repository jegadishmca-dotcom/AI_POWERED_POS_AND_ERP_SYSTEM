using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Npgsql;
using PosErp.Application.Features.Crm.Services;
using PosErp.Application.Features.Finance.Commands;
using PosErp.Application.Features.Finance.Services;
using PosErp.Application.Features.Inventory.Services;
using PosErp.Application.Features.Offers.Services;
using PosErp.Application.Features.Pos.Commands;
using PosErp.Application.Features.Pos.Commands.SyncInvoices;
using PosErp.Application.Interfaces;
using PosErp.Domain.Entities.Auth;
using PosErp.Domain.Entities.Catalog;
using PosErp.Domain.Entities.Crm;
using PosErp.Domain.Entities.Finance;
using PosErp.Domain.Entities.Inventory;
using PosErp.Domain.Entities.Pos;
using PosErp.Domain.Entities.Purchasing;
using PosErp.Infrastructure.Identity;
using PosErp.Infrastructure.Persistence;
using Xunit;

namespace PosErp.IntegrationTests;

public class AccountingIntegrationTests : IDisposable
{
    static AccountingIntegrationTests()
    {
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
    }

    private readonly ApplicationDbContext _context;
    private readonly MemoryCache _memoryCache;
    private readonly PeriodLockService _periodLockService;
    private readonly DocumentSequenceService _sequenceService;
    private readonly ApprovalWorkflowService _approvalService;
    private readonly FinancialPostingService _postingService;
    private readonly AllocationEngine _allocationEngine;
    private readonly StockLedgerService _stockLedgerService;
    private readonly PasswordHasher _passwordHasher;
    private readonly WalletService _walletService;
    private readonly LoyaltyService _loyaltyService;
    private readonly OfferEngine _offerEngine;

    private readonly APCommandsAndQueriesHandler _apHandler;
    private readonly ARCommandsAndQueriesHandler _arHandler;
    private readonly CreateInvoiceCommandHandler _invoiceHandler;
    private readonly ClosePosSessionCommandHandler _closeSessionHandler;
    private readonly ReturnCommandsHandler _returnHandler;
    private readonly TransferCommandsHandler _transferHandler;

    private readonly Guid _storeId;
    private readonly Guid _supplierId;
    private readonly Guid _customerId;
    private readonly Guid _userId;

    public AccountingIntegrationTests()
    {
        _context = GetDbContext();
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        _passwordHasher = new PasswordHasher();

        _periodLockService = new PeriodLockService(_context);
        _sequenceService = new DocumentSequenceService(_context);
        _approvalService = new ApprovalWorkflowService(_context);
        _postingService = new FinancialPostingService(_context, _periodLockService, _sequenceService, _approvalService);
        _allocationEngine = new AllocationEngine(_context);
        _stockLedgerService = new StockLedgerService(_context);
        _walletService = new WalletService(_context);
        _loyaltyService = new LoyaltyService(_context);
        _offerEngine = new OfferEngine(_context, _memoryCache);

        _apHandler = new APCommandsAndQueriesHandler(_context, _postingService, _sequenceService, _allocationEngine, _approvalService);
        _arHandler = new ARCommandsAndQueriesHandler(_context, _postingService, _sequenceService, _allocationEngine);
        _invoiceHandler = new CreateInvoiceCommandHandler(_context, _offerEngine, _walletService, _loyaltyService, _postingService, _stockLedgerService, _passwordHasher);
        _closeSessionHandler = new ClosePosSessionCommandHandler(_context, _postingService);
        _returnHandler = new ReturnCommandsHandler(_context, _postingService, _sequenceService, _stockLedgerService);
        _transferHandler = new TransferCommandsHandler(_context, _postingService, _sequenceService, _stockLedgerService);

        // Seed test store, supplier, customer, and business date
        _storeId = Guid.NewGuid();
        _supplierId = Guid.NewGuid();
        _customerId = Guid.NewGuid();
        _userId = Guid.NewGuid();

        InitializeTestData().GetAwaiter().GetResult();
    }

    private static ApplicationDbContext GetDbContext()
    {
        var hosts = new[] { "10.26.198.140", "192.168.29.64", "localhost", "127.0.0.1" };
        string chosenHost = "localhost";
        
        foreach (var host in hosts)
        {
            try
            {
                var testConn = $"Host={host};Port=5432;Database=postgres;Username=posadmin;Password=pospassword;Timeout=2;";
                using var conn = new NpgsqlConnection(testConn);
                conn.Open();
                chosenHost = host;
                break;
            }
            catch
            {
                // try next
            }
        }

        var masterConnStr = $"Host={chosenHost};Port=5432;Database=postgres;Username=posadmin;Password=pospassword;";
        using (var conn = new NpgsqlConnection(masterConnStr))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT 1 FROM pg_database WHERE datname = 'posdb_integration_tests';";
            var exists = cmd.ExecuteScalar() != null;
            if (!exists)
            {
                cmd.CommandText = "CREATE DATABASE posdb_integration_tests;";
                cmd.ExecuteNonQuery();
            }
        }

        var testConnStr = $"Host={chosenHost};Port=5432;Database=posdb_integration_tests;Username=posadmin;Password=pospassword;";
        using (var conn = new NpgsqlConnection(testConnStr))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DROP SCHEMA IF EXISTS public CASCADE; CREATE SCHEMA public; GRANT ALL ON SCHEMA public TO posadmin; GRANT ALL ON SCHEMA public TO public;";
            cmd.ExecuteNonQuery();

            var migrationsDir = @"d:/JEGADISH/APPLE_SUPERMARKET_POS_PROJECT/AI_POWERED_POS_AND_ERP_SYSTEM/src/Backend/PosErp.Infrastructure/Persistence/Migrations";
            var sqlFiles = Directory.GetFiles(migrationsDir, "*.sql")
                .OrderBy(f => Path.GetFileName(f))
                .ToList();

            foreach (var file in sqlFiles)
            {
                var sql = File.ReadAllText(file);
                using var runCmd = conn.CreateCommand();
                runCmd.CommandText = sql;
                runCmd.ExecuteNonQuery();
            }

            // Run manual DDL patches from Program.cs required by current entity maps
            var patches = new[]
            {
                @"CREATE TABLE IF NOT EXISTS refresh_tokens (
                    id UUID PRIMARY KEY,
                    user_id UUID NOT NULL,
                    token VARCHAR(512) NOT NULL,
                    token_family VARCHAR(255) NOT NULL,
                    expires_at TIMESTAMP WITH TIME ZONE NOT NULL,
                    device_id VARCHAR(255) NOT NULL,
                    is_revoked BOOLEAN NOT NULL DEFAULT FALSE,
                    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
                );",
                @"ALTER TABLE invoices ADD COLUMN IF NOT EXISTS cash_amount   NUMERIC(18,2) NOT NULL DEFAULT 0;
                  ALTER TABLE invoices ADD COLUMN IF NOT EXISTS upi_amount    NUMERIC(18,2) NOT NULL DEFAULT 0;
                  ALTER TABLE invoices ADD COLUMN IF NOT EXISTS card_amount   NUMERIC(18,2) NOT NULL DEFAULT 0;
                  ALTER TABLE invoices ADD COLUMN IF NOT EXISTS wallet_amount NUMERIC(18,2) NOT NULL DEFAULT 0;",
                @"ALTER TABLE grn_items ADD COLUMN IF NOT EXISTS rejection_reason VARCHAR(500);",
                @"ALTER TABLE products ADD COLUMN IF NOT EXISTS has_expiry BOOLEAN DEFAULT TRUE;",
                @"CREATE TABLE IF NOT EXISTS pending_price_approvals (
                    id UUID PRIMARY KEY,
                    barcode VARCHAR(255) NOT NULL,
                    product_name VARCHAR(512) NOT NULL,
                    existing_cost_price NUMERIC(18,2) NOT NULL DEFAULT 0,
                    new_cost_price NUMERIC(18,2) NOT NULL DEFAULT 0,
                    quantity NUMERIC(18,2) NOT NULL DEFAULT 0,
                    invoice_reference VARCHAR(255) NOT NULL DEFAULT '',
                    status VARCHAR(50) NOT NULL DEFAULT 'PENDING',
                    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
                    actioned_at TIMESTAMP WITH TIME ZONE,
                    actioned_by UUID
                );"
            };

            foreach (var patch in patches)
            {
                using var runPatchCmd = conn.CreateCommand();
                runPatchCmd.CommandText = patch;
                runPatchCmd.ExecuteNonQuery();
            }
        }

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(testConnStr)
            .Options;

        return new ApplicationDbContext(options);
    }

    private async Task InitializeTestData()
    {
        // 1. Store
        var store = new Store
        {
            Id = _storeId,
            StoreCode = "STORE-001",
            StoreName = "Supermarket Test Branch 1",
            IsActive = true
        };
        _context.Stores.Add(store);

        // 2. User & Roles
        var ownerRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Owner") 
            ?? new Role { Id = Guid.NewGuid(), Name = "Owner", Description = "Owner Role" };
        if (_context.Entry(ownerRole).State == EntityState.Detached) _context.Roles.Add(ownerRole);

        var user = new User
        {
            Id = _userId,
            Username = "testowner@supermarket.local",
            PasswordHash = _passwordHasher.HashPassword("TestOwner@123"),
            PinHash = _passwordHasher.HashPassword("9999"),
            FullName = "Test Branch Owner",
            RoleId = ownerRole.Id,
            IsActive = true
        };
        _context.Users.Add(user);

        // 3. Supplier (NET30 terms)
        var supplier = new Supplier
        {
            Id = _supplierId,
            Name = "Priyanka Distributors Ltd",
            PaymentTerms = "NET30",
            IsActive = true
        };
        _context.Suppliers.Add(supplier);

        // 4. Customer with ₹10,000 credit limit
        var customer = new Customer
        {
            Id = _customerId,
            Name = "Aadhavan Kumar",
            Phone = "9988776655",
            CreditLimit = 10000.00m,
            MembershipCardNumber = "CARD-1001"
        };
        _context.Customers.Add(customer);

        // 5. Open Business Date
        var bizDate = await _context.StoreBusinessDates
            .FirstOrDefaultAsync(b => b.StoreId == Guid.Empty && b.BusinessDate == DateTime.UtcNow.Date);
        if (bizDate == null)
        {
            bizDate = new StoreBusinessDate
            {
                StoreId = Guid.Empty, // central date session
                BusinessDate = DateTime.UtcNow.Date,
                Status = "OPEN"
            };
            _context.StoreBusinessDates.Add(bizDate);
        }

        // Ensure default tax slab
        var taxSlab = await _context.TaxSlabs.FirstOrDefaultAsync() 
            ?? new TaxSlab { Id = Guid.NewGuid(), Name = "GST 18%", CgstRate = 9.0m, SgstRate = 9.0m, IgstRate = 18.0m, CessRate = 0.0m };
        if (_context.Entry(taxSlab).State == EntityState.Detached) _context.TaxSlabs.Add(taxSlab);

        await _context.SaveChangesAsync();
    }

    [Fact]
    public async Task Scenario1_Purchase_GRN_Bill_Payment_FullFlow()
    {
        // 1. Create a Product
        var product = new Product
        {
            Id = Guid.NewGuid(),
            ProductCode = "TSTPROD-01",
            Name = "Test Detergent 1kg",
            TaxSlabId = (await _context.TaxSlabs.FirstAsync()).Id,
            UnitOfMeasureId = Guid.Parse("a0000000-0000-0000-0000-000000000001"),
            Mrp = 200.00m,
            SellingPrice = 180.00m,
            PurchasePrice = 150.00m,
            IsWeighable = false,
            IsActive = true,
            HasExpiry = true
        };
        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        // 2. Mock a Purchase Order
        var po = new PurchaseOrderHeader
        {
            StoreId = _storeId,
            SupplierId = _supplierId,
            PoNumber = "PO-2026-0001",
            PoDate = DateTime.UtcNow.Date,
            ExpectedDeliveryDate = DateTime.UtcNow.Date.AddDays(7),
            Status = "APPROVED",
            TotalAmount = 15000.00m
        };
        po.Items.Add(new PurchaseOrderItem
        {
            ProductId = product.Id,
            OrderedQuantity = 100,
            UnitCost = 150.00m,
            TotalCost = 15000.00m
        });
        _context.PurchaseOrders.Add(po);
        await _context.SaveChangesAsync();

        // 3. Mock a GRN
        var grn = new GRNHeader
        {
            StoreId = _storeId,
            PurchaseOrderHeaderId = po.Id,
            SupplierId = _supplierId,
            GrnNumber = "GRN-2026-0001",
            SupplierInvoiceNumber = "SUPINV-8877",
            ReceivedDate = DateTime.UtcNow.Date,
            TotalAmount = 15000.00m,
            Status = "CONFIRMED"
        };
        grn.Items.Add(new GRNItem
        {
            ProductId = product.Id,
            PurchaseOrderItemId = po.Items.First().Id,
            ReceivedQuantity = 100,
            AcceptedQuantity = 100,
            RejectedQuantity = 0,
            UnitCost = 150.00m,
            TotalCost = 15000.00m,
            BatchNumber = "BATCH-DET-01",
            ExpiryDate = DateTime.UtcNow.Date.AddYears(1)
        });
        _context.GRNHeaders.Add(grn);
        await _context.SaveChangesAsync();

        // Create the product batch
        var pb = new ProductBatch
        {
            StoreId = _storeId,
            ProductId = product.Id,
            BatchNumber = "BATCH-DET-01",
            ExpiryDate = DateTime.UtcNow.Date.AddYears(1),
            CostPrice = 150.00m,
            Mrp = 200.00m,
            AvailableQuantity = 100,
            IsActive = true
        };
        _context.ProductBatches.Add(pb);
        await _context.SaveChangesAsync();

        // 4. Generate Purchase Bill
        var createBillCmd = new CreatePurchaseBillCommand(
            _storeId,
            grn.Id,
            "BILL-8877",
            DateTime.UtcNow.Date,
            _userId
        );

        var billId = await _apHandler.Handle(createBillCmd, CancellationToken.None);

        var bill = await _context.PurchaseBills.FindAsync(billId);
        Assert.NotNull(bill);
        Assert.Equal("PENDING_PAYMENT", bill.Status);
        
        // Assert due date is automatically calculated (BillDate + 30 credit days)
        Assert.Equal(bill.BillDate.AddDays(30), bill.DueDate);

        // Verify journal entries for Purchase Bill
        var je = await _context.JournalEntries
            .Include(j => j.Lines)
                .ThenInclude(l => l.Account)
            .FirstOrDefaultAsync(j => j.SourceDocumentId == billId && j.SourceDocumentType == "PURCHASE_BILL");
        Assert.NotNull(je);
        Assert.True(je.Lines.Count >= 2);
        
        // Balanced Check
        Assert.Equal(je.Lines.Sum(l => l.DebitAmount), je.Lines.Sum(l => l.CreditAmount));

        // 5. Process Supplier Payment (Scenario 1.5 - Partial Payment)
        // Bill total is 15000 + 18% tax (₹15,000 cost + ₹2,700 tax = ₹17,700 total)
        var partialPayCmd = new ProcessSupplierPaymentCommand(
            _storeId,
            _supplierId,
            DateTime.UtcNow.Date,
            "BANK_TRANSFER",
            "TXN-9988",
            10000.00m,
            "Partial Settlement",
            "AUTO_FIFO",
            null,
            _userId
        );

        var paymentId = await _apHandler.Handle(partialPayCmd, CancellationToken.None);

        // Re-fetch bill and check status is still PARTIALLY_PAID
        bill = await _context.PurchaseBills.FindAsync(billId);
        Assert.Equal("PARTIALLY_PAID", bill.Status);

        // Assert aging report correctly lists outstanding amount
        var agingReport = await _apHandler.Handle(new GetSupplierAgingReportQuery(_storeId, DateTime.UtcNow.Date), CancellationToken.None);
        var reportLine = agingReport.FirstOrDefault(a => a.SupplierId == _supplierId);
        Assert.NotNull(reportLine);
        Assert.Equal(7700.00m, reportLine.TotalOutstanding); // ₹17,700 bill - ₹10,000 partial payment

        // 6. Complete remaining payment
        var finalPayCmd = new ProcessSupplierPaymentCommand(
            _storeId,
            _supplierId,
            DateTime.UtcNow.Date,
            "BANK_TRANSFER",
            "TXN-9989",
            7700.00m,
            "Final Settlement",
            "AUTO_FIFO",
            null,
            _userId
        );

        await _apHandler.Handle(finalPayCmd, CancellationToken.None);

        bill = await _context.PurchaseBills.FindAsync(billId);
        Assert.Equal("PAID", bill.Status);
    }

    [Fact]
    public async Task Scenario2_POS_Credit_Sale_And_Returns_FullFlow()
    {
        // 1. Create a Product
        var product = new Product
        {
            Id = Guid.NewGuid(),
            ProductCode = "TSTPROD-02",
            Name = "Premium Rice 5kg",
            TaxSlabId = (await _context.TaxSlabs.FirstAsync()).Id,
            UnitOfMeasureId = Guid.Parse("a0000000-0000-0000-0000-000000000001"),
            Mrp = 500.00m,
            SellingPrice = 450.00m,
            PurchasePrice = 350.00m,
            IsWeighable = false,
            IsActive = true,
            HasExpiry = false
        };
        _context.Products.Add(product);

        var batch = new ProductBatch
        {
            StoreId = Guid.Empty,
            ProductId = product.Id,
            BatchNumber = "BATCH-RICE-01",
            CostPrice = 350.00m,
            Mrp = 500.00m,
            AvailableQuantity = 50,
            IsActive = true
        };
        _context.ProductBatches.Add(batch);
        await _context.SaveChangesAsync();

        // 2. Validate Credit Limit Block
        // Attempt a credit sale that exceeds the ₹10,000 credit limit (e.g. ₹12,000)
        var itemsExceeding = new List<InvoiceItemDto>
        {
            new(product.Id, 30, 450.00m, batch.Id) // Total 30 * 450 = 13,500
        };

        var exceedCmd = new CreateInvoiceCommand(
            "INV-TX-001",
            Guid.Parse("00000000-0000-0000-0000-000000000001"),
            _userId,
            _customerId,
            null,
            0,
            0,
            0,
            0,
            0,
            13500.00m,
            "CREDIT",
            itemsExceeding
        );

        await Assert.ThrowsAnyAsync<Exception>(async () => await _invoiceHandler.Handle(exceedCmd, CancellationToken.None));

        // 3. Successful Credit Sale within Limit
        var itemsValid = new List<InvoiceItemDto>
        {
            new(product.Id, 2, 450.00m, batch.Id) // Total 2 * 450 = 900 (under 1000 threshold to prevent discount application)
        };

        var validCmd = new CreateInvoiceCommand(
            "INV-TX-002",
            Guid.Parse("00000000-0000-0000-0000-000000000001"),
            _userId,
            _customerId,
            null,
            0,
            0,
            0,
            0,
            0,
            900.00m,
            "CREDIT",
            itemsValid
        );

        var invoiceId = await _invoiceHandler.Handle(validCmd, CancellationToken.None);
        var invoice = await _context.Invoices.FindAsync(invoiceId, DateTime.UtcNow.Date);
        Assert.NotNull(invoice);

        // Verify Customer Ledger running balance increased
        var ledgerBal = await _context.CustomerLedger
            .Where(c => c.CustomerId == _customerId)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => c.RunningBalance)
            .FirstOrDefaultAsync();
        Assert.Equal(900.00m, ledgerBal);

        // 4. Scenario 2.5: Partial Customer Receipt Allocation
        var partialReceiptCmd = new ProcessCustomerReceiptCommand(
            Guid.Empty,
            _customerId,
            DateTime.UtcNow.Date,
            "UPI",
            "UPI-TXN-01",
            200.00m,
            "Partial receipt payment",
            "AUTO_FIFO",
            null,
            _userId
        );

        var receiptId = await _arHandler.Handle(partialReceiptCmd, CancellationToken.None);

        var arAging = await _arHandler.Handle(new GetCustomerAgingReportQuery(Guid.Empty, DateTime.UtcNow.Date), CancellationToken.None);
        var arLine = arAging.FirstOrDefault(c => c.CustomerId == _customerId);
        Assert.NotNull(arLine);
        Assert.Equal(700.00m, arLine.TotalOutstanding); // ₹900 - ₹200 = ₹700 outstanding

        // 5. Scenario 3: Sales Return
        var returnCmd = new ProcessSalesReturnCommand(
            Guid.Empty,
            invoiceId,
            DateTime.UtcNow.Date,
            "CREDIT_NOTE",
            new List<SalesReturnItemInputDto>
            {
                new(product.Id, batch.Id, 1) // Return 1 unit (refund ₹450)
            },
            _userId
        );

        var salesReturnId = await _returnHandler.Handle(returnCmd, CancellationToken.None);
        var salesReturn = await _context.SalesReturns.FindAsync(salesReturnId);
        Assert.NotNull(salesReturn);
        Assert.Equal(450.00m, salesReturn.TotalAmount);

        // Verify batch was restocked
        var restockedBatch = await _context.ProductBatches.FindAsync(batch.Id);
        Assert.Equal(49, restockedBatch.AvailableQuantity); // 50 original - 2 sold + 1 returned = 49

        // Verify double entry for COGS and Revenue reversals
        var returnJe = await _context.JournalEntries
            .Include(j => j.Lines)
                .ThenInclude(l => l.Account)
            .FirstOrDefaultAsync(j => j.SourceDocumentId == salesReturnId && j.SourceDocumentType == "SALES_RETURN");
        Assert.NotNull(returnJe);
        Assert.True(returnJe.Lines.Any(l => l.Account.AccountCode == "4000" && l.DebitAmount > 0)); // Sales Revenue Debit
        Assert.True(returnJe.Lines.Any(l => l.Account.AccountCode == "10300" && l.DebitAmount > 0)); // Inventory restock Debit
        Assert.True(returnJe.Lines.Any(l => l.Account.AccountCode == "5000" && l.CreditAmount > 0)); // COGS Credit
    }

    [Fact]
    public async Task Scenario4_Purchase_Return_FullFlow()
    {
        // 1. Create a Product and Batch
        var product = new Product
        {
            Id = Guid.NewGuid(),
            ProductCode = "TSTPROD-04",
            Name = "Organic Honey 500g",
            TaxSlabId = (await _context.TaxSlabs.FirstAsync()).Id,
            UnitOfMeasureId = Guid.Parse("a0000000-0000-0000-0000-000000000001"),
            Mrp = 300.00m,
            SellingPrice = 280.00m,
            PurchasePrice = 220.00m,
            IsWeighable = false,
            IsActive = true
        };
        _context.Products.Add(product);

        var batch = new ProductBatch
        {
            StoreId = _storeId,
            ProductId = product.Id,
            BatchNumber = "BATCH-HONEY-01",
            CostPrice = 220.00m,
            Mrp = 300.00m,
            AvailableQuantity = 30,
            IsActive = true
        };
        _context.ProductBatches.Add(batch);
        await _context.SaveChangesAsync();

        // 2. Perform Purchase Return
        var purchaseReturnCmd = new ProcessPurchaseReturnCommand(
            _storeId,
            _supplierId,
            null,
            DateTime.UtcNow.Date,
            new List<PurchaseReturnItemInputDto>
            {
                new(product.Id, batch.Id, 10) // Return 10 honey jars
            },
            _userId
        );

        var returnId = await _returnHandler.Handle(purchaseReturnCmd, CancellationToken.None);

        var purchaseReturn = await _context.PurchaseReturns.FindAsync(returnId);
        Assert.NotNull(purchaseReturn);
        Assert.Equal(2200.00m, purchaseReturn.SubTotal); // 10 * 220 = 2200

        // Verify stock reduced
        var updatedBatch = await _context.ProductBatches.FindAsync(batch.Id);
        Assert.Equal(20, updatedBatch.AvailableQuantity); // 30 - 10 = 20

        // Verify Supplier Ledger got Debit Note
        var ledgerEntry = await _context.SupplierLedger
            .Where(s => s.SupplierId == _supplierId && s.TransactionType == "DEBIT_NOTE")
            .FirstOrDefaultAsync();
        Assert.NotNull(ledgerEntry);
        Assert.Equal(purchaseReturn.TotalAmount, ledgerEntry.DebitAmount);
    }

    [Fact]
    public async Task Scenario5_Cashier_Short_Over_Discrepancy()
    {
        // 1. Create a POS Session
        var session = new PosSession
        {
            Id = Guid.NewGuid(),
            TerminalId = Guid.NewGuid(),
            CashierId = _userId,
            StartTime = DateTime.UtcNow.AddHours(-4),
            OpeningFloatCash = 2000.00m,
            ExpectedClosingCash = 0,
            ActualClosingCash = 0,
            Status = "OPEN"
        };
        _context.PosSessions.Add(session);
        await _context.SaveChangesAsync();

        // 2. Close session with shortage (expected = 2000, actual = 1950, discrepancy = -50)
        var closeCmd = new ClosePosSessionCommand(session.Id, 1950.00m);
        var success = await _closeSessionHandler.Handle(closeCmd, CancellationToken.None);
        Assert.True(success);

        var closedSession = await _context.PosSessions.FindAsync(session.Id);
        Assert.Equal("CLOSED", closedSession.Status);
        Assert.Equal(-50.00m, closedSession.Difference);

        // Verify discrepancy GL entry (Debit Cash Drawer Shortage Expense, Credit Cash)
        var je = await _context.JournalEntries
            .Include(j => j.Lines)
                .ThenInclude(l => l.Account)
            .FirstOrDefaultAsync(j => j.ReferenceDocument == $"SES-{session.Id}");
        Assert.NotNull(je);
        Assert.True(je.Lines.Any(l => l.Account.AccountCode == "5200" && l.DebitAmount == 50.00m));
        Assert.True(je.Lines.Any(l => l.Account.AccountCode == "1000" && l.CreditAmount == 50.00m));
    }

    [Fact]
    public async Task Scenario6_InterStore_Inventory_Transfer()
    {
        // 1. Create source and destination stores
        var destStoreId = Guid.NewGuid();
        var destStore = new Store
        {
            Id = destStoreId,
            StoreCode = "STORE-002",
            StoreName = "Supermarket Test Branch 2",
            IsActive = true
        };
        _context.Stores.Add(destStore);

        // Create product and source batch
        var product = new Product
        {
            Id = Guid.NewGuid(),
            ProductCode = "TSTPROD-06",
            Name = "Chocolate Biscuit Pack",
            TaxSlabId = (await _context.TaxSlabs.FirstAsync()).Id,
            UnitOfMeasureId = Guid.Parse("a0000000-0000-0000-0000-000000000001"),
            Mrp = 80.00m,
            SellingPrice = 75.00m,
            PurchasePrice = 60.00m,
            IsActive = true
        };
        _context.Products.Add(product);

        var sourceBatch = new ProductBatch
        {
            StoreId = _storeId,
            ProductId = product.Id,
            BatchNumber = "BATCH-BISCUIT-01",
            CostPrice = 60.00m,
            Mrp = 80.00m,
            AvailableQuantity = 100,
            IsActive = true
        };
        _context.ProductBatches.Add(sourceBatch);
        await _context.SaveChangesAsync();

        // 2. Perform Transfer
        var transferCmd = new ProcessInterStoreTransferCommand(
            _storeId,
            destStoreId,
            DateTime.UtcNow.Date,
            new List<TransferItemInputDto>
            {
                new(product.Id, sourceBatch.Id, 40) // transfer 40 biscuit packs
            },
            _userId
        );

        var transferId = await _transferHandler.Handle(transferCmd, CancellationToken.None);

        var transfer = await _context.InterStoreTransfers.FindAsync(transferId);
        Assert.NotNull(transfer);

        // Verify Store A batch reduced
        var updatedSrcBatch = await _context.ProductBatches.FindAsync(sourceBatch.Id);
        Assert.Equal(60, updatedSrcBatch.AvailableQuantity); // 100 - 40 = 60

        // Verify Store B batch created and has 40 units
        var destBatch = await _context.ProductBatches
            .FirstOrDefaultAsync(b => b.ProductId == product.Id && b.StoreId == destStoreId && b.BatchNumber == "BATCH-BISCUIT-01");
        Assert.NotNull(destBatch);
        Assert.Equal(40, destBatch.AvailableQuantity);

        // Verify two separate Store-wise Journal entries were created
        var jes = await _context.JournalEntries
            .Include(j => j.Lines)
                .ThenInclude(l => l.Account)
            .Where(j => j.SourceDocumentId == transferId && j.SourceDocumentType.Contains("TRANSFER"))
            .ToListAsync();
        Assert.Equal(2, jes.Count);

        var srcJe = jes.FirstOrDefault(j => j.StoreId == _storeId);
        var destJe = jes.FirstOrDefault(j => j.StoreId == destStoreId);
        Assert.NotNull(srcJe);
        Assert.NotNull(destJe);

        // Store A: Credit Inventory Asset 10300 (Value: 40 * 60 = 2400)
        Assert.Contains(srcJe.Lines, l => l.Account.AccountCode == "10300" && l.CreditAmount == 2400.00m);
        // Store B: Debit Inventory Asset 10300 (Value: 2400)
        Assert.Contains(destJe.Lines, l => l.Account.AccountCode == "10300" && l.DebitAmount == 2400.00m);
    }

    public void Dispose()
    {
        _context.Dispose();
        _memoryCache.Dispose();
    }
}
