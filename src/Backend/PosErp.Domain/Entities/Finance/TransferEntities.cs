using System;
using System.Collections.Generic;
using PosErp.Domain.Entities.Catalog;
using PosErp.Domain.Entities.Inventory;

namespace PosErp.Domain.Entities.Finance;

public class InterStoreTransfer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TransferNumber { get; set; } = string.Empty;
    public Guid FromStoreId { get; set; }
    public Guid ToStoreId { get; set; }
    public DateTime TransferDate { get; set; }
    public string Status { get; set; } = "DRAFT"; // DRAFT, SHIPPED, RECEIVED
    public Guid? JournalEntryId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedById { get; set; }

    public ICollection<InterStoreTransferItem> Items { get; set; } = new List<InterStoreTransferItem>();
}

public class InterStoreTransferItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TransferId { get; set; }
    public Guid ProductId { get; set; }
    public Guid BatchId { get; set; }
    
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }

    public InterStoreTransfer Transfer { get; set; } = null!;
    public Product Product { get; set; } = null!;
    public ProductBatch ProductBatch { get; set; } = null!;
}
