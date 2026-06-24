using System;

using PosErp.Domain.Entities.Common;

namespace PosErp.Domain.Entities.Audit;

public class AuditLog : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public string? UserName { get; set; }
    public string Action { get; set; } = string.Empty; // e.g. "Offer Created", "Manager Override"
    public string EntityType { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public DateTime Timestamp { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public Guid TenantId { get; set; }
    public string? Details { get; set; } // JSON or text details of what changed
}
