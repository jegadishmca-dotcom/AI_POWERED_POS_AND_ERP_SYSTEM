using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using PosErp.Application.Interfaces;
using PosErp.Application.Features.Catalog.Commands.CreateProduct;
using PosErp.Application.Features.Catalog.Commands.UpdateProduct;
using PosErp.Application.Features.Catalog.Commands.ImportProducts;
using PosErp.Application.Features.Pos.Commands.SyncInvoices;
using PosErp.Application.Features.Audit.Services;
using PosErp.Application.Features.Crm.Services;
using PosErp.Application.Features.Finance.Services;
using PosErp.Application.Features.Inventory.Services;
using PosErp.Application.Features.Offers.Services;
using PosErp.Domain.Entities.Crm;
using PosErp.Domain.Entities.Auth;
using PosErp.Domain.Entities.Catalog;
using PosErp.Domain.Entities.Inventory;
using PosErp.Domain.Entities.Pos;
using PosErp.Domain.Entities.Finance;
using PosErp.Infrastructure.Identity;
using PosErp.Infrastructure.Persistence;
using PosErp.Infrastructure.Services;
using PosErp.Api.Controllers;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace PosErp.IntegrationTests
{
    [Collection("Database Collection")]
    public class F49_PricingAndSearchTests : IDisposable
    {
        private readonly ApplicationDbContext _context;
        private readonly CreateProductCommandHandler _createHandler;
        private readonly UpdateProductCommandHandler _updateHandler;
        private readonly ImportProductsCommandHandler _importHandler;
        private readonly CreateInvoiceCommandHandler _invoiceHandler;
        private readonly PosController _posController;
        private readonly HttpContextAccessorMock _httpContextAccessor;

        private readonly Guid _storeId = Guid.NewGuid();
        private readonly Guid _terminalId = Guid.NewGuid();
        private readonly Guid _cashierId = Guid.NewGuid();
        private readonly Guid _managerId = Guid.NewGuid();
        private readonly Guid _customerId = Guid.NewGuid();
        private readonly Guid _productId = Guid.NewGuid();
        private readonly Guid _taxSlabId = Guid.NewGuid();

        public F49_PricingAndSearchTests()
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
            var accountRes = new AccountResolutionService(_context);

            _createHandler = new CreateProductCommandHandler(_context);
            _updateHandler = new UpdateProductCommandHandler(_context);
            _importHandler = new ImportProductsCommandHandler(_context);
            _invoiceHandler = new CreateInvoiceCommandHandler(
                _context, offerEng, walletSvc, loyaltySvc, posting, stockSvc, hasher, accountRes, _httpContextAccessor);
            
            var auditLogger = new AuditLoggingService(_context, new TenantProviderMock(), _httpContextAccessor);
            _posController = new PosController(null, null, _context, null, auditLogger);

            SeedAsync().GetAwaiter().GetResult();
        }

        private async Task SeedAsync()
        {
            var ownerRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Owner")
                ?? new Role { Id = Guid.NewGuid(), Name = "Owner", Description = "Owner role" };
            if (_context.Entry(ownerRole).State == EntityState.Detached)
                _context.Roles.Add(ownerRole);

            var cashierRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Cashier")
                ?? new Role { Id = Guid.NewGuid(), Name = "Cashier", Description = "Cashier role" };
            if (_context.Entry(cashierRole).State == EntityState.Detached)
                _context.Roles.Add(cashierRole);

            var managerRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Manager")
                ?? new Role { Id = Guid.NewGuid(), Name = "Manager", Description = "Manager role" };
            if (_context.Entry(managerRole).State == EntityState.Detached)
                _context.Roles.Add(managerRole);

            var hasher = new PasswordHasher();
            _context.Users.Add(new User
            {
                Id = _managerId,
                Username = "manager_test_f49",
                PasswordHash = hasher.HashPassword("Test@1234"),
                PinHash = hasher.HashPassword("4321"), // manager pin override
                RoleId = ownerRole.Id,
                IsActive = true,
                FullName = "Test Manager"
            });

            _context.Users.Add(new User
            {
                Id = _cashierId,
                Username = "cashier_test_f49",
                PasswordHash = hasher.HashPassword("Test@1234"),
                RoleId = cashierRole.Id,
                IsActive = true,
                FullName = "Test Cashier"
            });

            var store = new Store { Id = _storeId, StoreName = "Test Store F49", StoreCode = "TSTF49", IsActive = true };
            _context.Stores.Add(store);

            var terminal = new Terminal { Id = _terminalId, Name = "T49", TerminalCode = "TST-T49", IsActive = true };
            _context.Terminals.Add(terminal);

            var customer = new Customer { Id = _customerId, Name = "F49 Customer", Phone = "9999949494", CreditLimit = 50000m };
            _context.Customers.Add(customer);

            var taxSlab = new TaxSlab { Id = _taxSlabId, Name = "0% GST", CgstRate = 0m, SgstRate = 0m, CessRate = 0m };
            _context.TaxSlabs.Add(taxSlab);

            var uom = await _context.UnitOfMeasures.FirstOrDefaultAsync(u => u.Symbol == "PCS");
            if (uom == null)
            {
                uom = new UnitOfMeasure { Id = Guid.NewGuid(), Symbol = "PCS", Name = "Pieces" };
                _context.UnitOfMeasures.Add(uom);
            }

            var product = new Product
            {
                Id = _productId,
                Name = "Base Product F49",
                ProductCode = "PROD-F49",
                TaxSlabId = _taxSlabId,
                UnitOfMeasureId = uom.Id,
                Mrp = 100m,
                SellingPrice = 90m,
                PurchasePrice = 50m,
                IsActive = true
            };
            product.Barcodes.Add(new Barcode { Id = Guid.NewGuid(), BarcodeValue = "49000000001", IsPrimary = true });
            _context.Products.Add(product);

            // Clean up old business dates to avoid multiple open dates
            var openDates = await _context.StoreBusinessDates.ToListAsync();
            foreach (var d in openDates)
            {
                _context.StoreBusinessDates.Remove(d);
            }

            _context.StoreBusinessDates.Add(new StoreBusinessDate
            {
                StoreId = Guid.Empty,
                BusinessDate = DateTime.UtcNow.Date,
                Status = "OPEN"
            });

            // Seed financial accounts
            var codesToSeed = new[] { "10100", "10300", "10400", "20200", "22010", "22020", "40100", "50100" };
            foreach (var code in codesToSeed)
            {
                if (!await _context.Accounts.AnyAsync(a => a.AccountCode == code))
                {
                    _context.Accounts.Add(new Account
                    {
                        Id = Guid.NewGuid(),
                        AccountCode = code,
                        Name = $"Account {code}",
                        AccountType = code.StartsWith("1") ? "ASSET" : code.StartsWith("2") ? "LIABILITY" : code.StartsWith("4") ? "REVENUE" : "EXPENSE",
                        IsActive = true
                    });
                }
            }

            await _context.SaveChangesAsync();
        }

        [Fact]
        public async Task CreateProduct_InvalidPrices_ThrowsException()
        {
            var uom = await _context.UnitOfMeasures.FirstAsync();
            var tax = await _context.TaxSlabs.FirstAsync();

            // 1. SellingPrice <= 0
            var cmd1 = new CreateProductCommand("P-E1", "Err1", null, null, 100m, 0m, 50m, "E123", tax.Id, null, uom.Id);
            await Assert.ThrowsAsync<ArgumentException>(() => _createHandler.Handle(cmd1, CancellationToken.None));

            // 2. MRP <= 0
            var cmd2 = new CreateProductCommand("P-E2", "Err2", null, null, 0m, 90m, 50m, "E124", tax.Id, null, uom.Id);
            await Assert.ThrowsAsync<ArgumentException>(() => _createHandler.Handle(cmd2, CancellationToken.None));

            // 3. PurchasePrice < 0
            var cmd3 = new CreateProductCommand("P-E3", "Err3", null, null, 100m, 90m, -1m, "E125", tax.Id, null, uom.Id);
            await Assert.ThrowsAsync<ArgumentException>(() => _createHandler.Handle(cmd3, CancellationToken.None));

            // 4. SellingPrice > MRP
            var cmd4 = new CreateProductCommand("P-E4", "Err4", null, null, 80m, 90m, 50m, "E126", tax.Id, null, uom.Id);
            await Assert.ThrowsAsync<ArgumentException>(() => _createHandler.Handle(cmd4, CancellationToken.None));
        }

        [Fact]
        public async Task UpdateProduct_InvalidPrices_ThrowsException()
        {
            var uom = await _context.UnitOfMeasures.FirstAsync();
            var tax = await _context.TaxSlabs.FirstAsync();

            // Try to update base product with selling price > MRP
            var cmd = new UpdateProductCommand(_productId, "PROD-F49", "Base Product F49", null, null, 100m, 110m, 50m, "49000000001", tax.Id, null, uom.Id);
            await Assert.ThrowsAsync<ArgumentException>(() => _updateHandler.Handle(cmd, CancellationToken.None));
        }

        [Fact]
        public async Task ImportProducts_InvalidPrices_RecordsFailedCountAndErrors()
        {
            // CSV content with header and 1 valid product, 1 product with 0 SellingPrice
            var csvContent = "ProductCode,Name,Mrp,SellingPrice,PurchasePrice,Barcode\r\n" +
                             "P-OK,Valid Prod,100,90,50,OK123\r\n" +
                             "P-BAD,Invalid Prod,100,0,50,BAD123";

            var fileMock = new FormFileMock(csvContent);
            var cmd = new ImportProductsCommand(fileMock);

            var result = await _importHandler.Handle(cmd, CancellationToken.None);

            Assert.Equal(1, result.TotalFailed);
            Assert.Contains(result.Errors, e => e.Contains("Line 3: Selling Price and MRP must be greater than zero"));
        }

        [Fact]
        public async Task CreateInvoice_ZeroRateItem_WithoutOverride_ThrowsException()
        {
            _httpContextAccessor.SetUser(_managerId, "Owner");

            var items = new List<InvoiceItemDto>
            {
                new(_productId, 1m, 0m, null) // zero rate selling
            };

            var cmd = new CreateInvoiceCommand(
                "TEST-ZERO-1", _terminalId, _cashierId, _customerId, null, 0m, 0m, 0m, 0m, 0m, 0m, "CASH", items, 0, null);

            var ex = await Assert.ThrowsAsync<Exception>(() => _invoiceHandler.Handle(cmd, CancellationToken.None));
            Assert.Contains("ZERO_RATE_LIMIT", ex.Message);
        }

        [Fact]
        public async Task CreateInvoice_ZeroRateItem_WithCorrectOverride_Succeeds()
        {
            _httpContextAccessor.SetUser(_managerId, "Owner");

            var items = new List<InvoiceItemDto>
            {
                new(_productId, 1m, 0m, null)
            };

            // "4321" is manager PIN seeded in SeedAsync
            var cmd = new CreateInvoiceCommand(
                "TEST-ZERO-2", _terminalId, _cashierId, _customerId, null, 0m, 0m, 0m, 0m, 0m, 0m, "CASH", items, 0, "4321");

            var response = await _invoiceHandler.Handle(cmd, CancellationToken.None);
            Assert.NotNull(response);
            Assert.Equal("TEST-ZERO-2", response.InvoiceNumber);
        }

        [Fact]
        public async Task CreateInvoice_ZeroRateItem_WithIncorrectOverride_ThrowsException()
        {
            _httpContextAccessor.SetUser(_managerId, "Owner");

            var items = new List<InvoiceItemDto>
            {
                new(_productId, 1m, 0m, null)
            };

            var cmd = new CreateInvoiceCommand(
                "TEST-ZERO-3", _terminalId, _cashierId, _customerId, null, 0m, 0m, 0m, 0m, 0m, 0m, "CASH", items, 0, "WRONG_PIN");

            var ex = await Assert.ThrowsAsync<Exception>(() => _invoiceHandler.Handle(cmd, CancellationToken.None));
            Assert.Contains("ZERO_RATE_LIMIT", ex.Message);
        }

        [Fact]
        public async Task SearchInvoices_MultipleCriteria_MatchesCorrectly()
        {
            // Seed a test invoice to search
            var testInvoiceId = Guid.NewGuid();
            var invoice = new Invoice
            {
                Id = testInvoiceId,
                InvoiceNumber = "INV-TST-T49-20260710-9999",
                TerminalId = _terminalId,
                CashierId = _cashierId,
                CustomerId = _customerId,
                BusinessDate = DateTime.UtcNow.Date,
                SubTotal = 90m,
                DiscountAmount = 0m,
                TaxAmount = 0m,
                TotalAmount = 90m,
                NetPayable = 90m,
                PaymentMode = "CASH",
                Status = "COMPLETED"
            };
            invoice.Items.Add(new InvoiceItem
            {
                Id = Guid.NewGuid(),
                InvoiceId = testInvoiceId,
                ProductId = _productId,
                ProductName = "Base Product F49",
                Barcode = "49000000001",
                Quantity = 1m,
                UnitPrice = 90m,
                BusinessDate = DateTime.UtcNow.Date
            });
            _context.Invoices.Add(invoice);
            await _context.SaveChangesAsync();

            // 1. Search by Suffix
            var action1 = await _posController.SearchInvoices("9999", CancellationToken.None);
            var okResult1 = Assert.IsType<OkObjectResult>(action1);
            var list1 = Assert.IsAssignableFrom<System.Collections.IEnumerable>(okResult1.Value);
            Assert.NotEmpty(list1);

            // 2. Search by Customer Phone
            var action2 = await _posController.SearchInvoices("9999949494", CancellationToken.None);
            var okResult2 = Assert.IsType<OkObjectResult>(action2);
            var list2 = Assert.IsAssignableFrom<System.Collections.IEnumerable>(okResult2.Value);
            Assert.NotEmpty(list2);

            // 3. Search by Product Barcode
            var action3 = await _posController.SearchInvoices("49000000001", CancellationToken.None);
            var okResult3 = Assert.IsType<OkObjectResult>(action3);
            var list3 = Assert.IsAssignableFrom<System.Collections.IEnumerable>(okResult3.Value);
            Assert.NotEmpty(list3);

            // 4. Search by Customer Name
            var action4 = await _posController.SearchInvoices("F49 Customer", CancellationToken.None);
            var okResult4 = Assert.IsType<OkObjectResult>(action4);
            var list4 = Assert.IsAssignableFrom<System.Collections.IEnumerable>(okResult4.Value);
            Assert.NotEmpty(list4);
        }

        [Fact]
        public async Task Test_CustomerRegistration_Email_Validation()
        {
            var handler = new PosErp.Application.Features.Crm.Commands.RegisterCustomer.RegisterCustomerCommandHandler(_context);

            // 1. Register customer with a valid email
            var phone1 = "9988776655";
            var cmd1 = new PosErp.Application.Features.Crm.Commands.RegisterCustomer.RegisterCustomerCommand(phone1, "Valid Email Cust", null, "test@example.com", null, false);
            var id1 = await handler.Handle(cmd1, CancellationToken.None);
            Assert.NotEqual(Guid.Empty, id1);

            var cust1 = await _context.Customers.FirstOrDefaultAsync(c => c.Id == id1);
            Assert.NotNull(cust1);
            Assert.Equal("test@example.com", cust1.Email);

            // 2. Register customer with an invalid email (should throw ArgumentException)
            var phone2 = "9988776656";
            var cmd2 = new PosErp.Application.Features.Crm.Commands.RegisterCustomer.RegisterCustomerCommand(phone2, "Invalid Email Cust", null, "invalid-email-format", null, false);
            await Assert.ThrowsAsync<ArgumentException>(() => handler.Handle(cmd2, CancellationToken.None));

            // 3. Register customer without email (should succeed)
            var phone3 = "9988776657";
            var cmd3 = new PosErp.Application.Features.Crm.Commands.RegisterCustomer.RegisterCustomerCommand(phone3, "No Email Cust", null, null, null, false);
            var id3 = await handler.Handle(cmd3, CancellationToken.None);
            Assert.NotEqual(Guid.Empty, id3);

            var cust3 = await _context.Customers.FirstOrDefaultAsync(c => c.Id == id3);
            Assert.NotNull(cust3);
            Assert.Null(cust3.Email);

            // 4. Register customer with too long email (should throw ArgumentException)
            var phone4 = "9988776658";
            var longEmail = new string('a', 250) + "@test.com"; // 259 chars
            var cmd4 = new PosErp.Application.Features.Crm.Commands.RegisterCustomer.RegisterCustomerCommand(phone4, "Long Email Cust", null, longEmail, null, false);
            await Assert.ThrowsAsync<ArgumentException>(() => handler.Handle(cmd4, CancellationToken.None));

            // Cleanup
            var cleanPhones = new[] { phone1, phone3 };
            var toClean = await _context.Customers.Where(c => cleanPhones.Contains(c.Phone)).ToListAsync();
            _context.Customers.RemoveRange(toClean);
            await _context.SaveChangesAsync();
        }

        public void Dispose()
        {
            // Clean up test invoices
            var testInvoices = _context.Invoices.Where(i => i.InvoiceNumber.Contains("TEST-ZERO") || i.InvoiceNumber.Contains("9999"));
            _context.Invoices.RemoveRange(testInvoices);
            _context.SaveChanges();
        }
    }

    public class FormFileMock : IFormFile
    {
        private readonly byte[] _content;
        public FormFileMock(string content)
        {
            _content = System.Text.Encoding.UTF8.GetBytes(content);
        }
        public string ContentType => "text/csv";
        public string ContentDisposition => "";
        public IHeaderDictionary Headers => new HeaderDictionary();
        public long Length => _content.Length;
        public string Name => "file";
        public string FileName => "products.csv";
        public Stream OpenReadStream() => new MemoryStream(_content);
        public void CopyTo(Stream target) => target.Write(_content, 0, _content.Length);
        public Task CopyToAsync(Stream target, CancellationToken cancellationToken = default)
        {
            return target.WriteAsync(_content, 0, _content.Length, cancellationToken);
        }
    }
}
