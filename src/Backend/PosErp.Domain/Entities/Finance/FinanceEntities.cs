using System;
using System.Collections.Generic;

namespace PosErp.Domain.Entities.Finance;

public class Account
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string AccountCode { get; set; } = string.Empty; // e.g., 1000, 2000
    public string Name { get; set; } = string.Empty;
    public string AccountType { get; set; } = string.Empty; // ASSET, LIABILITY, EQUITY, REVENUE, EXPENSE
    public Guid? ParentAccountId { get; set; } // For hierarchical reporting
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class JournalEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? StoreId { get; set; }
    public string EntryNumber { get; set; } = string.Empty; // e.g., JE-20260515-001
    public DateTime EntryDate { get; set; }
    public string Description { get; set; } = string.Empty; // e.g., "POS Invoice #12345"
    public string ReferenceDocument { get; set; } = string.Empty; // Source document ID/Number
    public bool IsPosted { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }

    public ICollection<JournalEntryLine> Lines { get; set; } = new List<JournalEntryLine>();
}

public class JournalEntryLine
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid JournalEntryId { get; set; }
    public Guid AccountId { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal DebitAmount { get; set; } = 0;
    public decimal CreditAmount { get; set; } = 0;

    public JournalEntry JournalEntry { get; set; } = null!;
    public Account Account { get; set; } = null!;
}

public class TaxTransaction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? StoreId { get; set; }
    public string TransactionType { get; set; } = string.Empty; // SALE, PURCHASE, RETURN
    public string DocumentNumber { get; set; } = string.Empty;
    public DateTime TransactionDate { get; set; }
    
    public decimal TaxableAmount { get; set; }
    public decimal CgstAmount { get; set; }
    public decimal SgstAmount { get; set; }
    public decimal IgstAmount { get; set; }
    public decimal CessAmount { get; set; }
    
    public string? Gstin { get; set; } // B2B if present
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
