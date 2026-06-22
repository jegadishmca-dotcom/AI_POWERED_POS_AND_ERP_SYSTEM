using System;
using PosErp.Domain.Entities.Crm;

namespace PosErp.Domain.Entities.Finance;

public class CustomerLedgerEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StoreId { get; set; }
    public Guid CustomerId { get; set; }
    public DateTime EntryDate { get; set; }
    public string TransactionType { get; set; } = string.Empty; // INVOICE, RECEIPT, CREDIT_NOTE, DEBIT_NOTE
    public string ReferenceNumber { get; set; } = string.Empty;
    public decimal DebitAmount { get; set; }
    public decimal CreditAmount { get; set; }
    public decimal RunningBalance { get; set; }
    public string? Description { get; set; }
    public Guid? JournalEntryId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Customer Customer { get; set; } = null!;
    public JournalEntry? JournalEntry { get; set; }
}
