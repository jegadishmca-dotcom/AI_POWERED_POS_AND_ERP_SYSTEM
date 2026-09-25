using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Features.Catalog.Queries.SearchProducts;
using PosErp.Domain.Entities.Catalog;
using PosErp.Domain.Entities.Inventory;
using PosErp.Infrastructure.Persistence;
using Xunit;

namespace PosErp.IntegrationTests
{
    /// <summary>
    /// Tests for Issue 5: Case-insensitive barcode search.
    /// Validates that SearchProductsQuery returns products when the barcode case
    /// doesn't match the stored value (e.g. searching "mms" finds barcode "MMS").
    /// </summary>
    [Collection("Database Collection")]
    public class F50_CaseInsensitiveBarcodeSearchTests : IDisposable
    {
        private readonly ApplicationDbContext _context;
        private readonly SearchProductsQueryHandler _handler;

        private readonly Guid _taxSlabId = Guid.NewGuid();
        private readonly Guid _uomId = Guid.NewGuid();

        // Product 1: barcode stored as UPPERCASE "MMS12345"
        private readonly Guid _productUpperId = Guid.NewGuid();

        // Product 2: barcode stored as lowercase "abc99"
        private readonly Guid _productLowerId = Guid.NewGuid();

        // Product 3: barcode stored as MixedCase "DaIrY-001"
        private readonly Guid _productMixedId = Guid.NewGuid();

        public F50_CaseInsensitiveBarcodeSearchTests()
        {
            _context = IntegrationTestDbFactory.Build();
            _handler = new SearchProductsQueryHandler(_context);
            SeedAsync().GetAwaiter().GetResult();
        }

        private async Task SeedAsync()
        {
            // Tax slab
            if (!await _context.TaxSlabs.AnyAsync(t => t.Id == _taxSlabId))
            {
                _context.TaxSlabs.Add(new TaxSlab
                {
                    Id = _taxSlabId, Name = "0% GST F50",
                    CgstRate = 0m, SgstRate = 0m, CessRate = 0m
                });
            }

            // UOM
            var uom = await _context.UnitOfMeasures.FirstOrDefaultAsync(u => u.Symbol == "PCS");
            if (uom == null)
            {
                uom = new UnitOfMeasure { Id = _uomId, Symbol = "PCS", Name = "Pieces" };
                _context.UnitOfMeasures.Add(uom);
            }
            var uomIdToUse = uom.Id;

            // Product 1: UPPERCASE barcode "MMS12345"
            var p1 = new Product
            {
                Id = _productUpperId,
                Name = "Mars MilkShake F50",
                ProductCode = "F50-UPPER",
                TaxSlabId = _taxSlabId,
                UnitOfMeasureId = uomIdToUse,
                Mrp = 50m, SellingPrice = 45m, PurchasePrice = 30m,
                IsActive = true
            };
            p1.Barcodes.Add(new Barcode { Id = Guid.NewGuid(), BarcodeValue = "MMS12345", IsPrimary = true });
            _context.Products.Add(p1);

            // Product 2: lowercase barcode "abc99"
            var p2 = new Product
            {
                Id = _productLowerId,
                Name = "Alpha Biscuit Cookie F50",
                ProductCode = "F50-LOWER",
                TaxSlabId = _taxSlabId,
                UnitOfMeasureId = uomIdToUse,
                Mrp = 30m, SellingPrice = 25m, PurchasePrice = 15m,
                IsActive = true
            };
            p2.Barcodes.Add(new Barcode { Id = Guid.NewGuid(), BarcodeValue = "abc99", IsPrimary = true });
            _context.Products.Add(p2);

            // Product 3: MixedCase barcode "DaIrY-001"
            var p3 = new Product
            {
                Id = _productMixedId,
                Name = "Dairy Milk Chocolate F50",
                ProductCode = "F50-MIXED",
                TaxSlabId = _taxSlabId,
                UnitOfMeasureId = uomIdToUse,
                Mrp = 100m, SellingPrice = 90m, PurchasePrice = 60m,
                IsActive = true
            };
            p3.Barcodes.Add(new Barcode { Id = Guid.NewGuid(), BarcodeValue = "DaIrY-001", IsPrimary = true });
            _context.Products.Add(p3);

            await _context.SaveChangesAsync();
        }

        // ── Case-insensitive exact barcode match tests ──────────────────────

        [Theory]
        [InlineData("mms12345")]      // all lowercase → should match stored "MMS12345"
        [InlineData("MMS12345")]      // exact match
        [InlineData("Mms12345")]      // mixed case
        public async Task Search_BarcodeUppercaseStored_MatchesRegardlessOfInputCase(string query)
        {
            var results = await _handler.Handle(
                new SearchProductsQuery(query, 20), CancellationToken.None);

            Assert.NotEmpty(results);
            Assert.Contains(results, r => r.Id == _productUpperId);
        }

        [Theory]
        [InlineData("ABC99")]         // all uppercase → should match stored "abc99"
        [InlineData("abc99")]         // exact match
        [InlineData("Abc99")]         // mixed case
        public async Task Search_BarcodeLowercaseStored_MatchesRegardlessOfInputCase(string query)
        {
            var results = await _handler.Handle(
                new SearchProductsQuery(query, 20), CancellationToken.None);

            Assert.NotEmpty(results);
            Assert.Contains(results, r => r.Id == _productLowerId);
        }

        [Theory]
        [InlineData("dairy-001")]     // all lowercase → should match stored "DaIrY-001"
        [InlineData("DAIRY-001")]     // all uppercase
        [InlineData("DaIrY-001")]     // exact match
        public async Task Search_BarcodeMixedCaseStored_MatchesRegardlessOfInputCase(string query)
        {
            var results = await _handler.Handle(
                new SearchProductsQuery(query, 20), CancellationToken.None);

            Assert.NotEmpty(results);
            Assert.Contains(results, r => r.Id == _productMixedId);
        }

        // ── Multi-word search with barcode in one word ──────────────────────

        [Fact]
        public async Task Search_MultiWord_BarcodeTermIsCaseInsensitive()
        {
            // "mms12345 Mars" — one word is a barcode (lowercase), other is name
            var results = await _handler.Handle(
                new SearchProductsQuery("mms12345 Mars", 20), CancellationToken.None);

            // Should match since both "mms12345" (barcode, case-insensitive) AND "Mars" (name ILike) match
            Assert.NotEmpty(results);
            Assert.Contains(results, r => r.Id == _productUpperId);
        }

        // ── Negative test: non-existent barcode returns nothing ─────────────

        [Fact]
        public async Task Search_NonExistentBarcode_ReturnsEmpty()
        {
            var results = await _handler.Handle(
                new SearchProductsQuery("ZZZNOMATCH999", 20), CancellationToken.None);

            Assert.Empty(results);
        }

        public void Dispose()
        {
            _context?.Dispose();
        }
    }
}
