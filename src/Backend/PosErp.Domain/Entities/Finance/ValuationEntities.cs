using System;
using PosErp.Domain.Entities.Catalog;
using PosErp.Domain.Entities.Inventory;

namespace PosErp.Domain.Entities.Finance;

public class InventoryValuationHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StoreId { get; set; }
    public DateTime BusinessDate { get; set; }
    public Guid ProductId { get; set; }
    public Guid BatchId { get; set; }
    
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalValuation { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Product Product { get; set; } = null!;
    public ProductBatch ProductBatch { get; set; } = null!;
}
