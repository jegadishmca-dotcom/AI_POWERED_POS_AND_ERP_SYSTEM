$backendDir = "d:\JEGADISH\AI_POWERED_POS_AND_ERP_SYSTEM\src\Backend"

# 1. Update CatalogEntities.cs
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

public class UnitOfMeasure
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? StoreId { get; set; }
    public string Name { get; set; } = string.Empty; // e.g., Kgs, Pcs
    public string Symbol { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
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
    
    // Categorization & Tax
    public Guid? CategoryId { get; set; }
    public Guid? BrandId { get; set; }
    public Guid TaxSlabId { get; set; }
    public Guid UnitOfMeasureId { get; set; }
    public string? HsnCode { get; set; }
    
    // Properties
    public bool IsWeighable { get; set; }
    public bool HasExpiry { get; set; }
    
    // Stock Thresholds (Actual stock handled by StockLedger)
    public decimal MinStockLevel { get; set; }
    public decimal ReorderPoint { get; set; }
    
    // Base Financials (decimal(18,4))
    public decimal Mrp { get; set; }
    public decimal SellingPrice { get; set; }
    public decimal PurchasePrice { get; set; }
    
    public bool IsActive { get; set; } = true;
    
    // Postgres TSVector column (mapped in EF configuration usually)
    public string? SearchVector { get; set; } 
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }

    public ICollection<Barcode> Barcodes { get; set; } = new List<Barcode>();
    public ICollection<ProductVariant> Variants { get; set; } = new List<ProductVariant>();
    public ICollection<ProductPriceList> PriceLists { get; set; } = new List<ProductPriceList>();
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
    public string VariantName { get; set; } = string.Empty;
    public decimal SellingPrice { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; }
    
    public Product Product { get; set; } = null!;
}

public class ProductPriceList
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProductId { get; set; }
    public string PriceListName { get; set; } = string.Empty; // e.g., "Wholesale", "Festival Offer"
    public decimal SellingPrice { get; set; }
    public DateTime ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
    public bool IsActive { get; set; } = true;
    public Product Product { get; set; } = null!;
}
"@ | Out-File -FilePath "$backendDir\PosErp.Domain\Entities\Catalog\CatalogEntities.cs" -Encoding utf8


