using System;

namespace PosErp.Domain.Entities.Auth;

public class Store
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string StoreCode { get; set; } = string.Empty;
    public string StoreName { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? Gstin { get; set; }
    public string? ContactNumber { get; set; }
    public string? Email { get; set; }
    public Guid? ManagerId { get; set; }
    public decimal SquareFootage { get; set; } = 2000.00m;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
}
