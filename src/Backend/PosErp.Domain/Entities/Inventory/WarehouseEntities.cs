using System;
using System.Collections.Generic;

namespace PosErp.Domain.Entities.Inventory;

public class Warehouse
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? StoreId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public ICollection<Bin> Bins { get; set; } = new List<Bin>();
}

public class Bin
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WarehouseId { get; set; }
    public string Code { get; set; } = string.Empty; // e.g. A1-01
    public string Description { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public Warehouse Warehouse { get; set; } = null!;
}

public class StockTakeHeader
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? StoreId { get; set; }
    public string TakeNumber { get; set; } = string.Empty;
    public DateTime ScheduledDate { get; set; }
    public string Status { get; set; } = "DRAFT"; // DRAFT, IN_PROGRESS, REVIEW, APPROVED
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? ApprovedBy { get; set; }
    public ICollection<StockTakeItem> Items { get; set; } = new List<StockTakeItem>();
}

public class StockTakeItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StockTakeHeaderId { get; set; }
    public Guid ProductId { get; set; }
    public Guid? BatchId { get; set; }
    
    public decimal SystemQuantity { get; set; }
    public decimal PhysicalQuantity { get; set; }
    public decimal VarianceQuantity => PhysicalQuantity - SystemQuantity;
    
    public StockTakeHeader StockTakeHeader { get; set; } = null!;
}
