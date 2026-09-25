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
    /// Tests for Issue 2: Best-match search ordering.
    /// Validates that SearchProductsQuery returns results with exact barcode matches
    /// first, then exact code matches, then starts-with name matches, then contains.
    /// </summary>
    [Collection("Database Collection")]
    public class F51_BestMatchSearchOrderingTests : IDisposable
    {
        private readonly ApplicationDbContext _context;
        private readonly SearchProductsQueryHandler _handler;

        private readonly Guid _taxSlabId = Guid.NewGuid();

        // Products designed to test ordering:
        // - "Sugar" with barcode "SUG001" → exact barcode match when searching "SUG001"
        // - "Sugar Cube" with code "SUGAR" → code starts-with when searching "sugar"
        // - "Brown Sugar" → name contains "sugar" (not starts-with)
        // - "Sugar Free Candy" → name starts with "Sugar"
        private readonly Guid _pidBarcodeMatch = Guid.NewGuid();  // barcode = "SUG001"
        private readonly Guid _pidCodeMatch = Guid.NewGuid();     // code = "SUGAR"
        private readonly Guid _pidExactNameMatch = Guid.NewGuid(); // name = "Sugar"
        private readonly Guid _pidNameContains = Guid.NewGuid();  // name = "Brown Sugar Syrup"
        private readonly Guid _pidNameStartsWith = Guid.NewGuid(); // name = "Sugar Free Candy"

        public F51_BestMatchSearchOrderingTests()
        {
            _context = IntegrationTestDbFactory.Build();
            _handler = new SearchProductsQueryHandler(_context);
            SeedAsync().GetAwaiter().GetResult();
        }

        private async Task SeedAsync()
        {
            if (!await _context.TaxSlabs.AnyAsync(t => t.Id == _taxSlabId))
            {
                _context.TaxSlabs.Add(new TaxSlab
                {
                    Id = _taxSlabId, Name = "0% GST F51",
                    CgstRate = 0m, SgstRate = 0m, CessRate = 0m
                });
            }

            var uom = await _context.UnitOfMeasures.FirstOrDefaultAsync(u => u.Symbol == "PCS");
            if (uom == null)
            {
                uom = new UnitOfMeasure { Id = Guid.NewGuid(), Symbol = "PCS", Name = "Pieces" };
                _context.UnitOfMeasures.Add(uom);
            }

            // Product 1: Has barcode "SUG001" — should rank highest for "SUG001" search
            var p1 = new Product
            {
                Id = _pidBarcodeMatch, Name = "Zeta Sugar Plain", ProductCode = "F51-A",
                TaxSlabId = _taxSlabId, UnitOfMeasureId = uom.Id,
                Mrp = 50m, SellingPrice = 45m, PurchasePrice = 30m, IsActive = true
            };
            p1.Barcodes.Add(new Barcode { BarcodeValue = "SUG001", IsPrimary = true });
            _context.Products.Add(p1);

            // Product 2: Code = "SUGAR" — should rank after barcode for "sugar" search
            var p2 = new Product
            {
                Id = _pidCodeMatch, Name = "Zeta Sugar Cubes", ProductCode = "SUGAR",
                TaxSlabId = _taxSlabId, UnitOfMeasureId = uom.Id,
                Mrp = 60m, SellingPrice = 55m, PurchasePrice = 35m, IsActive = true
            };
            p2.Barcodes.Add(new Barcode { BarcodeValue = "F51BC002", IsPrimary = true });
            _context.Products.Add(p2);

            // Product 3: Name = "Brown Sugar Syrup" — contains "sugar" (not starts-with)
            var p3 = new Product
            {
                Id = _pidNameContains, Name = "Brown Sugar Syrup", ProductCode = "F51-C",
                TaxSlabId = _taxSlabId, UnitOfMeasureId = uom.Id,
                Mrp = 80m, SellingPrice = 70m, PurchasePrice = 40m, IsActive = true
            };
            p3.Barcodes.Add(new Barcode { BarcodeValue = "F51BC003", IsPrimary = true });
            _context.Products.Add(p3);

            // Product 4: Name = "Sugar Free Candy" — starts with "Sugar"
            var p4 = new Product
            {
                Id = _pidNameStartsWith, Name = "Sugar Free Candy", ProductCode = "F51-D",
                TaxSlabId = _taxSlabId, UnitOfMeasureId = uom.Id,
                Mrp = 40m, SellingPrice = 35m, PurchasePrice = 20m, IsActive = true
            };
            p4.Barcodes.Add(new Barcode { BarcodeValue = "F51BC004", IsPrimary = true });
            _context.Products.Add(p4);

            // Product 5: Name = "Sugar" — exact name match
            var p5 = new Product
            {
                Id = _pidExactNameMatch, Name = "Sugar", ProductCode = "F51-E",
                TaxSlabId = _taxSlabId, UnitOfMeasureId = uom.Id,
                Mrp = 42m, SellingPrice = 38m, PurchasePrice = 22m, IsActive = true
            };
            p5.Barcodes.Add(new Barcode { BarcodeValue = "F51BC005", IsPrimary = true });
            _context.Products.Add(p5);

            await _context.SaveChangesAsync();
        }

        // ── Ordering tests ─────────────────────────────────────────────────

        [Fact]
        public async Task Search_ExactName_AppearsBeforeCodeStartsWithAndContains()
        {
            var results = await _handler.Handle(
                new SearchProductsQuery("sugar", 20), CancellationToken.None);

            var exactNameIdx = results.FindIndex(r => r.Id == _pidExactNameMatch);
            var codeMatchIdx = results.FindIndex(r => r.Id == _pidCodeMatch);
            var startsWithIdx = results.FindIndex(r => r.Id == _pidNameStartsWith);
            var containsIdx = results.FindIndex(r => r.Id == _pidNameContains);

            Assert.True(exactNameIdx >= 0, "Exact name product not found");
            Assert.True(codeMatchIdx >= 0, "Exact code product not found");
            Assert.True(startsWithIdx >= 0, "Starts-with product not found");
            Assert.True(containsIdx >= 0, "Contains product not found");

            Assert.True(exactNameIdx < codeMatchIdx,
                $"exact name (idx={exactNameIdx}) should come before exact code (idx={codeMatchIdx})");
            Assert.True(codeMatchIdx < startsWithIdx,
                $"exact code (idx={codeMatchIdx}) should come before starts-with (idx={startsWithIdx})");
            Assert.True(startsWithIdx < containsIdx,
                $"starts-with (idx={startsWithIdx}) should come before contains (idx={containsIdx})");
        }

        [Fact]
        public async Task Search_Sugar_NameStartsWithAppearsBeforeContains()
        {
            var results = await _handler.Handle(
                new SearchProductsQuery("sugar", 20), CancellationToken.None);

            // All 4 products should match "sugar" (code, name, etc.)
            Assert.True(results.Count >= 2, $"Expected >= 2 results, got {results.Count}");

            // Find positions of starts-with vs contains
            var startsWithIdx = results.FindIndex(r => r.Id == _pidNameStartsWith);
            var containsIdx = results.FindIndex(r => r.Id == _pidNameContains);

            Assert.True(startsWithIdx >= 0, "Sugar Free Candy (starts-with) not found");
            Assert.True(containsIdx >= 0, "Brown Sugar Syrup (contains) not found");
            Assert.True(startsWithIdx < containsIdx,
                $"starts-with (idx={startsWithIdx}) should come before contains (idx={containsIdx})");
        }

        [Fact]
        public async Task Search_ExactBarcode_AppearsFirst()
        {
            var results = await _handler.Handle(
                new SearchProductsQuery("SUG001", 20), CancellationToken.None);

            Assert.NotEmpty(results);
            Assert.Equal(_pidBarcodeMatch, results[0].Id);
        }

        [Fact]
        public async Task Search_ExactCode_RanksHigherThanNameContains()
        {
            // "SUGAR" is an exact code match for product 2, and a name-contains for others
            var results = await _handler.Handle(
                new SearchProductsQuery("SUGAR", 20), CancellationToken.None);

            Assert.True(results.Count >= 2);

            var codeMatchIdx = results.FindIndex(r => r.Id == _pidCodeMatch);
            var containsIdx = results.FindIndex(r => r.Id == _pidNameContains);

            Assert.True(codeMatchIdx >= 0, "Code match product not found");
            if (containsIdx >= 0)
            {
                Assert.True(codeMatchIdx < containsIdx,
                    $"exact code (idx={codeMatchIdx}) should come before contains (idx={containsIdx})");
            }
        }

        [Fact]
        public async Task Search_EmptyQuery_ReturnsAlphabeticalOrder()
        {
            var results = await _handler.Handle(
                new SearchProductsQuery("", 100), CancellationToken.None);

            if (results.Count >= 2)
            {
                // Verify alphabetical ordering for non-empty result set
                for (int i = 0; i < results.Count - 1; i++)
                {
                    Assert.True(
                        string.Compare(results[i].Name, results[i + 1].Name, StringComparison.OrdinalIgnoreCase) <= 0,
                        $"Results not alphabetical: '{results[i].Name}' before '{results[i + 1].Name}'");
                }
            }
        }

        [Fact]
        public async Task Search_MultiWord_StillReturnsRelevantResults()
        {
            var results = await _handler.Handle(
                new SearchProductsQuery("sugar free", 20), CancellationToken.None);

            // "Sugar Free Candy" matches both words
            Assert.NotEmpty(results);
            Assert.Contains(results, r => r.Id == _pidNameStartsWith);
        }

        public void Dispose()
        {
            _context?.Dispose();
        }
    }
}
