using System;
using PosErp.Domain.Entities.Purchasing;

namespace PosErp.Domain.Entities.Finance;

public class SupplierRebate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StoreId { get; set; }
    public Guid SupplierId { get; set; }
    
    public string RebateProgramName { get; set; } = string.Empty;
    public decimal? Percentage { get; set; }
    public decimal FixedAmount { get; set; } = 0;
    public decimal EarnedAmount { get; set; } = 0;
    
    public string Status { get; set; } = "ACTIVE"; // ACTIVE, CLAIMED, EXPIRED
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Supplier Supplier { get; set; } = null!;
}
