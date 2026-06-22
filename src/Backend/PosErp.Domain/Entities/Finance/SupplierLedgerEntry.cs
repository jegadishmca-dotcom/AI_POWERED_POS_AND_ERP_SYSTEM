using System;
using PosErp.Domain.Entities.Purchasing;

namespace PosErp.Domain.Entities.Finance;

public class SupplierLedgerEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StoreId { get; set; }
    public Guid SupplierId { get; set; }
    public DateTime EntryDate { get; set; }
    public string TransactionType { get; set; } = string.Empty; // BILL, PAYMENT, DEBIT_NOTE, CREDIT_NOTE
    public string ReferenceNumber { get; set; } = string.Empty;
    public decimal DebitAmount { get; set; }
    public decimal CreditAmount { get; set; }
    public decimal RunningBalance { get; set; }
    public string? Description { get; set; }
    public Guid? JournalEntryId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Supplier Supplier { get; set; } = null!;
    public JournalEntry? JournalEntry { get; set; }
}
