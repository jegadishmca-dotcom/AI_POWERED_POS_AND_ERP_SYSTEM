using System;
using System.Collections.Generic;

namespace PosErp.Domain.Entities.Purchasing;

public class PurchaseOrderHeader
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? StoreId { get; set; }
    public Guid SupplierId { get; set; }
    
    public string PoNumber { get; set; } = string.Empty;
    public DateTime PoDate { get; set; }
    public DateTime ExpectedDeliveryDate { get; set; }
    
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = "DRAFT"; // DRAFT, APPROVED, PARTIAL_GRN, FULL_GRN, CANCELLED
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    
    public ICollection<PurchaseOrderItem> Items { get; set; } = new List<PurchaseOrderItem>();
}

public class PurchaseOrderItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PurchaseOrderHeaderId { get; set; }
    public Guid ProductId { get; set; }
    
    public decimal OrderedQuantity { get; set; }
    public decimal ReceivedQuantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }
    
    public PurchaseOrderHeader PurchaseOrderHeader { get; set; } = null!;
}

public class GRNHeader
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? StoreId { get; set; }
    public Guid PurchaseOrderHeaderId { get; set; }
    public Guid SupplierId { get; set; }
    
    public string GrnNumber { get; set; } = string.Empty;
    public string SupplierInvoiceNumber { get; set; } = string.Empty;
    public DateTime ReceivedDate { get; set; }
    
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = "DRAFT"; // DRAFT, CONFIRMED
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    
    public ICollection<GRNItem> Items { get; set; } = new List<GRNItem>();
}

public class GRNItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GRNHeaderId { get; set; }
    public Guid PurchaseOrderItemId { get; set; }
    public Guid ProductId { get; set; }
    public Guid? BatchId { get; set; } // Filled when GRN confirmed
    
    // Batch Info collected during GRN
    public string BatchNumber { get; set; } = string.Empty;
    public DateTime? ExpiryDate { get; set; }
    public DateTime? MfgDate { get; set; }
    
    public decimal ReceivedQuantity { get; set; }
    public decimal AcceptedQuantity { get; set; }
    public decimal RejectedQuantity { get; set; }
    public string? RejectionReason { get; set; }
    
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }
    
    public GRNHeader GRNHeader { get; set; } = null!;
}

public class PurchaseBillHeader
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? StoreId { get; set; }
    public Guid SupplierId { get; set; }
    public Guid GRNHeaderId { get; set; }
    
    public string BillNumber { get; set; } = string.Empty;
    public DateTime BillDate { get; set; }
    
    public decimal SubTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = "PENDING_PAYMENT"; 
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    
    public ICollection<PurchaseBillItem> Items { get; set; } = new List<PurchaseBillItem>();
}

public class PurchaseBillItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PurchaseBillHeaderId { get; set; }
    public Guid ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public PurchaseBillHeader PurchaseBillHeader { get; set; } = null!;
}

