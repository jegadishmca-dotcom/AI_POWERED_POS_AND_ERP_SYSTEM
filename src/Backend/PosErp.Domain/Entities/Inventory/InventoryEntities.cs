using System;
using System.Collections.Generic;
using PosErp.Domain.Entities.Catalog;

namespace PosErp.Domain.Entities.Inventory;

public class ProductBatch
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? StoreId { get; set; }
    public Guid ProductId { get; set; }
    
    public string BatchNumber { get; set; } = string.Empty;
    public DateTime? MfgDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    
    public decimal Mrp { get; set; }
    public decimal CostPrice { get; set; }
    
    public decimal AvailableQuantity { get; set; }
    public string? GrnReference { get; set; }
    
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public Product Product { get; set; } = null!;
}

public class StockLedgerEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? StoreId { get; set; }
    public Guid? WarehouseId { get; set; } // NEW
    public Guid? TerminalId { get; set; } // NEW
    public DateTime BusinessDate { get; set; } // NEW
    
    public Guid ProductId { get; set; }
    public Guid? BatchId { get; set; }
    
    public string MovementType { get; set; } = string.Empty; 
    
    public decimal Quantity { get; set; } 
    public decimal UnitCost { get; set; } // NEW
    public DateTime? ExpiryDate { get; set; } // NEW
    
    public Guid ReferenceDocumentId { get; set; } 
    public string ReferenceNumber { get; set; } = string.Empty;
    
    public decimal RunningBalance { get; set; } 
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    
    // For Postgres optimistic concurrency, 'xmin' is usually a hidden system column, 
    // but in EF Core we can map a uint RowVersion to it.
    public uint Version { get; set; } 
    
    public Product Product { get; set; } = null!;
}

public class GoodsReceiptNote
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? StoreId { get; set; }
    public Guid? PurchaseOrderId { get; set; }
    public Guid SupplierId { get; set; }
    public string GrnNumber { get; set; } = string.Empty;
    public string SupplierInvoiceNumber { get; set; } = string.Empty;
    public DateTime ReceivedDate { get; set; }
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = "DRAFT"; 
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    public ICollection<GoodsReceiptNoteItem> Items { get; set; } = new List<GoodsReceiptNoteItem>();
}

public class GoodsReceiptNoteItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GoodsReceiptNoteId { get; set; }
    public Guid ProductId { get; set; }
    public Guid? BatchId { get; set; }
    public decimal ReceivedQuantity { get; set; }
    public decimal AcceptedQuantity { get; set; }
    public decimal RejectedQuantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }
    public GoodsReceiptNote GoodsReceiptNote { get; set; } = null!;
}

public class StockAdjustment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? StoreId { get; set; }
    public string AdjustmentNumber { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty; 
    public string Status { get; set; } = "PENDING"; 
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? ApprovedBy { get; set; }
    public ICollection<StockAdjustmentItem> Items { get; set; } = new List<StockAdjustmentItem>();
}

public class StockAdjustmentItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StockAdjustmentId { get; set; }
    public Guid ProductId { get; set; }
    public Guid? BatchId { get; set; }
    public decimal AdjustedQuantity { get; set; } 
    public decimal UnitCost { get; set; }
    public StockAdjustment StockAdjustment { get; set; } = null!;
}

public class PendingPriceApproval
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Barcode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal ExistingCostPrice { get; set; }
    public decimal NewCostPrice { get; set; }
    public decimal Quantity { get; set; }
    public string InvoiceReference { get; set; } = string.Empty;
    public string Status { get; set; } = "PENDING"; // PENDING, APPROVED, REJECTED
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ActionedAt { get; set; }
    public Guid? ActionedBy { get; set; }
}

public class InventoryLocation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string LocationType { get; set; } = "STORE"; // STORE, WAREHOUSE, DC
    public Guid? ParentLocationId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class ProductStoreInventoryPolicy
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProductId { get; set; }
    public Guid InventoryLocationId { get; set; }
    
    public decimal MinStockLevel { get; set; }
    public decimal MaxStockLevel { get; set; }
    public decimal ReorderPoint { get; set; }
    public decimal SafetyStock { get; set; }
    public int LeadTimeDays { get; set; }
    
    // Phase 4 Enhancements
    public decimal EconomicOrderQuantity { get; set; }
    public int ReorderFrequencyDays { get; set; }
    public Guid? PreferredSupplierId { get; set; }
    public int PreferredOrderMultiple { get; set; } = 1;
    public bool IsAutoReorderEnabled { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    
    public Product Product { get; set; } = null!;
}

public class InventoryForecast
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProductId { get; set; }
    public Guid InventoryLocationId { get; set; }
    
    public DateTime ForecastDate { get; set; }
    public string ForecastModelVersion { get; set; } = string.Empty;
    
    public decimal HistoricalDemand { get; set; }
    public decimal PredictedDemand { get; set; }
    public decimal ConfidencePercentage { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public Product Product { get; set; } = null!;
}

public class InventoryAgingSnapshot
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProductId { get; set; }
    public Guid InventoryLocationId { get; set; }
    public DateTime SnapshotDate { get; set; }
    
    public decimal Age0To30 { get; set; }
    public decimal Age31To60 { get; set; }
    public decimal Age61To90 { get; set; }
    public decimal Age91To180 { get; set; }
    public decimal Age180Plus { get; set; }
    
    public Product Product { get; set; } = null!;
}

public class InventoryAuditEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InventoryLocationId { get; set; }
    public Guid ProductId { get; set; }
    public Guid? BatchId { get; set; }
    
    public decimal BeforeQuantity { get; set; }
    public decimal AfterQuantity { get; set; }
    public string Reason { get; set; } = string.Empty; // DAMAGE, EXPIRED, SHRINKAGE, CORRECTION
    
    public Guid UserId { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class StockTransferRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SourceLocationId { get; set; }
    public Guid DestinationLocationId { get; set; }
    public Guid ProductId { get; set; }
    
    public decimal Quantity { get; set; }
    public string Status { get; set; } = "PENDING"; // PENDING, APPROVED, IN_TRANSIT, RECEIVED
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    
    public DateTime? DispatchedAt { get; set; }
    public DateTime? ReceivedAt { get; set; }
}
