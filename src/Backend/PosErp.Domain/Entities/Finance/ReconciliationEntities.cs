using System;
using System.Collections.Generic;
using PosErp.Domain.Entities.Purchasing;
using PosErp.Domain.Entities.Crm;

namespace PosErp.Domain.Entities.Finance;

public class BankAccount
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StoreId { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public string BankName { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string IfsCode { get; set; } = string.Empty;
    public string? Branch { get; set; }
    public Guid GlAccountId { get; set; }
    public decimal CurrentBalance { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Account GlAccount { get; set; } = null!;
}

public class BankTransaction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BankAccountId { get; set; }
    public DateTime TransactionDate { get; set; }
    public string Type { get; set; } = string.Empty; // DEPOSIT, WITHDRAWAL, BANK_FEE, INTEREST
    public decimal Amount { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? Description { get; set; }
    public bool IsReconciled { get; set; } = false;
    public DateTime? ReconciledDate { get; set; }
    public Guid? JournalEntryId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public BankAccount BankAccount { get; set; } = null!;
}

public class PettyCashLedgerEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StoreId { get; set; }
    public DateTime TransactionDate { get; set; }
    public string VoucherNumber { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal DebitAmount { get; set; }
    public decimal CreditAmount { get; set; }
    public decimal RunningBalance { get; set; }
    public string? RequestedBy { get; set; }
    public Guid ApprovedById { get; set; }
    public Guid? JournalEntryId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class SupplierPayment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StoreId { get; set; }
    public Guid SupplierId { get; set; }
    public DateTime PaymentDate { get; set; }
    public string PaymentNumber { get; set; } = string.Empty;
    public string PaymentMode { get; set; } = string.Empty; // CASH, BANK_TRANSFER, CHEQUE, UPI
    public string? ReferenceNumber { get; set; }
    public decimal Amount { get; set; }
    public Guid? JournalEntryId { get; set; }
    public string Status { get; set; } = "PENDING_APPROVAL"; // PENDING_APPROVAL, APPROVED, POSTED, VOID
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Supplier Supplier { get; set; } = null!;
    public ICollection<SupplierPaymentAllocation> Allocations { get; set; } = new List<SupplierPaymentAllocation>();
}

public class SupplierPaymentAllocation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PaymentId { get; set; }
    public Guid PurchaseBillId { get; set; }
    public decimal AllocatedAmount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public SupplierPayment SupplierPayment { get; set; } = null!;
    public PurchaseBillHeader PurchaseBill { get; set; } = null!;
}

public class CustomerReceipt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StoreId { get; set; }
    public Guid CustomerId { get; set; }
    public DateTime ReceiptDate { get; set; }
    public string ReceiptNumber { get; set; } = string.Empty;
    public string PaymentMode { get; set; } = string.Empty;
    public string? ReferenceNumber { get; set; }
    public decimal Amount { get; set; }
    public Guid? JournalEntryId { get; set; }
    public string Status { get; set; } = "POSTED";
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Customer Customer { get; set; } = null!;
    public ICollection<CustomerReceiptAllocation> Allocations { get; set; } = new List<CustomerReceiptAllocation>();
}

public class CustomerReceiptAllocation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ReceiptId { get; set; }
    public Guid InvoiceId { get; set; }
    public DateTime InvoiceBusinessDate { get; set; }
    public decimal AllocatedAmount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public CustomerReceipt CustomerReceipt { get; set; } = null!;
}
