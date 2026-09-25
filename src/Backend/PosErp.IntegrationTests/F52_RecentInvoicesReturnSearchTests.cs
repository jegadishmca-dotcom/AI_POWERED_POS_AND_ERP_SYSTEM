using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PosErp.Api.Controllers;
using PosErp.Domain.Entities.Catalog;
using PosErp.Domain.Entities.Crm;
using PosErp.Domain.Entities.Inventory;
using PosErp.Domain.Entities.Pos;
using PosErp.Domain.Entities.Auth;
using PosErp.Infrastructure.Persistence;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace PosErp.IntegrationTests;

/// <summary>
/// Tests for Issue 3: Sales return recent invoices dropdown and case-insensitive search.
/// Validates:
/// 1. Only COMPLETED and non-deleted invoices are returned.
/// 2. todayOnly parameter correctly scopes to active business date.
/// 3. Case-insensitive search on invoice number (exact, suffix, contains).
/// 4. Case-insensitive search on customer name and phone.
/// 5. Case-insensitive search on item barcodes.
/// 6. Case-insensitive lookup via GetInvoiceByNumber.
/// </summary>
[Collection("Database Collection")]
public class F52_RecentInvoicesReturnSearchTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly PosController _controller;

    private readonly Guid _storeId = Guid.NewGuid();
    private readonly Guid _terminalId = Guid.NewGuid();
    private readonly Guid _cashierId = Guid.NewGuid();
    private readonly Guid _customerId = Guid.NewGuid();
    private readonly Guid _productId = Guid.NewGuid();
    private readonly Guid _taxSlabId = Guid.NewGuid();

    private readonly Guid _invCompletedToday = Guid.NewGuid();
    private readonly Guid _invCompletedPast = Guid.NewGuid();
    private readonly Guid _invHold = Guid.NewGuid();
    private readonly Guid _invDeleted = Guid.NewGuid();

    private readonly DateTime _today = new DateTime(2026, 9, 25);
    private readonly DateTime _yesterday = new DateTime(2026, 9, 24);

    public F52_RecentInvoicesReturnSearchTests()
    {
        _context = IntegrationTestDbFactory.Build();
        _controller = new PosController(null!, null!, _context, null!, null!);
        SetCallerContext("Cashier", _storeId);
        SeedAsync().GetAwaiter().GetResult();
    }

    private void SetCallerContext(string role, Guid? storeId)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, _cashierId.ToString()),
            new Claim(ClaimTypes.Role, role)
        };
        if (storeId.HasValue)
        {
            claims.Add(new Claim("store_id", storeId.Value.ToString()));
        }

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"))
            }
        };
    }

    private async Task SeedAsync()
    {
        // 0. Store
        if (!await _context.Stores.AnyAsync(s => s.Id == _storeId))
        {
            _context.Stores.Add(new Store
            {
                Id = _storeId,
                StoreName = "F52 Test Store",
                StoreCode = "F52STR",
                IsActive = true
            });
        }

        // 1. Tax Slab & UOM
        if (!await _context.TaxSlabs.AnyAsync(t => t.Id == _taxSlabId))
        {
            _context.TaxSlabs.Add(new TaxSlab
            {
                Id = _taxSlabId, Name = "0% GST F52",
                CgstRate = 0m, SgstRate = 0m, CessRate = 0m
            });
        }

        var uom = await _context.UnitOfMeasures.FirstOrDefaultAsync(u => u.Symbol == "PCS");
        if (uom == null)
        {
            uom = new UnitOfMeasure { Id = Guid.NewGuid(), Symbol = "PCS", Name = "Pieces" };
            _context.UnitOfMeasures.Add(uom);
        }

        // 2. Terminal & Cashier
        if (!await _context.Terminals.AnyAsync(t => t.Id == _terminalId))
        {
            _context.Terminals.Add(new Terminal
            {
                Id = _terminalId,
                TerminalCode = "POS-F52",
                Name = "F52 Register",
                IsActive = true
            });
        }

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
                Username = "cashier_f52",
                FullName = "Meena Cashier",
                PasswordHash = "hash",
                RoleId = role.Id,
                IsActive = true
            });
        }

        // 3. Customer
        if (!await _context.Customers.AnyAsync(c => c.Id == _customerId))
        {
            _context.Customers.Add(new Customer
            {
                Id = _customerId,
                Name = "Kavitha Anand",
                Phone = "9840199887",
                Email = "kavitha@test.local"
            });
        }

        // 4. Product
        if (!await _context.Products.AnyAsync(p => p.Id == _productId))
        {
            _context.Products.Add(new Product
            {
                Id = _productId,
                ProductCode = "PRD-F52-01",
                Name = "Heritage Full Cream Milk 500ml",
                SellingPrice = 36.00m,
                Mrp = 36.00m,
                PurchasePrice = 32.00m,
                TaxSlabId = _taxSlabId,
                UnitOfMeasureId = uom.Id,
                IsActive = true
            });
        }

        // 5. Store Business Date (OPEN for today)
        var sbd = await _context.StoreBusinessDates.FirstOrDefaultAsync(d => d.StoreId == _storeId && d.BusinessDate == _today);
        if (sbd == null)
        {
            _context.StoreBusinessDates.Add(new StoreBusinessDate
            {
                StoreId = _storeId,
                BusinessDate = _today,
                Status = "OPEN",
                OpenedAt = DateTime.UtcNow
            });
        }

        await _context.SaveChangesAsync();

        // 6. Invoices:
        // A) Completed Today
        if (!await _context.Invoices.AnyAsync(i => i.Id == _invCompletedToday))
        {
            var inv1 = new Invoice
            {
                Id = _invCompletedToday,
                StoreId = _storeId,
                TerminalId = _terminalId,
                CashierId = _cashierId,
                CustomerId = _customerId,
                InvoiceNumber = "INV-F52-TODAY-0001",
                TerminalSequence = 1,
                BusinessDate = _today,
                Status = "COMPLETED",
                SubTotal = 72m,
                TotalAmount = 72m,
                NetPayable = 72m,
                PaymentMode = "CASH",
                CreatedAt = DateTime.UtcNow,
                IsDeleted = false
            };
            inv1.Items.Add(new InvoiceItem
            {
                Id = Guid.NewGuid(),
                InvoiceId = _invCompletedToday,
                ProductId = _productId,
                ProductName = "Heritage Full Cream Milk 500ml",
                Barcode = "BC-F52-MILK",
                Quantity = 2,
                UnitPrice = 36m,
                TotalAmount = 72m
            });
            _context.Invoices.Add(inv1);
        }

        // B) Completed Yesterday
        if (!await _context.Invoices.AnyAsync(i => i.Id == _invCompletedPast))
        {
            var inv2 = new Invoice
            {
                Id = _invCompletedPast,
                StoreId = _storeId,
                TerminalId = _terminalId,
                CashierId = _cashierId,
                CustomerId = _customerId,
                InvoiceNumber = "INV-F52-PAST-0002",
                TerminalSequence = 1,
                BusinessDate = _yesterday,
                Status = "COMPLETED",
                SubTotal = 36m,
                TotalAmount = 36m,
                NetPayable = 36m,
                PaymentMode = "UPI",
                CreatedAt = DateTime.UtcNow.AddDays(-1),
                IsDeleted = false
            };
            inv2.Items.Add(new InvoiceItem
            {
                Id = Guid.NewGuid(),
                InvoiceId = _invCompletedPast,
                ProductId = _productId,
                ProductName = "Heritage Full Cream Milk 500ml",
                Barcode = "BC-F52-MILK",
                Quantity = 1,
                UnitPrice = 36m,
                TotalAmount = 36m
            });
            _context.Invoices.Add(inv2);
        }

        // C) Hold Invoice
        if (!await _context.Invoices.AnyAsync(i => i.Id == _invHold))
        {
            _context.Invoices.Add(new Invoice
            {
                Id = _invHold,
                StoreId = _storeId,
                TerminalId = _terminalId,
                CashierId = _cashierId,
                InvoiceNumber = "INV-F52-HOLD-0003",
                TerminalSequence = 2,
                BusinessDate = _today,
                Status = "HOLD",
                SubTotal = 36m,
                TotalAmount = 36m,
                NetPayable = 36m,
                PaymentMode = "CASH",
                CreatedAt = DateTime.UtcNow,
                IsDeleted = false
            });
        }

        // D) Deleted Invoice
        if (!await _context.Invoices.AnyAsync(i => i.Id == _invDeleted))
        {
            _context.Invoices.Add(new Invoice
            {
                Id = _invDeleted,
                StoreId = _storeId,
                TerminalId = _terminalId,
                CashierId = _cashierId,
                InvoiceNumber = "INV-F52-DEL-0004",
                TerminalSequence = 3,
                BusinessDate = _today,
                Status = "COMPLETED",
                SubTotal = 36m,
                TotalAmount = 36m,
                NetPayable = 36m,
                PaymentMode = "CASH",
                CreatedAt = DateTime.UtcNow,
                IsDeleted = true
            });
        }

        await _context.SaveChangesAsync();
    }

    private static Guid GetId(object obj)
    {
        var prop = obj.GetType().GetProperty("Id");
        return (Guid)(prop?.GetValue(obj) ?? Guid.Empty);
    }

    [Fact]
    public async Task SearchInvoices_ExcludesNonCompletedAndDeleted()
    {
        var actionResult = await _controller.SearchInvoices(query: null, limit: 50, todayOnly: false);
        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var invoices = ((IEnumerable<object>)okResult.Value!).ToList();

        // Must include COMPLETED invoices
        Assert.Contains(invoices, inv => GetId(inv) == _invCompletedToday);
        Assert.Contains(invoices, inv => GetId(inv) == _invCompletedPast);

        // Must NOT include HOLD or DELETED invoices
        Assert.DoesNotContain(invoices, inv => GetId(inv) == _invHold);
        Assert.DoesNotContain(invoices, inv => GetId(inv) == _invDeleted);
    }

    [Fact]
    public async Task SearchInvoices_TodayOnlyFilter_ScopesCorrectly()
    {
        // When todayOnly = true
        var actionResult = await _controller.SearchInvoices(query: null, limit: 50, todayOnly: true);
        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var invoices = ((IEnumerable<object>)okResult.Value!).ToList();

        Assert.Contains(invoices, inv => GetId(inv) == _invCompletedToday);
        Assert.DoesNotContain(invoices, inv => GetId(inv) == _invCompletedPast);
    }

    [Fact]
    public async Task SearchInvoices_CaseInsensitive_InvoiceNumberExactAndPartial()
    {
        // Lowercase query for exact invoice number
        var resLower = await _controller.SearchInvoices(query: "inv-f52-today-0001", limit: 50);
        var okLower = Assert.IsType<OkObjectResult>(resLower);
        var invsLower = ((IEnumerable<object>)okLower.Value!).ToList();
        Assert.Single(invsLower);
        Assert.Equal(_invCompletedToday, GetId(invsLower[0]));

        // Suffix / Partial query "0001"
        var resSuffix = await _controller.SearchInvoices(query: "0001", limit: 50);
        var okSuffix = Assert.IsType<OkObjectResult>(resSuffix);
        var invsSuffix = ((IEnumerable<object>)okSuffix.Value!).ToList();
        Assert.Contains(invsSuffix, inv => GetId(inv) == _invCompletedToday);

        // Substring "today"
        var resSub = await _controller.SearchInvoices(query: "TODAY", limit: 50);
        var okSub = Assert.IsType<OkObjectResult>(resSub);
        var invsSub = ((IEnumerable<object>)okSub.Value!).ToList();
        Assert.Contains(invsSub, inv => GetId(inv) == _invCompletedToday);
    }

    [Fact]
    public async Task SearchInvoices_CaseInsensitive_CustomerNameAndPhone()
    {
        // Search by lowercase customer first name "kavitha"
        var resName = await _controller.SearchInvoices(query: "kavitha", limit: 50);
        var okName = Assert.IsType<OkObjectResult>(resName);
        var invsName = ((IEnumerable<object>)okName.Value!).ToList();
        Assert.Contains(invsName, inv => GetId(inv) == _invCompletedToday);

        // Search by phone "9840199887"
        var resPhone = await _controller.SearchInvoices(query: "9840199887", limit: 50);
        var okPhone = Assert.IsType<OkObjectResult>(resPhone);
        var invsPhone = ((IEnumerable<object>)okPhone.Value!).ToList();
        Assert.Contains(invsPhone, inv => GetId(inv) == _invCompletedToday);
    }

    [Fact]
    public async Task SearchInvoices_CaseInsensitive_ItemBarcode()
    {
        // Item barcode is "BC-F52-MILK"
        // Search using lowercase "bc-f52-milk"
        var resBarcode = await _controller.SearchInvoices(query: "bc-f52-milk", limit: 50);
        var okBarcode = Assert.IsType<OkObjectResult>(resBarcode);
        var invsBarcode = ((IEnumerable<object>)okBarcode.Value!).ToList();

        Assert.Contains(invsBarcode, inv => GetId(inv) == _invCompletedToday);
        Assert.Contains(invsBarcode, inv => GetId(inv) == _invCompletedPast);
    }

    [Fact]
    public async Task GetInvoiceByNumber_CaseInsensitiveLookup()
    {
        // Lookup using all lowercase
        var resLower = await _controller.GetInvoiceByNumber("inv-f52-today-0001");
        var okLower = Assert.IsType<OkObjectResult>(resLower);
        Assert.Equal(_invCompletedToday, GetId(okLower.Value!));

        // Lookup using mixed case
        var resMixed = await _controller.GetInvoiceByNumber("InV-f52-ToDaY-0001");
        var okMixed = Assert.IsType<OkObjectResult>(resMixed);
        Assert.Equal(_invCompletedToday, GetId(okMixed.Value!));
    }

    [Fact]
    public async Task SearchInvoices_StoreScoping_FiltersByStoreIdClaim()
    {
        // 1. With matching store_id claim -> invoices belonging to _storeId are returned
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, _cashierId.ToString()),
                    new Claim("store_id", _storeId.ToString())
                }))
            }
        };

        var resMatching = await _controller.SearchInvoices(query: "inv-f52-today-0001", limit: 50);
        var okMatching = Assert.IsType<OkObjectResult>(resMatching);
        var invsMatching = ((IEnumerable<object>)okMatching.Value!).ToList();
        Assert.Single(invsMatching);
        Assert.Equal(_invCompletedToday, GetId(invsMatching[0]));

        // 2. With foreign store_id claim -> 0 invoices returned
        var foreignStoreId = Guid.NewGuid();
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, _cashierId.ToString()),
                    new Claim("store_id", foreignStoreId.ToString())
                }))
            }
        };

        var resForeign = await _controller.SearchInvoices(query: "inv-f52-today-0001", limit: 50);
        var okForeign = Assert.IsType<OkObjectResult>(resForeign);
        var invsForeign = ((IEnumerable<object>)okForeign.Value!).ToList();
        Assert.Empty(invsForeign);
    }

    [Fact]
    public async Task GetInvoiceByNumber_StoreScoping_EnforcesStoreIsolation()
    {
        // 1. Matching store -> 200 OK
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, _cashierId.ToString()),
                    new Claim("store_id", _storeId.ToString())
                }))
            }
        };

        var resMatch = await _controller.GetInvoiceByNumber("inv-f52-today-0001");
        var okMatch = Assert.IsType<OkObjectResult>(resMatch);
        Assert.Equal(_invCompletedToday, GetId(okMatch.Value!));

        // 2. Mismatched store -> 404 NotFound
        var foreignStoreId = Guid.NewGuid();
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, _cashierId.ToString()),
                    new Claim("store_id", foreignStoreId.ToString())
                }))
            }
        };

        var resMismatch = await _controller.GetInvoiceByNumber("inv-f52-today-0001");
        Assert.IsType<NotFoundObjectResult>(resMismatch);
    }

    [Fact]
    public async Task GetInvoice_StoreScoping_EnforcesStoreIsolation()
    {
        // 1. Matching store -> 200 OK
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, _cashierId.ToString()),
                    new Claim("store_id", _storeId.ToString())
                }))
            }
        };

        var resMatch = await _controller.GetInvoice(_invCompletedToday);
        var okMatch = Assert.IsType<OkObjectResult>(resMatch);
        Assert.Equal(_invCompletedToday, GetId(okMatch.Value!));

        // 2. Mismatched store -> 404 NotFound
        var foreignStoreId = Guid.NewGuid();
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, _cashierId.ToString()),
                    new Claim("store_id", foreignStoreId.ToString())
                }))
            }
        };

        var resMismatch = await _controller.GetInvoice(_invCompletedToday);
        Assert.IsType<NotFoundObjectResult>(resMismatch);
    }

    [Fact]
    public async Task PosEndpoints_OwnerAdminWithoutStoreClaim_HasCrossStoreAccess()
    {
        // When an Owner/Admin account has no store_id claim (e.g. head-office cross-store manager):
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, _cashierId.ToString()),
                    new Claim(ClaimTypes.Role, "Owner")
                    // NO store_id claim
                }))
            }
        };

        // 1. GetInvoiceByNumber succeeds across stores
        var resByNum = await _controller.GetInvoiceByNumber("inv-f52-today-0001");
        var okByNum = Assert.IsType<OkObjectResult>(resByNum);
        Assert.Equal(_invCompletedToday, GetId(okByNum.Value!));

        // 2. GetInvoice by Guid succeeds across stores
        var resById = await _controller.GetInvoice(_invCompletedToday);
        var okById = Assert.IsType<OkObjectResult>(resById);
        Assert.Equal(_invCompletedToday, GetId(okById.Value!));

        // 3. SearchInvoices returns invoices across stores
        var resSearch = await _controller.SearchInvoices(query: "inv-f52-today-0001", limit: 50);
        var okSearch = Assert.IsType<OkObjectResult>(resSearch);
        var invsSearch = ((IEnumerable<object>)okSearch.Value!).ToList();
        Assert.Single(invsSearch);
        Assert.Equal(_invCompletedToday, GetId(invsSearch[0]));
    }

    [Fact]
    public async Task PosEndpoints_CashierWithoutStoreClaim_IsDeniedAccess()
    {
        // When a non-Owner/Admin account (e.g. Cashier) has no store_id claim:
        // System MUST fail closed: zero accessible stores (empty results or 404), NEVER global access.
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, _cashierId.ToString()),
                    new Claim(ClaimTypes.Role, "Cashier")
                    // NO store_id claim
                }))
            }
        };

        // 1. GetInvoiceByNumber returns NotFound
        var resByNum = await _controller.GetInvoiceByNumber("inv-f52-today-0001");
        Assert.IsType<NotFoundObjectResult>(resByNum);

        // 2. GetInvoice by Guid returns NotFound
        var resById = await _controller.GetInvoice(_invCompletedToday);
        Assert.IsType<NotFoundObjectResult>(resById);

        // 3. SearchInvoices returns empty list (0 invoices)
        var resSearch = await _controller.SearchInvoices(query: "inv-f52-today-0001", limit: 50);
        var okSearch = Assert.IsType<OkObjectResult>(resSearch);
        var invsSearch = ((IEnumerable<object>)okSearch.Value!).ToList();
        Assert.Empty(invsSearch);

        // 4. PrintReceipt returns NotFound
        var resPrint = await _controller.PrintReceipt(_invCompletedToday);
        Assert.IsType<NotFoundObjectResult>(resPrint);
    }

    public void Dispose()
    {
        try
        {
            var invs = _context.Invoices
                .Include(i => i.Items)
                .Where(i => i.InvoiceNumber.StartsWith("INV-F52-"))
                .ToList();
            if (invs.Any())
            {
                _context.Invoices.RemoveRange(invs);
            }

            var cust = _context.Customers.Find(_customerId);
            if (cust != null) _context.Customers.Remove(cust);

            var term = _context.Terminals.Find(_terminalId);
            if (term != null) _context.Terminals.Remove(term);

            var usr = _context.Users.Find(_cashierId);
            if (usr != null) _context.Users.Remove(usr);

            var prod = _context.Products.Find(_productId);
            if (prod != null) _context.Products.Remove(prod);

            var sbd = _context.StoreBusinessDates.FirstOrDefault(d => d.StoreId == _storeId && d.BusinessDate == _today);
            if (sbd != null) _context.StoreBusinessDates.Remove(sbd);

            var store = _context.Stores.Find(_storeId);
            if (store != null) _context.Stores.Remove(store);

            _context.SaveChanges();
        }
        catch { }
        finally
        {
            _context.Dispose();
        }
    }
}
