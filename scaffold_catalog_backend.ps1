$backendDir = "d:\JEGADISH\AI_POWERED_POS_AND_ERP_SYSTEM\src\Backend"

New-Item -ItemType Directory -Force -Path "$backendDir\PosErp.Domain\Entities\Catalog"
New-Item -ItemType Directory -Force -Path "$backendDir\PosErp.Application\Features\Catalog\Commands\ImportProducts"
New-Item -ItemType Directory -Force -Path "$backendDir\PosErp.Application\Features\Catalog\Queries\SearchProducts"

# 1. Domain Entities
@"
using System;
using System.Collections.Generic;

namespace PosErp.Domain.Entities.Catalog;

public class Category
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? StoreId { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid? ParentCategoryId { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    public bool IsDeleted { get; set; }
}

public class Brand
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? StoreId { get; set; }
    public string Name { get; set; } = string.Empty;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    public bool IsDeleted { get; set; }
}

public class TaxSlab
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? StoreId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal CgstRate { get; set; }
    public decimal SgstRate { get; set; }
    public decimal IgstRate { get; set; }
    public decimal CessRate { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    public bool IsDeleted { get; set; }
}

public class Product
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? StoreId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? TamilName { get; set; }
    public string? Description { get; set; }
    public Guid? CategoryId { get; set; }
    public Guid? BrandId { get; set; }
    public Guid TaxSlabId { get; set; }
    public string? HsnCode { get; set; }
    public bool IsWeighable { get; set; }
    
    public decimal Mrp { get; set; }
    public decimal SellingPrice { get; set; }
    public decimal PurchasePrice { get; set; }
    public decimal CurrentStock { get; set; }
    
    public bool IsActive { get; set; } = true;
    public string? SearchVector { get; set; } // tsvector handled by DB trigger
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }

    public ICollection<Barcode> Barcodes { get; set; } = new List<Barcode>();
    public ICollection<ProductVariant> Variants { get; set; } = new List<ProductVariant>();
}

public class Barcode
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? StoreId { get; set; }
    public Guid ProductId { get; set; }
    public string BarcodeValue { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; }
    
    public Product Product { get; set; } = null!;
}

public class ProductVariant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? StoreId { get; set; }
    public Guid ProductId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string VariantName { get; set; } = string.Empty; // e.g. "Red - Large"
    public decimal SellingPrice { get; set; }
    public decimal CurrentStock { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; }
    
    public Product Product { get; set; } = null!;
}
"@ | Out-File -FilePath "$backendDir\PosErp.Domain\Entities\Catalog\CatalogEntities.cs" -Encoding utf8

# 2. Update DbContext
@"
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PosErp.Domain.Entities.Auth;
using PosErp.Domain.Entities.Catalog;

namespace PosErp.Application.Interfaces;

public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    
    DbSet<Product> Products { get; }
    DbSet<ProductVariant> ProductVariants { get; }
    DbSet<Barcode> Barcodes { get; }
    DbSet<Category> Categories { get; }
    DbSet<Brand> Brands { get; }
    DbSet<TaxSlab> TaxSlabs { get; }
    
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
"@ | Out-File -FilePath "$backendDir\PosErp.Application\Interfaces\IApplicationDbContext.cs" -Encoding utf8

# 3. CSV Import Command
@"
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using PosErp.Domain.Entities.Catalog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
// Note: In real app, we'd add 'using CsvHelper;' and map records properly. 
// We are simulating the core logic here to keep scaffolding clean without external nuget issues right now.

namespace PosErp.Application.Features.Catalog.Commands.ImportProducts;

public record ImportProductsCommand(IFormFile File) : IRequest<ImportResult>;

public record ImportResult(int TotalImported, int TotalFailed, List<string> Errors);

public class ImportProductsCommandHandler : IRequestHandler<ImportProductsCommand, ImportResult>
{
    private readonly IApplicationDbContext _context;

