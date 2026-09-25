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
using PosErp.Domain.Entities.Crm;
using PosErp.Domain.Entities.Finance;
using PosErp.Domain.Entities.Inventory;
using PosErp.Domain.Entities.Pos;
using PosErp.Infrastructure.Persistence;
using Xunit;

namespace PosErp.IntegrationTests;

/// <summary>
/// Tests for Issue 4: Stock Ledger SALES_RETURN preview support.
/// Validates:
/// 1. Querying return details by GUID returns full payload (return info, invoice ref, customer, items, batch, financials).
/// 2. Querying return details by ReturnNumber is case-insensitive.
/// 3. Querying return details with "CAN-" prefix (cancellation reference document) resolves the return.
/// 4. Non-existent returns return null / 404.
/// 5. Endpoint inherits controller-level [Authorize] attribute with Cashier/Manager/Supervisor/Admin/Owner roles.
/// </summary>
[Collection("Database Collection")]
public class F53_SalesReturnDocumentPreviewTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly GetSalesReturnByIdQueryHandler _handler;

    private readonly Guid _storeId = Guid.NewGuid();
    private readonly Guid _terminalId = Guid.NewGuid();
    private readonly Guid _cashierId = Guid.NewGuid();
    private readonly Guid _customerId = Guid.NewGuid();
    private readonly Guid _productId = Guid.NewGuid();
    private readonly Guid _batchId = Guid.NewGuid();
    private readonly Guid _taxSlabId = Guid.NewGuid();
    private readonly Guid _uomId = Guid.NewGuid();
    private readonly Guid _categoryId = Guid.NewGuid();

    private readonly Guid _invoiceId = Guid.NewGuid();
    private readonly Guid _salesReturnId = Guid.NewGuid();
    private readonly string _returnNumber = "RET-F53-0001";
    private readonly string _invoiceNumber = "INV-F53-0001";

    public F53_SalesReturnDocumentPreviewTests()
    {
        _context = IntegrationTestDbFactory.Build();
        _handler = new GetSalesReturnByIdQueryHandler(_context);
        SeedAsync().GetAwaiter().GetResult();
    }

    private async Task SeedAsync()
    {
        // Store
        if (!await _context.Stores.AnyAsync(s => s.Id == _storeId))
        {
            _context.Stores.Add(new Store
            {
                Id = _storeId,
                StoreName = "F53 Test Store",
                StoreCode = "F53STR",
                IsActive = true
            });
        }

        // Tax Slab
        if (!await _context.TaxSlabs.AnyAsync(t => t.Id == _taxSlabId))
        {
            _context.TaxSlabs.Add(new TaxSlab
            {
                Id = _taxSlabId,
                Name = "GST 5%",
                CgstRate = 2.5m,
                SgstRate = 2.5m,
                CessRate = 0m
            });
        }

        // UOM
        var uom = await _context.UnitOfMeasures.FirstOrDefaultAsync(u => u.Symbol == "NOS");
        if (uom == null)
        {
            uom = new UnitOfMeasure { Id = _uomId, Symbol = "NOS", Name = "Numbers" };
            _context.UnitOfMeasures.Add(uom);
        }

        // Category
        var cat = await _context.Categories.FirstOrDefaultAsync(c => c.Name == "F53 Category");
        if (cat == null)
        {
            cat = new Category { Id = _categoryId, Name = "F53 Category" };
            _context.Categories.Add(cat);
        }

        // Terminal
        if (!await _context.Terminals.AnyAsync(t => t.Id == _terminalId))
        {
            _context.Terminals.Add(new Terminal
            {
                Id = _terminalId,
                TerminalCode = "POS-F53",
                Name = "POS Register F53",
                IsActive = true
            });
        }

        // Role & User / Cashier
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
                StoreId = _storeId,
                Username = "cashier_f53",
                FullName = "Priya Sharma",
                PasswordHash = "dummyhash",
                RoleId = role.Id,
                IsActive = true
            });
        }

        // Customer
        if (!await _context.Customers.AnyAsync(c => c.Id == _customerId))
        {
            _context.Customers.Add(new Customer
            {
                Id = _customerId,
                Name = "Ravi Shankar",
                Phone = "9876543210"
            });
        }

        await _context.SaveChangesAsync();

        // Product
        if (!await _context.Products.AnyAsync(p => p.Id == _productId))
        {
            var product = new Product
            {
                Id = _productId,
                StoreId = _storeId,
                ProductCode = "PRD-F53-01",
                Name = "Premium Basmati Rice 1kg",
                CategoryId = cat.Id,
                TaxSlabId = _taxSlabId,
                UnitOfMeasureId = uom.Id,
                Mrp = 150.0m,
                SellingPrice = 140.0m,
                PurchasePrice = 110.0m,
                IsActive = true
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

        // ProductBatch
        if (!await _context.ProductBatches.AnyAsync(b => b.Id == _batchId))
        {
            _context.ProductBatches.Add(new ProductBatch
            {
                Id = _batchId,
                ProductId = _productId,
                BatchNumber = "BAT-2026-F53",
                CostPrice = 110.0m,
                Mrp = 150.0m,
                AvailableQuantity = 100,
                CreatedAt = DateTime.UtcNow
            });
        }

        await _context.SaveChangesAsync();

        // Original Invoice
        if (!await _context.Invoices.AnyAsync(i => i.Id == _invoiceId))
        {
            var invoice = new Invoice
            {
                Id = _invoiceId,
                StoreId = _storeId,
                TerminalId = _terminalId,
                CashierId = _cashierId,
                CustomerId = _customerId,
                BusinessDate = new DateTime(2026, 9, 25),
                InvoiceNumber = _invoiceNumber,
                SubTotal = 266.67m,
                TaxAmount = 13.33m,
                TotalAmount = 280.00m,
                NetPayable = 280.00m,
                Status = "COMPLETED",
                PaymentMode = "CASH",
                CashAmount = 280.00m,
                CreatedAt = DateTime.UtcNow
            };
            invoice.Items.Add(new InvoiceItem
            {
                Id = Guid.NewGuid(),
                InvoiceId = _invoiceId,
                ProductId = _productId,
                ProductName = "Premium Basmati Rice 1kg",
                Barcode = "8901234567890",
                Quantity = 2,
                UnitPrice = 140.0m,
                TotalAmount = 280.0m,
                CgstRate = 2.5m,
                CgstAmount = 6.67m,
                SgstRate = 2.5m,
                SgstAmount = 6.66m
            });
            _context.Invoices.Add(invoice);
        }

        await _context.SaveChangesAsync();

        // Sales Return
        if (!await _context.SalesReturns.AnyAsync(sr => sr.Id == _salesReturnId))
        {
            var salesReturn = new SalesReturn
            {
                Id = _salesReturnId,
                StoreId = _storeId,
                InvoiceId = _invoiceId,
                BusinessDate = new DateTime(2026, 9, 25),
                ReturnNumber = _returnNumber,
                ReturnDate = new DateTime(2026, 9, 25),
                SubTotal = 133.33m,
                TaxAmount = 6.67m,
                TotalAmount = 140.00m,
                RefundAmount = 140.00m,
                RefundMode = "CASH",
                Status = "COMPLETED",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = _cashierId
            };
            salesReturn.Items.Add(new SalesReturnItem
            {
                Id = Guid.NewGuid(),
                SalesReturnId = _salesReturnId,
                ProductId = _productId,
                BatchId = _batchId,
                Quantity = 1,
                UnitPrice = 140.0m,
                TaxAmount = 6.67m,
                TotalAmount = 140.0m
            });
            _context.SalesReturns.Add(salesReturn);
        }

        await _context.SaveChangesAsync();
    }

    [Fact]
    public async Task GetReturnDetails_ByGuid_ReturnsFullDocumentPayload()
    {
        var result = await _handler.Handle(new GetSalesReturnByIdQuery(_salesReturnId.ToString()), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(_salesReturnId, result.Id);
        Assert.Equal(_returnNumber, result.ReturnNumber);
        Assert.Equal(_invoiceNumber, result.OriginalInvoiceNumber);
        Assert.Equal("Ravi Shankar", result.CustomerName);
        Assert.Equal("9876543210", result.CustomerPhone);
        Assert.Equal("POS-F53", result.TerminalCode);
        Assert.Equal("Priya Sharma", result.CreatedByName);
        Assert.Equal("CASH", result.RefundMode);
        Assert.Equal("COMPLETED", result.Status);
        Assert.Equal(140.00m, result.RefundAmount);

        // Verify items
        Assert.Single(result.Items);
        var item = result.Items[0];
        Assert.Equal("Premium Basmati Rice 1kg", item.ProductName);
        Assert.Equal("PRD-F53-01", item.ProductCode);
        Assert.Equal("8901234567890", item.Barcode);
        Assert.Equal("BAT-2026-F53", item.BatchNumber);
        Assert.Equal(1, item.Quantity);
        Assert.Equal(140.0m, item.UnitPrice);
        Assert.Equal(6.67m, item.TaxAmount);
        Assert.Equal(140.0m, item.TotalAmount);
    }

    [Fact]
    public async Task GetReturnDetails_ByReturnNumber_CaseInsensitive()
    {
        // Lowercase query
        var resLower = await _handler.Handle(new GetSalesReturnByIdQuery(_returnNumber.ToLower()), CancellationToken.None);
        Assert.NotNull(resLower);
        Assert.Equal(_salesReturnId, resLower.Id);

        // Uppercase query
        var resUpper = await _handler.Handle(new GetSalesReturnByIdQuery(_returnNumber.ToUpper()), CancellationToken.None);
        Assert.NotNull(resUpper);
        Assert.Equal(_salesReturnId, resUpper.Id);
    }

    [Fact]
    public async Task GetReturnDetails_ByCancellationReference_CanPrefixResolvesReturn()
    {
        // Stock Ledger creates reference_number "CAN-RET-..." for return cancellations
        var res = await _handler.Handle(new GetSalesReturnByIdQuery($"CAN-{_returnNumber}"), CancellationToken.None);
        Assert.NotNull(res);
        Assert.Equal(_salesReturnId, res.Id);
        Assert.Equal(_returnNumber, res.ReturnNumber);
    }

    [Fact]
    public async Task GetReturnDetails_NotFound_ReturnsNull()
    {
        var randomGuid = Guid.NewGuid().ToString();
        var res1 = await _handler.Handle(new GetSalesReturnByIdQuery(randomGuid), CancellationToken.None);
        Assert.Null(res1);

        var res2 = await _handler.Handle(new GetSalesReturnByIdQuery("NONEXISTENT-RETURN-9999"), CancellationToken.None);
        Assert.Null(res2);
    }

    [Fact]
    public async Task GetSalesReturnByIdQuery_StoreScoping_FiltersByStoreId()
    {
        // 1. With matching StoreId -> returns sales return DTO
        var matchingResult = await _handler.Handle(new GetSalesReturnByIdQuery(_salesReturnId.ToString(), _storeId), CancellationToken.None);
        Assert.NotNull(matchingResult);
        Assert.Equal(_salesReturnId, matchingResult!.Id);

        // 2. With foreign StoreId -> returns null
        var foreignStoreId = Guid.NewGuid();
        var foreignResult = await _handler.Handle(new GetSalesReturnByIdQuery(_salesReturnId.ToString(), foreignStoreId), CancellationToken.None);
        Assert.Null(foreignResult);
    }

    [Fact]
    public void AccountsReceivableController_InheritsRoleAuthorization()
    {
        var controllerType = typeof(AccountsReceivableController);
        var authAttr = controllerType.GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(authAttr);
        Assert.False(string.IsNullOrEmpty(authAttr.Roles));
        
        var roles = authAttr.Roles.Split(',').Select(r => r.Trim()).ToList();
        Assert.Contains("Cashier", roles);
        Assert.Contains("Manager", roles);
        Assert.Contains("Supervisor", roles);
        Assert.Contains("Admin", roles);
        Assert.Contains("Owner", roles);

        var method = controllerType.GetMethod(nameof(AccountsReceivableController.GetReturnDetails));
        Assert.NotNull(method);

        // Verify method does not bypass controller authorization with [AllowAnonymous]
        var allowAnonymous = method.GetCustomAttribute<AllowAnonymousAttribute>();
        Assert.Null(allowAnonymous);
    }

    private const string JwtSecret = "SuperSecretPosErpIntegrationTestingKey_2026_SecureKey!";

    private TestServer CreateTestServer()
    {
        var webHost = new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddRouting();
                services.AddLogging();

                var key = Encoding.UTF8.GetBytes(JwtSecret);
                services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                    .AddJwtBearer(options =>
                    {
                        options.TokenValidationParameters = new TokenValidationParameters
                        {
                            ValidateIssuer = true,
                            ValidateAudience = true,
                            ValidateLifetime = true,
                            ValidateIssuerSigningKey = true,
                            ValidIssuer = "PosErp",
                            ValidAudience = "PosErpClient",
                            IssuerSigningKey = new SymmetricSecurityKey(key),
                            ClockSkew = TimeSpan.Zero
                        };
                    });

                services.AddAuthorization();

                services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(GetSalesReturnByIdQuery).Assembly));
                services.AddScoped<IApplicationDbContext>(_ => _context);

                services.AddControllers()
                    .AddApplicationPart(typeof(AccountsReceivableController).Assembly);
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
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, _cashierId.ToString()),
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
    public async Task GetReturnDetails_UnauthenticatedCall_Returns401Unauthorized_ThroughMiddleware()
    {
        using var server = CreateTestServer();
        var client = server.CreateClient();

        // 1. Unauthenticated HTTP call (no token) -> Middleware must reject with 401
        var response = await client.GetAsync($"/api/AccountsReceivable/returns/{_salesReturnId}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetReturnDetails_WrongRoleCall_Returns403Forbidden_ThroughMiddleware()
    {
        using var server = CreateTestServer();
        var client = server.CreateClient();

        // 2. Authenticated HTTP call with wrong role "Customer" (not in "Admin,Manager,Owner,Supervisor,Cashier")
        // Middleware must reject with 403 Forbidden
        var token = GenerateTestJwtToken("Customer");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync($"/api/AccountsReceivable/returns/{_salesReturnId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetReturnDetails_AuthorizedRoleCall_Returns200Ok_ThroughMiddleware()
    {
        using var server = CreateTestServer();
        var client = server.CreateClient();

        // 3. Authenticated HTTP call with authorized head-office role "Owner" (cross-store enabled without store_id) -> returns 200 OK
        var token = GenerateTestJwtToken("Owner");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync($"/api/AccountsReceivable/returns/{_salesReturnId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetReturnDetails_MatchingStoreClaim_Returns200Ok_ThroughMiddleware()
    {
        using var server = CreateTestServer();
        var client = server.CreateClient();

        // 4. Authenticated HTTP call with matching store_id -> returns 200 OK
        var token = GenerateTestJwtToken("Cashier", _storeId);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync($"/api/AccountsReceivable/returns/{_salesReturnId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetReturnDetails_MismatchedStoreClaim_Returns404NotFound_ThroughMiddleware()
    {
        using var server = CreateTestServer();
        var client = server.CreateClient();

        // 5. Authenticated HTTP call with foreign store_id -> returns 404 Not Found (isolated)
        var foreignStoreId = Guid.NewGuid();
        var token = GenerateTestJwtToken("Cashier", foreignStoreId);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync($"/api/AccountsReceivable/returns/{_salesReturnId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetReturnDetails_CashierWithoutStoreClaim_Returns404NotFound_ThroughMiddleware()
    {
        using var server = CreateTestServer();
        var client = server.CreateClient();

        // 6. Authenticated HTTP call with Cashier role but missing store_id -> fails closed -> 404 Not Found
        var token = GenerateTestJwtToken("Cashier");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync($"/api/AccountsReceivable/returns/{_salesReturnId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    public void Dispose()
    {
        try
        {
            var ret = _context.SalesReturns.Include(sr => sr.Items).FirstOrDefault(sr => sr.Id == _salesReturnId);
            if (ret != null)
            {
                _context.SalesReturnItems.RemoveRange(ret.Items);
                _context.SalesReturns.Remove(ret);
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

            var term = _context.Terminals.FirstOrDefault(t => t.Id == _terminalId);
            if (term != null) _context.Terminals.Remove(term);

            _context.SaveChanges();
            _context.Dispose();
        }
        catch { }
    }
}
