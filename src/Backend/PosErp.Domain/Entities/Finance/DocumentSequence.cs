using System;

namespace PosErp.Domain.Entities.Finance;

public class DocumentSequence
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StoreId { get; set; }
    public string DocumentType { get; set; } = string.Empty; // INVOICE, PURCHASE_BILL, SUPPLIER_PAYMENT, etc.
    public string Prefix { get; set; } = string.Empty;
    public int CurrentNumber { get; set; }
    public int Padding { get; set; } = 6;
    public string? Suffix { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
