using System;
using System.Collections.Generic;
using PosErp.Domain.Entities.Catalog;
using PosErp.Domain.Entities.Inventory;
using PosErp.Domain.Entities.Purchasing;

namespace PosErp.Domain.Entities.Finance;

public class PurchaseReturn
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StoreId { get; set; }
    public Guid SupplierId { get; set; }
    public Guid? GRNHeaderId { get; set; }
    
    public string ReturnNumber { get; set; } = string.Empty;
    public DateTime ReturnDate { get; set; }
    public decimal SubTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = "DRAFT"; // DRAFT, APPROVED, POSTED
    public Guid? JournalEntryId { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }

    public ICollection<PurchaseReturnItem> Items { get; set; } = new List<PurchaseReturnItem>();
    public Supplier Supplier { get; set; } = null!;
    public GRNHeader? GRNHeader { get; set; }
}

public class PurchaseReturnItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PurchaseReturnId { get; set; }
    public Guid ProductId { get; set; }
    public Guid BatchId { get; set; }
    
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }

    public PurchaseReturn PurchaseReturn { get; set; } = null!;
    public Product Product { get; set; } = null!;
    public ProductBatch ProductBatch { get; set; } = null!;
}

public class SalesReturn
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StoreId { get; set; }
    public Guid InvoiceId { get; set; }
    public DateTime BusinessDate { get; set; }
    
    public string ReturnNumber { get; set; } = string.Empty;
    public DateTime ReturnDate { get; set; }
    public decimal SubTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal RefundAmount { get; set; }
    public string RefundMode { get; set; } = string.Empty; // CASH, UPI, CREDIT_NOTE
    public string Status { get; set; } = "COMPLETED";
    public Guid? JournalEntryId { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }

    public ICollection<SalesReturnItem> Items { get; set; } = new List<SalesReturnItem>();
}

public class SalesReturnItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SalesReturnId { get; set; }
    public Guid ProductId { get; set; }
    public Guid BatchId { get; set; }
    
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }

    public SalesReturn SalesReturn { get; set; } = null!;
    public Product Product { get; set; } = null!;
    public ProductBatch ProductBatch { get; set; } = null!;
}
