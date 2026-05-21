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
    public TaxSlab TaxSlab { get; set; } = null!;
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
