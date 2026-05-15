using System;
using System.Collections.Generic;

namespace PosErp.Domain.Entities.Pos;

public class Invoice
{
    // Composite Key in DB: (Id, BusinessDate)
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? StoreId { get; set; }
    public DateTime BusinessDate { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    
    public Guid TerminalId { get; set; }
    public int TerminalSequence { get; set; }
    public Guid CashierId { get; set; }
    
    public decimal SubTotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal RoundOff { get; set; }
    public decimal NetPayable { get; set; }
    
    // E-Invoicing Hooks
    public string? Irn { get; set; }
    public string? AckNo { get; set; }
    public DateTime? AckDate { get; set; }
    public string? QrCode { get; set; }
    
    public string Status { get; set; } = "COMPLETED"; // COMPLETED, CANCELLED, HOLD
    public string PaymentMode { get; set; } = "CASH"; // CASH, CARD, UPI
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }

    public ICollection<InvoiceItem> Items { get; set; } = new List<InvoiceItem>();
}

public class InvoiceItem
{
    // Composite Key in DB: (Id, BusinessDate)
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? StoreId { get; set; }
    public Guid InvoiceId { get; set; }
    public DateTime BusinessDate { get; set; }
    
    public Guid ProductId { get; set; }
    public string? Barcode { get; set; }
    public string ProductName { get; set; } = string.Empty;
    
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal DiscountAmount { get; set; }
    
    public decimal CgstRate { get; set; }
    public decimal CgstAmount { get; set; }
    public decimal SgstRate { get; set; }
    public decimal SgstAmount { get; set; }
    public decimal CessRate { get; set; }
    public decimal CessAmount { get; set; }
    
    public decimal TotalAmount { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    public bool IsDeleted { get; set; }
    
    public Invoice Invoice { get; set; } = null!;
}
