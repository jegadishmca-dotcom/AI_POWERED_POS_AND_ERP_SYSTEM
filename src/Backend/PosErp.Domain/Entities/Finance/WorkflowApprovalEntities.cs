using System;
using PosErp.Domain.Entities.Auth;

namespace PosErp.Domain.Entities.Finance;

public class ApprovalLimit
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StoreId { get; set; }
    public string RequestType { get; set; } = string.Empty; // SUPPLIER_PAYMENT, JOURNAL_ADJUSTMENT, ASSET_PURCHASE
    public decimal ManagerLimit { get; set; }
    public decimal OwnerLimit { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class ApprovalRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StoreId { get; set; }
    public string RequestType { get; set; } = string.Empty;
    public Guid TargetId { get; set; }
    public decimal Amount { get; set; }
    public Guid RequestedById { get; set; }
    public string Status { get; set; } = "PENDING"; // PENDING, APPROVED, REJECTED
    public Guid? ActionedById { get; set; }
    public DateTime? ActionedAt { get; set; }
    public string? Comments { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User RequestedBy { get; set; } = null!;
    public User? ActionedBy { get; set; }
    
    public ICollection<ApprovalRequestStep> Steps { get; set; } = new List<ApprovalRequestStep>();
}

public class ApprovalRequestStep
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ApprovalRequestId { get; set; }
    public int Level { get; set; } // 1, 2, etc.
    public string RoleName { get; set; } = string.Empty; // Manager, Owner
    public string Status { get; set; } = "PENDING"; // PENDING, APPROVED, REJECTED
    
    public Guid? ActionedById { get; set; }
    public DateTime? ActionedAt { get; set; }
    public string? Comments { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ApprovalRequest ApprovalRequest { get; set; } = null!;
    public User? ActionedBy { get; set; }
}
