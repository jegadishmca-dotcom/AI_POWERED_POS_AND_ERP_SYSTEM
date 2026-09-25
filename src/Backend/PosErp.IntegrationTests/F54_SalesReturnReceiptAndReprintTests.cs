using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using PosErp.Api.Controllers;
using PosErp.Application.Features.Finance.Queries;
using PosErp.Application.Interfaces;
using PosErp.Domain.Entities.Auth;
using PosErp.Domain.Entities.Catalog;
using PosErp.Infrastructure.Printing;
using PosErp.Domain.Entities.Crm;
using PosErp.Domain.Entities.Finance;
using PosErp.Domain.Entities.Inventory;
using PosErp.Domain.Entities.Pos;
using PosErp.Application.Features.Inventory.Services;
using PosErp.Application.Features.Finance.Services;
using PosErp.Infrastructure.Identity;
using Microsoft.Extensions.Configuration;
using PosErp.Infrastructure.Persistence;
using Xunit;

namespace PosErp.IntegrationTests;

/// <summary>
/// Tests for Issue 1: Sales return thermal receipt printing and reprint flow.
/// Validates:
/// 1. GetSalesReturnsQuery correctly filters by caller store_id (store scoping).
/// 2. GetSalesReturnsQuery correctly filters by date range (fromDate, toDate).
/// 3. GetReturnDetails returns all required thermal receipt fields (ReturnNumber, OriginalInvoiceNumber, CustomerName, CustomerPhone, Items, RefundAmount, Cashier, Terminal).
/// 4. ProcessReturn enforces caller's store_id from JWT claims, preventing cross-store return spoofing.
/// 5. Full ASP.NET Core middleware HTTP tests verify 200 OK with matching store token and isolated empty results with foreign store token.
/// </summary>
[Collection("Database Collection")]
public class F54_SalesReturnReceiptAndReprintTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly GetSalesReturnsQueryHandler _listHandler;
    private readonly GetSalesReturnByIdQueryHandler _detailHandler;

    private readonly Guid _storeId = Guid.NewGuid();
    private readonly Guid _foreignStoreId = Guid.NewGuid();
    private readonly Guid _terminalId = Guid.NewGuid();
    private readonly Guid _cashierId = Guid.NewGuid();
    private readonly Guid _ownerId = Guid.NewGuid();
    private readonly Guid _customerId = Guid.NewGuid();
    private readonly Guid _productId = Guid.NewGuid();
    private readonly Guid _batchId = Guid.NewGuid();
    private readonly Guid _taxSlabId = Guid.NewGuid();
    private readonly Guid _uomId = Guid.NewGuid();
    private readonly Guid _categoryId = Guid.NewGuid();

    private readonly Guid _invoiceId = Guid.NewGuid();
    private readonly Guid _salesReturnId = Guid.NewGuid();
    private readonly Guid _foreignSalesReturnId = Guid.NewGuid();
    private readonly string _returnNumber = "RET-F54-0001";
    private readonly string _foreignReturnNumber = "RET-F54-0002";
    private readonly string _invoiceNumber = "INV-F54-0001";

    public F54_SalesReturnReceiptAndReprintTests()
    {
        _context = IntegrationTestDbFactory.Build();
        _listHandler = new GetSalesReturnsQueryHandler(_context);
        _detailHandler = new GetSalesReturnByIdQueryHandler(_context);
        SeedAsync().GetAwaiter().GetResult();
    }

    private async Task SeedAsync()
    {
        // 1. Stores
        if (!await _context.Stores.AnyAsync(s => s.Id == _storeId))
        {
            _context.Stores.Add(new Store
            {
                Id = _storeId,
                StoreName = "F54 Test Store 1",
                StoreCode = "F54STR1",
                IsActive = true
            });
        }
        if (!await _context.Stores.AnyAsync(s => s.Id == _foreignStoreId))
        {
            _context.Stores.Add(new Store
            {
                Id = _foreignStoreId,
                StoreName = "F54 Test Store 2",
                StoreCode = "F54STR2",
                IsActive = true
            });
        }

        // 2. Tax Slab
        if (!await _context.TaxSlabs.AnyAsync(t => t.Id == _taxSlabId))
        {
            _context.TaxSlabs.Add(new TaxSlab
            {
                Id = _taxSlabId,
                Name = "GST 5% F54",
                CgstRate = 2.5m,
                SgstRate = 2.5m,
                CessRate = 0m
            });
        }

        // 3. UOM
        var uom = await _context.UnitOfMeasures.FirstOrDefaultAsync(u => u.Symbol == "PCS");
        if (uom == null)
        {
            uom = new UnitOfMeasure { Id = _uomId, Symbol = "PCS", Name = "Pieces" };
            _context.UnitOfMeasures.Add(uom);
        }

        // 4. Category
        var cat = await _context.Categories.FirstOrDefaultAsync(c => c.Name == "General F54");
        if (cat == null)
        {
            cat = new Category { Id = _categoryId, Name = "General F54" };
            _context.Categories.Add(cat);
        }

        // 5. Terminal
        if (!await _context.Terminals.AnyAsync(t => t.Id == _terminalId))
        {
            _context.Terminals.Add(new Terminal
            {
                Id = _terminalId,
                TerminalCode = "POS-F54-01",
                Name = "Checkout Terminal 1",
                IsActive = true
            });
        }

        // 6. Role & Cashier
        var role = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Cashier");
        if (role == null)
        {
            role = new Role { Id = Guid.NewGuid(), Name = "Cashier" };
            _context.Roles.Add(role);
        }

        if (!await _context.Users.AnyAsync(u => u.Id == _cashierId))
        {
            _context.Users.Add(new User
            {
                Id = _cashierId,
                Username = "cashier_f54",
                FullName = "Ramesh Cashier",
                PasswordHash = "dummy_hash",
                RoleId = role.Id,
                IsActive = true,
                StoreId = _storeId
            });
        }

        var ownerRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Owner");
        if (ownerRole == null)
        {
            ownerRole = new Role { Id = Guid.NewGuid(), Name = "Owner" };
            _context.Roles.Add(ownerRole);
        }

        if (!await _context.Users.AnyAsync(u => u.Id == _ownerId))
        {
            _context.Users.Add(new User
            {
                Id = _ownerId,
                Username = "owner_f54",
                FullName = "System Owner",
                PasswordHash = "dummy_hash",
                RoleId = ownerRole.Id,
                IsActive = true,
                StoreId = null
            });
        }

        // 7. Customer
        if (!await _context.Customers.AnyAsync(c => c.Id == _customerId))
        {
            _context.Customers.Add(new Customer
            {
                Id = _customerId,
                Name = "Murugan Pillai",
                Phone = "9876543210"
            });
        }

        await _context.SaveChangesAsync();

        // 8. Product
        if (!await _context.Products.AnyAsync(p => p.Id == _productId))
        {
            var product = new Product
            {
                Id = _productId,
                StoreId = _storeId,
                ProductCode = "PRD-F54-001",
                Name = "Apple Basmati Rice 5kg",
                TamilName = "ஆப்பிள் பாசுமதி அரிசி 5கிலோ",
                TaxSlabId = _taxSlabId,
                CategoryId = cat.Id,
                UnitOfMeasureId = uom.Id,
                PurchasePrice = 300.00m,
                SellingPrice = 450.00m,
                Mrp = 480.00m,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            product.Barcodes.Add(new Barcode
            {
                Id = Guid.NewGuid(),
                ProductId = _productId,
                BarcodeValue = "8901234567890",
                IsPrimary = true
            });
            _context.Products.Add(product);
        }

        // 9. Batch
        if (!await _context.ProductBatches.AnyAsync(b => b.Id == _batchId))
        {
            _context.ProductBatches.Add(new ProductBatch
            {
                Id = _batchId,
                ProductId = _productId,
                StoreId = _storeId,
                BatchNumber = "BATCH-F54-01",
                ExpiryDate = DateTime.UtcNow.AddMonths(12),
                AvailableQuantity = 100,
                CostPrice = 300.00m,
                Mrp = 480.00m
            });
        }

        // 10. Completed Sale Invoice
        if (!await _context.Invoices.AnyAsync(i => i.Id == _invoiceId))
        {
            var inv = new Invoice
            {
                Id = _invoiceId,
                StoreId = _storeId,
                TerminalId = _terminalId,
                CashierId = _cashierId,
                CustomerId = _customerId,
                InvoiceNumber = _invoiceNumber,
                BusinessDate = DateTime.Today,
                SubTotal = 428.57m,
                TaxAmount = 21.43m,
                TotalAmount = 450.00m,
                NetPayable = 450.00m,
                Status = "COMPLETED",
                PaymentMode = "CASH",
                CashAmount = 500.00m,
                CreatedAt = DateTime.UtcNow
            };
            inv.Items.Add(new InvoiceItem
            {
                Id = Guid.NewGuid(),
                InvoiceId = _invoiceId,
                ProductId = _productId,
                ProductName = "Apple Basmati Rice 5kg",
                Barcode = "8901234567890",
                Quantity = 1,
                UnitPrice = 450.00m,
                TotalAmount = 450.00m
            });
            _context.Invoices.Add(inv);
        }

        // 11. Sales Return for Store 1
        if (!await _context.SalesReturns.AnyAsync(sr => sr.Id == _salesReturnId))
        {
            var sr = new SalesReturn
            {
                Id = _salesReturnId,
                StoreId = _storeId,
                InvoiceId = _invoiceId,
                ReturnNumber = _returnNumber,
                ReturnDate = DateTime.Today,
                BusinessDate = DateTime.Today,
                SubTotal = 428.57m,
                TaxAmount = 21.43m,
                TotalAmount = 450.00m,
                RefundAmount = 450.00m,
                RefundMode = "CASH",
                Status = "COMPLETED",
                CreatedBy = _cashierId,
                CreatedAt = DateTime.UtcNow
            };
            sr.Items.Add(new SalesReturnItem
            {
                Id = Guid.NewGuid(),
                SalesReturnId = _salesReturnId,
                ProductId = _productId,
                BatchId = _batchId,
                Quantity = 1,
                UnitPrice = 450.00m,
                TaxAmount = 21.43m,
                TotalAmount = 450.00m
            });
            _context.SalesReturns.Add(sr);
        }

        // 12. Foreign Sales Return for Store 2
        if (!await _context.SalesReturns.AnyAsync(sr => sr.Id == _foreignSalesReturnId))
        {
            var srForeign = new SalesReturn
            {
                Id = _foreignSalesReturnId,
                StoreId = _foreignStoreId,
                InvoiceId = _invoiceId,
                ReturnNumber = _foreignReturnNumber,
                ReturnDate = DateTime.Today,
                BusinessDate = DateTime.Today,
                SubTotal = 428.57m,
                TaxAmount = 21.43m,
                TotalAmount = 450.00m,
                RefundAmount = 450.00m,
                RefundMode = "UPI",
                Status = "COMPLETED",
                CreatedBy = _cashierId,
                CreatedAt = DateTime.UtcNow
            };
            srForeign.Items.Add(new SalesReturnItem
            {
                Id = Guid.NewGuid(),
                SalesReturnId = _foreignSalesReturnId,
                ProductId = _productId,
                BatchId = _batchId,
                Quantity = 1,
                UnitPrice = 450.00m,
                TaxAmount = 21.43m,
                TotalAmount = 450.00m
            });
            _context.SalesReturns.Add(srForeign);
        }

        await _context.SaveChangesAsync();
    }

    [Fact]
    public async Task GetSalesReturnsQuery_StoreScoping_FiltersByStoreId()
    {
        // 1. Caller with _storeId only sees returns belonging to _storeId
        var queryStore1 = new GetSalesReturnsQuery(
            FromDate: DateTime.Today.AddDays(-1),
            ToDate: DateTime.Today.AddDays(1),
            Limit: 50,
            StoreId: _storeId
        );
        var resultStore1 = await _listHandler.Handle(queryStore1, CancellationToken.None);

        Assert.NotEmpty(resultStore1);
        Assert.All(resultStore1, ret => Assert.Equal(_storeId, ret.StoreId));
        Assert.Contains(resultStore1, r => r.ReturnNumber == _returnNumber);
        Assert.DoesNotContain(resultStore1, r => r.ReturnNumber == _foreignReturnNumber);

        // 2. Caller with _foreignStoreId only sees Store 2 returns
        var queryStore2 = new GetSalesReturnsQuery(
            FromDate: DateTime.Today.AddDays(-1),
            ToDate: DateTime.Today.AddDays(1),
            Limit: 50,
            StoreId: _foreignStoreId
        );
        var resultStore2 = await _listHandler.Handle(queryStore2, CancellationToken.None);

        Assert.NotEmpty(resultStore2);
        Assert.All(resultStore2, ret => Assert.Equal(_foreignStoreId, ret.StoreId));
        Assert.Contains(resultStore2, r => r.ReturnNumber == _foreignReturnNumber);
        Assert.DoesNotContain(resultStore2, r => r.ReturnNumber == _returnNumber);
    }

    [Fact]
    public async Task GetSalesReturnsQuery_DateRange_FiltersCorrectly()
    {
        // Query yesterday's window (should exclude today's return)
        var pastDate = DateTime.Today.AddDays(-10);
        var queryPast = new GetSalesReturnsQuery(
            FromDate: pastDate,
            ToDate: pastDate.AddDays(1),
            Limit: 50,
            StoreId: _storeId
        );
        var resultPast = await _listHandler.Handle(queryPast, CancellationToken.None);
        Assert.Empty(resultPast);

        // Query today's window (should include today's return)
        var queryToday = new GetSalesReturnsQuery(
            FromDate: DateTime.Today,
            ToDate: DateTime.Today,
            Limit: 50,
            StoreId: _storeId
        );
        var resultToday = await _listHandler.Handle(queryToday, CancellationToken.None);
        Assert.Contains(resultToday, r => r.ReturnNumber == _returnNumber);
    }

    [Fact]
    public async Task GetReturnDetails_ReturnsAllRequiredReceiptFields()
    {
        // Must contain all fields necessary for printing a valid thermal sales return receipt
        var result = await _detailHandler.Handle(new GetSalesReturnByIdQuery(_salesReturnId.ToString(), _storeId), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(_salesReturnId, result!.Id);
        Assert.Equal(_returnNumber, result.ReturnNumber);
        Assert.Equal(_invoiceNumber, result.OriginalInvoiceNumber);
        Assert.Equal("Murugan Pillai", result.CustomerName);
        Assert.Equal("9876543210", result.CustomerPhone);
        Assert.Equal("CASH", result.RefundMode);
        Assert.Equal(450.00m, result.RefundAmount);
        Assert.Equal("Ramesh Cashier", result.CashierName);
        Assert.Equal("POS-F54-01", result.TerminalCode);

        // Line items
        Assert.Single(result.Items);
        var item = result.Items[0];
        Assert.Equal("Apple Basmati Rice 5kg", item.ProductName);
        Assert.Equal(1, item.Quantity);
        Assert.Equal(450.00m, item.UnitPrice);
        Assert.Equal(450.00m, item.TotalAmount);
    }

    private const string JwtSecret = "SuperSecretPosErpIntegrationTestingKey_2026_SecureKey!";

    private TestServer CreateTestServer(IPrintService? printService = null)
    {
        var webHost = new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddRouting();
                services.AddControllers().AddApplicationPart(typeof(AccountsReceivableController).Assembly);
                services.AddScoped<IApplicationDbContext>(_ => IntegrationTestDbFactory.CreateNewContext());
                services.AddLogging();
                services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(GetSalesReturnsQuery).Assembly));
                services.AddScoped<IPasswordHasher, PasswordHasher>();
                services.AddScoped<IPeriodLockService, PeriodLockService>();
                services.AddScoped<IDocumentSequenceService, DocumentSequenceService>();
                services.AddScoped<IApprovalWorkflowService, ApprovalWorkflowService>();
                services.AddScoped<IFinancialPostingService, FinancialPostingService>();
                services.AddScoped<IStockLedgerService, StockLedgerService>();
                services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
                if (printService != null)
                {
                    services.AddSingleton<IPrintService>(printService);
                }

                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                })
                .AddJwtBearer(options =>
                {
                    options.RequireHttpsMetadata = false;
                    options.SaveToken = true;
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ValidIssuer = "PosErp",
                        ValidAudience = "PosErpClient",
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSecret))
                    };
                });

                services.AddAuthorization();
            })
            .Configure(app =>
            {
                app.UseRouting();
                app.UseAuthentication();
                app.UseAuthorization();
                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapControllers();
                });
            });

        return new TestServer(webHost);
    }

    private string GenerateTestJwtToken(string role, Guid? storeId = null)
    {
        var userId = (role == "Owner" || role == "Admin") ? _ownerId : _cashierId;
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Name, "testuser"),
            new Claim(ClaimTypes.Role, role)
        };

        if (storeId.HasValue)
        {
            claims.Add(new Claim("store_id", storeId.Value.ToString()));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: "PosErp",
            audience: "PosErpClient",
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    [Fact]
    public async Task GetSalesReturns_HttpEndpoint_StoreIsolation_ThroughMiddleware()
    {
        using var server = CreateTestServer();
        var client = server.CreateClient();

        // 1. Request with Store 1 token -> sees Store 1 return
        var tokenStore1 = GenerateTestJwtToken("Cashier", _storeId);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenStore1);

        var responseStore1 = await client.GetAsync("/api/AccountsReceivable/returns");
        Assert.Equal(HttpStatusCode.OK, responseStore1.StatusCode);

        var json1 = await responseStore1.Content.ReadAsStringAsync();
        Assert.Contains(_returnNumber, json1);
        Assert.DoesNotContain(_foreignReturnNumber, json1);

        // 2. Request with Store 2 token -> sees Store 2 return, not Store 1
        var tokenStore2 = GenerateTestJwtToken("Cashier", _foreignStoreId);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenStore2);

        var responseStore2 = await client.GetAsync("/api/AccountsReceivable/returns");
        Assert.Equal(HttpStatusCode.OK, responseStore2.StatusCode);

        var json2 = await responseStore2.Content.ReadAsStringAsync();
        Assert.Contains(_foreignReturnNumber, json2);
        Assert.DoesNotContain(_returnNumber, json2);
    }

    [Fact]
    public async Task GetReturnDetails_HttpEndpoint_ReturnsFullReceiptPayload_ForPrinting()
    {
        using var server = CreateTestServer();
        var client = server.CreateClient();

        var token = GenerateTestJwtToken("Cashier", _storeId);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync($"/api/AccountsReceivable/returns/{_salesReturnId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains(_returnNumber, json);
        Assert.Contains(_invoiceNumber, json);
        Assert.Contains("Apple Basmati Rice 5kg", json);
        Assert.Contains("Murugan Pillai", json);
        Assert.Contains("9876543210", json);
        Assert.Contains("450", json);
        Assert.Contains("CASH", json);
    }

    [Fact]
    public async Task PrintReturnReceipt_HttpEndpoint_StoreIsolation_ThroughMiddleware()
    {
        var mockPrint = new MockPrintService();
        using var server = CreateTestServer(mockPrint);
        var client = server.CreateClient();

        // 1. Caller with matching Store 1 token -> prints successfully (200 OK)
        var tokenStore1 = GenerateTestJwtToken("Cashier", _storeId);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenStore1);

        var responseOk = await client.PostAsync($"/api/AccountsReceivable/returns/{_salesReturnId}/print?printerIp=192.168.1.100", null);
        Assert.Equal(HttpStatusCode.OK, responseOk.StatusCode);
        Assert.Single(mockPrint.Prints);
        Assert.Contains(_returnNumber, mockPrint.Prints[0].Text);
        Assert.Contains(_invoiceNumber, mockPrint.Prints[0].Text);
        Assert.Contains("Apple Basmati Rice", mockPrint.Prints[0].Text);
        Assert.Contains("450.00", mockPrint.Prints[0].Text);

        // 2. Caller with foreign Store 2 token attempting to print Store 1 return -> 404 NotFound (store isolation)
        var tokenStore2 = GenerateTestJwtToken("Cashier", _foreignStoreId);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenStore2);

        var responseForbidden = await client.PostAsync($"/api/AccountsReceivable/returns/{_salesReturnId}/print?printerIp=192.168.1.100", null);
        Assert.Equal(HttpStatusCode.NotFound, responseForbidden.StatusCode);

        // 3. Unauthenticated call -> 401 Unauthorized
        client.DefaultRequestHeaders.Authorization = null;
        var responseUnauth = await client.PostAsync($"/api/AccountsReceivable/returns/{_salesReturnId}/print?printerIp=192.168.1.100", null);
        Assert.Equal(HttpStatusCode.Unauthorized, responseUnauth.StatusCode);
    }

    [Fact]
    public async Task GetSalesReturns_OwnerAdminWithoutStoreClaim_ReturnsCrossStoreReturns()
    {
        using var server = CreateTestServer();
        var client = server.CreateClient();

        // Token with Role = "Owner", but NO store_id claim
        var tokenOwnerGlobal = GenerateTestJwtToken("Owner", storeId: null);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenOwnerGlobal);

        var response = await client.GetAsync("/api/AccountsReceivable/returns");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        // Global owner sees returns from both Store 1 and Store 2
        Assert.Contains(_returnNumber, json);
        Assert.Contains(_foreignReturnNumber, json);
    }

    [Fact]
    public async Task GetSalesReturns_NonOwnerWithoutStoreClaim_IsDeniedAccess()
    {
        using var server = CreateTestServer();
        var client = server.CreateClient();

        // Non-Owner/Admin role (e.g. "Cashier") with missing store_id claim
        var tokenCashierNoStore = GenerateTestJwtToken("Cashier", storeId: null);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenCashierNoStore);

        // 1. GET /api/AccountsReceivable/returns -> returns 200 with empty list (fail-closed, NOT cross-store!)
        var responseList = await client.GetAsync("/api/AccountsReceivable/returns");
        Assert.Equal(HttpStatusCode.OK, responseList.StatusCode);
        var jsonList = await responseList.Content.ReadAsStringAsync();
        Assert.DoesNotContain(_returnNumber, jsonList);
        Assert.DoesNotContain(_foreignReturnNumber, jsonList);
        Assert.Equal("[]", jsonList.Trim());

        // 2. GET /api/AccountsReceivable/returns/{id} -> 404 NotFound
        var responseDetails = await client.GetAsync($"/api/AccountsReceivable/returns/{_salesReturnId}");
        Assert.Equal(HttpStatusCode.NotFound, responseDetails.StatusCode);

        // 3. POST /api/AccountsReceivable/returns/{id}/print -> 404 NotFound
        var responsePrint = await client.PostAsync($"/api/AccountsReceivable/returns/{_salesReturnId}/print?printerIp=192.168.1.100", null);
        Assert.Equal(HttpStatusCode.NotFound, responsePrint.StatusCode);

        // 4. POST /api/AccountsReceivable/returns (ProcessReturn) -> 403 Forbidden
        var processPayload = new StringContent(
            System.Text.Json.JsonSerializer.Serialize(new
            {
                storeId = _foreignStoreId,
                invoiceId = Guid.NewGuid(),
                returnDate = DateTime.UtcNow,
                refundMode = "CASH",
                items = new List<object>()
            }),
            System.Text.Encoding.UTF8,
            "application/json"
        );
        var responseProcess = await client.PostAsync("/api/AccountsReceivable/returns", processPayload);
        Assert.Equal(HttpStatusCode.Forbidden, responseProcess.StatusCode);
    }

    [Fact]
    public async Task ProcessReturn_GlobalOwner_RejectsMismatchedStoreId_AndAnchorsToInvoiceStore()
    {
        using var server = CreateTestServer();
        var client = server.CreateClient();

        // Token with Role = "Owner", but NO store_id claim (Global scope)
        var tokenOwnerGlobal = GenerateTestJwtToken("Owner", storeId: null);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenOwnerGlobal);

        // 1. Owner attempts to return an invoice from Store 1 while passing Store 2 in request.StoreId:
        // System MUST reject with 400 Bad Request ("Store mismatch") and NOT allow cross-store redirection.
        var mismatchedPayload = new StringContent(
            System.Text.Json.JsonSerializer.Serialize(new
            {
                storeId = _foreignStoreId, // Mismatched Store 2!
                invoiceId = _invoiceId,    // Belongs to Store 1!
                returnDate = DateTime.UtcNow,
                refundMode = "CASH",
                items = new List<object>
                {
                    new
                    {
                        productId = _productId,
                        quantity = 1m
                    }
                }
            }),
            System.Text.Encoding.UTF8,
            "application/json"
        );

        var responseMismatched = await client.PostAsync("/api/AccountsReceivable/returns", mismatchedPayload);
        Assert.Equal(HttpStatusCode.BadRequest, responseMismatched.StatusCode);
        var errJson = await responseMismatched.Content.ReadAsStringAsync();
        Assert.Contains("Store mismatch", errJson);
    }

    [Fact]
    public async Task ProcessReturn_GlobalOwner_RejectsStorelessInvoice_WhenNoStoreProvided()
    {
        var storelessInvId = Guid.NewGuid();
        var storelessInv = new Invoice
        {
            Id = storelessInvId,
            StoreId = null,
            TerminalId = _terminalId,
            CashierId = _cashierId,
            CustomerId = _customerId,
            InvoiceNumber = "INV-F54-NOSTORE",
            BusinessDate = DateTime.Today,
            SubTotal = 100m,
            TaxAmount = 5m,
            TotalAmount = 105m,
            NetPayable = 105m,
            Status = "COMPLETED",
            PaymentMode = "CASH",
            TerminalSequence = 88888,
            CreatedAt = DateTime.UtcNow
        };
        storelessInv.Items.Add(new InvoiceItem
        {
            Id = Guid.NewGuid(),
            InvoiceId = storelessInvId,
            ProductId = _productId,
            ProductName = "Apple Basmati Rice 5kg",
            Quantity = 1,
            UnitPrice = 105m,
            TotalAmount = 105m
        });
        _context.Invoices.Add(storelessInv);
        await _context.SaveChangesAsync();

        try
        {
            using var server = CreateTestServer();
            var client = server.CreateClient();

            var tokenOwnerGlobal = GenerateTestJwtToken("Owner", storeId: null);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenOwnerGlobal);

            var payload = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(new
                {
                    storeId = Guid.Empty,
                    invoiceId = storelessInvId,
                    returnDate = DateTime.UtcNow,
                    refundMode = "CASH",
                    items = new List<object>
                    {
                        new { productId = _productId, quantity = 1m }
                    }
                }),
                System.Text.Encoding.UTF8,
                "application/json"
            );

            var response = await client.PostAsync("/api/AccountsReceivable/returns", payload);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var errJson = await response.Content.ReadAsStringAsync();
            Assert.Contains("Cannot determine store for this return — invoice has no store and none was provided.", errJson);
        }
        finally
        {
            var inv = _context.Invoices.Include(i => i.Items).FirstOrDefault(i => i.Id == storelessInvId);
            if (inv != null)
            {
                _context.InvoiceItems.RemoveRange(inv.Items);
                _context.Invoices.Remove(inv);
                await _context.SaveChangesAsync();
            }
        }
    }

    private class MockPrintService : IPrintService
    {
        public List<(string Ip, int Port, string Text)> Prints { get; } = new();

        public Task PrintReceiptAsync(string printerIp, int port, string textContent)
        {
            Prints.Add((printerIp, port, textContent));
            return Task.CompletedTask;
        }
    }

    public void Dispose()
    {
        try
        {
            var rets = _context.SalesReturns.Include(sr => sr.Items)
                .Where(sr => sr.Id == _salesReturnId || sr.Id == _foreignSalesReturnId)
                .ToList();
            if (rets.Any())
            {
                foreach (var r in rets)
                {
                    _context.SalesReturnItems.RemoveRange(r.Items);
                }
                _context.SalesReturns.RemoveRange(rets);
            }

            var inv = _context.Invoices.Include(i => i.Items).FirstOrDefault(i => i.Id == _invoiceId);
            if (inv != null)
            {
                _context.InvoiceItems.RemoveRange(inv.Items);
                _context.Invoices.Remove(inv);
            }

            var batch = _context.ProductBatches.FirstOrDefault(b => b.Id == _batchId);
            if (batch != null) _context.ProductBatches.Remove(batch);

            var prd = _context.Products.Include(p => p.Barcodes).FirstOrDefault(p => p.Id == _productId);
            if (prd != null)
            {
                _context.Barcodes.RemoveRange(prd.Barcodes);
                _context.Products.Remove(prd);
            }

            var cust = _context.Customers.FirstOrDefault(c => c.Id == _customerId);
            if (cust != null) _context.Customers.Remove(cust);

            var usr = _context.Users.FirstOrDefault(u => u.Id == _cashierId);
            if (usr != null) _context.Users.Remove(usr);

            var ownerUsr = _context.Users.FirstOrDefault(u => u.Id == _ownerId);
            if (ownerUsr != null) _context.Users.Remove(ownerUsr);

            var term = _context.Terminals.FirstOrDefault(t => t.Id == _terminalId);
            if (term != null) _context.Terminals.Remove(term);

            var uom = _context.UnitOfMeasures.FirstOrDefault(u => u.Id == _uomId);
            if (uom != null) _context.UnitOfMeasures.Remove(uom);

            var cat = _context.Categories.FirstOrDefault(c => c.Id == _categoryId);
            if (cat != null) _context.Categories.Remove(cat);

            var slab = _context.TaxSlabs.FirstOrDefault(s => s.Id == _taxSlabId);
            if (slab != null) _context.TaxSlabs.Remove(slab);

            var str1 = _context.Stores.FirstOrDefault(s => s.Id == _storeId);
            if (str1 != null) _context.Stores.Remove(str1);

            var str2 = _context.Stores.FirstOrDefault(s => s.Id == _foreignStoreId);
            if (str2 != null) _context.Stores.Remove(str2);

            _context.SaveChanges();
        }
        catch { }
        finally
        {
            _context.Dispose();
        }
    }
}
