using System;
using PosErp.Domain.Entities.Auth;

namespace PosErp.Domain.Entities.Finance;

public class FinancialYear
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty; // e.g., FY-2026-27
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Status { get; set; } = "ACTIVE"; // ACTIVE, CLOSED
    public DateTime? ClosedAt { get; set; }
    public Guid? ClosedById { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User? ClosedBy { get; set; }
}

public class FinancialPeriodLock
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StoreId { get; set; }
    public string PeriodName { get; set; } = string.Empty; // e.g. June 2026
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsLocked { get; set; } = false;
    public Guid? LockedById { get; set; }
    public DateTime? LockedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User? LockedBy { get; set; }
}