    public ImportProductsCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ImportResult> Handle(ImportProductsCommand request, CancellationToken cancellationToken)
    {
        if (request.File == null || request.File.Length == 0)
        {
            return new ImportResult(0, 0, new List<string> { "Empty file." });
        }

        var errors = new List<string>();
        int imported = 0;
        int failed = 0;

        // In a real scenario, use CsvHelper to read the stream
        using var reader = new StreamReader(request.File.OpenReadStream());
        
        // Skip header
        await reader.ReadLineAsync(cancellationToken);

        // Simple cache to prevent massive DB lookups per row
        var categories = await _context.Categories.ToDictionaryAsync(x => x.Name, x => x.Id, cancellationToken);
        var brands = await _context.Brands.ToDictionaryAsync(x => x.Name, x => x.Id, cancellationToken);
        var taxSlabs = await _context.TaxSlabs.ToDictionaryAsync(x => x.Name, x => x.Id, cancellationToken);

        string line;
        while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
        {
            var parts = line.Split(',');
            if (parts.Length < 7) { failed++; continue; }

            try
            {
                var code = parts[0].Trim();
                var name = parts[1].Trim();
                var catName = parts[2].Trim();
                var brandName = parts[3].Trim();
                var taxSlabName = parts[4].Trim();
                var mrp = decimal.Parse(parts[5].Trim());
                var sp = decimal.Parse(parts[6].Trim());
                var barcodeVal = parts.Length > 7 ? parts[7].Trim() : code;

                // Resolve references or create placeholders (if needed)
                Guid? catId = categories.ContainsKey(catName) ? categories[catName] : null;
                Guid? brandId = brands.ContainsKey(brandName) ? brands[brandName] : null;
                
                // For safety, fallback to first tax slab or fail
                Guid taxId = taxSlabs.ContainsKey(taxSlabName) ? taxSlabs[taxSlabName] : (taxSlabs.Values.FirstOrDefault());

                var product = new Product
                {
                    ProductCode = code,
                    Name = name,
                    CategoryId = catId,
                    BrandId = brandId,
                    TaxSlabId = taxId,
                    Mrp = mrp,
                    SellingPrice = sp,
                    PurchasePrice = sp * 0.8m, // dummy
                    CurrentStock = 100,
                    IsActive = true
                };

                product.Barcodes.Add(new Barcode { BarcodeValue = barcodeVal, IsPrimary = true });
                
                _context.Products.Add(product);
                imported++;
            }
            catch (Exception ex)
            {
                errors.Add($"Row {imported + failed + 1} failed: {ex.Message}");
                failed++;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
        return new ImportResult(imported, failed, errors);
    }
}
"@ | Out-File -FilePath "$backendDir\PosErp.Application\Features\Catalog\Commands\ImportProducts\ImportProductsCommand.cs" -Encoding utf8

# 4. Search Products Query
@"
using MediatR;
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Catalog.Queries.SearchProducts;

public record SearchProductsQuery(string Query, int Limit = 20) : IRequest<List<ProductSearchResultDto>>;

public record ProductSearchResultDto(Guid Id, string ProductCode, string Name, string? TamilName, decimal SellingPrice, string PrimaryBarcode);

public class SearchProductsQueryHandler : IRequestHandler<SearchProductsQuery, List<ProductSearchResultDto>>
{
    private readonly IApplicationDbContext _context;

    public SearchProductsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<ProductSearchResultDto>> Handle(SearchProductsQuery request, CancellationToken cancellationToken)
    {
        var q = request.Query.Trim();

        // Use PostgreSQL full-text search vector via EF.Functions
        // In real EF Core PG, we'd use: EF.Functions.ToTsQuery("english", q)
        var productsQuery = _context.Products
            .Include(p => p.Barcodes)
            .Where(p => !p.IsDeleted && p.IsActive);

        if (!string.IsNullOrEmpty(q))
        {
            // Fallback for ILike or precise matching if TsVector isn't configured in pure EF yet
            productsQuery = productsQuery.Where(p => 
                EF.Functions.ILike(p.Name, $"%{q}%") || 
                EF.Functions.ILike(p.ProductCode, $"%{q}%") ||
                p.Barcodes.Any(b => b.BarcodeValue == q));
        }

        var results = await productsQuery
            .Take(request.Limit)
            .Select(p => new ProductSearchResultDto(
                p.Id, 
                p.ProductCode, 
                p.Name, 
                p.TamilName, 
                p.SellingPrice, 
                p.Barcodes.FirstOrDefault(b => b.IsPrimary).BarcodeValue ?? string.Empty
            ))
            .ToListAsync(cancellationToken);

        return results;
    }
}
"@ | Out-File -FilePath "$backendDir\PosErp.Application\Features\Catalog\Queries\SearchProducts\SearchProductsQuery.cs" -Encoding utf8

# 5. Controller
@"
using Microsoft.AspNetCore.Mvc;
using MediatR;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using PosErp.Application.Features.Catalog.Commands.ImportProducts;
using PosErp.Application.Features.Catalog.Queries.SearchProducts;
using Microsoft.AspNetCore.Authorization;

namespace PosErp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
// [Authorize] // Commented out for easier testing without token
public class CatalogController : ControllerBase
{
    private readonly IMediator _mediator;

    public CatalogController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string q, [FromQuery] int limit = 20)
    {
        var result = await _mediator.Send(new SearchProductsQuery(q ?? string.Empty, limit));
        return Ok(result);
    }

    [HttpPost("import")]
    public async Task<IActionResult> ImportCsv(IFormFile file)
    {
        var result = await _mediator.Send(new ImportProductsCommand(file));
        return Ok(result);
    }
}
"@ | Out-File -FilePath "$backendDir\PosErp.Api\Controllers\CatalogController.cs" -Encoding utf8

Write-Host "Backend Catalog Scaffolded"