# 2. Update SearchProductsQuery.cs with Full-Text Search Matches
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

        var productsQuery = _context.Products
            .Include(p => p.Barcodes)
            .Where(p => !p.IsDeleted && p.IsActive);

        if (!string.IsNullOrEmpty(q))
        {
            // Mandatory Full-Text Search using Postgres MATCHES operator (@@)
            // It relies on the pre-computed 'search_vector' column.
            
            // Note: In EF Core for PostgreSQL (Npgsql), ToTsQuery formats words with '&' for exact match logic
            string formattedQuery = string.Join(" & ", q.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(w => w + ":*"));
            
            productsQuery = productsQuery.Where(p => 
                EF.Functions.ToTsVector("english", p.SearchVector!).Matches(EF.Functions.ToTsQuery("english", formattedQuery))
                || EF.Functions.ILike(p.ProductCode, $"%{q}%") // Direct code fallback
                || p.Barcodes.Any(b => b.BarcodeValue == q)); // Exact barcode match is faster
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


# 3. Update ImportProductsCommand.cs to use Hangfire and Transaction
@"
using MediatR;
using Microsoft.AspNetCore.Http;
using PosErp.Application.Interfaces;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Hangfire;

namespace PosErp.Application.Features.Catalog.Commands.ImportProducts;

public record ImportProductsCommand(IFormFile File) : IRequest<string>;

public class ImportProductsCommandHandler : IRequestHandler<ImportProductsCommand, string>
{
    private readonly IBackgroundJobClient _backgroundJobClient;

    public ImportProductsCommandHandler(IBackgroundJobClient backgroundJobClient)
    {
        _backgroundJobClient = backgroundJobClient;
    }

    public async Task<string> Handle(ImportProductsCommand request, CancellationToken cancellationToken)
    {
        if (request.File == null || request.File.Length == 0)
        {
            throw new ArgumentException("Empty file.");
        }

        // Save file temporarily for background processing
        var tempPath = Path.GetTempFileName();
        using (var stream = new FileStream(tempPath, FileMode.Create))
        {
            await request.File.CopyToAsync(stream, cancellationToken);
        }

        // Enqueue background job (Hangfire)
        var jobId = _backgroundJobClient.Enqueue<ICsvImportService>(x => x.ProcessProductCsvAsync(tempPath));

        return $"Import started successfully. Job ID: {jobId}";
    }
}

// Background service interface (implementation would handle the Transaction and Validation)
public interface ICsvImportService
{
    Task ProcessProductCsvAsync(string filePath);
}
"@ | Out-File -FilePath "$backendDir\PosErp.Application\Features\Catalog\Commands\ImportProducts\ImportProductsCommand.cs" -Encoding utf8

# 4. Generate the Full-Text Search Trigger SQL Script explicitly for sharing
$sqlScript = @"
-- ==============================================================================
-- PRODUCT FULL-TEXT SEARCH TRIGGER & FUNCTION
-- Updates search_vector automatically on INSERT/UPDATE of product or its barcodes
-- ==============================================================================

-- 1. Create the function
CREATE OR REPLACE FUNCTION products_search_vector_trigger() RETURNS trigger AS `$$
DECLARE
  barcodes_str TEXT;
BEGIN
  -- Aggregate all associated barcodes for the product
  SELECT string_agg(barcode, ' ') INTO barcodes_str
  FROM barcodes
  WHERE product_id = NEW.id AND is_deleted = false;

  -- Build the TSVECTOR with weights
  NEW.search_vector :=
    setweight(to_tsvector('english', coalesce(NEW.name, '')), 'A') ||
    setweight(to_tsvector('english', coalesce(NEW.product_code, '')), 'A') ||
    setweight(to_tsvector('english', coalesce(NEW.tamil_name, '')), 'B') ||
    setweight(to_tsvector('english', coalesce(NEW.hsn_code, '')), 'C') ||
    setweight(to_tsvector('english', coalesce(barcodes_str, '')), 'A');
    
  RETURN NEW;
END
`$$ LANGUAGE plpgsql;

-- 2. Attach trigger to Products
DROP TRIGGER IF EXISTS trg_products_search_vector ON products;
CREATE TRIGGER trg_products_search_vector
BEFORE INSERT OR UPDATE ON products
FOR EACH ROW EXECUTE FUNCTION products_search_vector_trigger();

-- 3. Create a secondary trigger on Barcodes to update the Product's search_vector when a barcode changes
CREATE OR REPLACE FUNCTION update_product_search_vector_from_barcode() RETURNS trigger AS `$$
BEGIN
  IF TG_OP = 'DELETE' THEN
    UPDATE products SET updated_at = NOW() WHERE id = OLD.product_id;
    RETURN OLD;
  ELSE
    UPDATE products SET updated_at = NOW() WHERE id = NEW.product_id;
    RETURN NEW;
  END IF;
END
`$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_barcodes_update_product ON barcodes;
CREATE TRIGGER trg_barcodes_update_product
AFTER INSERT OR UPDATE OR DELETE ON barcodes
FOR EACH ROW EXECUTE FUNCTION update_product_search_vector_from_barcode();

-- 4. Create Indexes
CREATE INDEX IF NOT EXISTS idx_products_search ON products USING GIN(search_vector);
CREATE INDEX IF NOT EXISTS idx_barcodes_barcode ON barcodes(barcode) WHERE is_deleted = FALSE;
CREATE INDEX IF NOT EXISTS idx_products_code ON products(product_code) WHERE is_deleted = FALSE;
CREATE INDEX IF NOT EXISTS idx_products_hsn ON products(hsn_code) WHERE is_deleted = FALSE;
"@
$sqlScript | Out-File -FilePath "$backendDir\PosErp.Infrastructure\Persistence\Migrations\02_ProductSearchTrigger.sql" -Encoding utf8

Write-Host "Catalog Enhanced"
