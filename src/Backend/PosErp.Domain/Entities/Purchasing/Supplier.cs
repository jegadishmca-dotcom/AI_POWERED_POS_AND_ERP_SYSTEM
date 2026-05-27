using System;

namespace PosErp.Domain.Entities.Purchasing;

public class Supplier
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? Gstin { get; set; }
    public string? Phone { get; set; }
    public string PaymentTerms { get; set; } = "NET30";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
