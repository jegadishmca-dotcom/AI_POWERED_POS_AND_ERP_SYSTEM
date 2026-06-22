using System;

namespace PosErp.Domain.Entities.Finance;

public class EInvoiceMetadata
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InvoiceId { get; set; }
    public DateTime BusinessDate { get; set; }
    public string? Irn { get; set; }
    public string? AckNumber { get; set; }
    public DateTime? AckDate { get; set; }
    public string? QrCodeContent { get; set; }
    public string Status { get; set; } = "PENDING"; // PENDING, GENERATED, CANCELLED, FAILED
    public string? ErrorMessage { get; set; }
    public int SyncAttempts { get; set; }
    public DateTime? LastSyncAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class EWayBillMetadata
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ReferenceType { get; set; } = string.Empty; // INVOICE, INTER_STORE_TRANSFER
    public Guid ReferenceId { get; set; }
    public string? EWayBillNumber { get; set; }
    public DateTime? IssueDate { get; set; }
    public DateTime? ValidUntil { get; set; }
    public string? VehicleNumber { get; set; }
    public int DistanceKm { get; set; }
    public string Status { get; set; } = "ACTIVE"; // ACTIVE, CANCELLED
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
