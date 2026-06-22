using System;

namespace PosErp.Domain.Entities.Finance;

public class FixedAsset
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StoreId { get; set; }
    public string AssetCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    
    public DateTime PurchaseDate { get; set; }
    public decimal PurchaseCost { get; set; }
    public decimal SalvageValue { get; set; }
    public int UsefulLifeYears { get; set; }
    public string DepreciationMethod { get; set; } = string.Empty; // STRAIGHT_LINE, WRITTEN_DOWN_VALUE
    public decimal DepreciationRate { get; set; }
    
    public Guid AssetAccountId { get; set; }
    public Guid AccumulatedDeprAccountId { get; set; }
    public Guid DepreciationExpenseAccountId { get; set; }
    public decimal CurrentBookValue { get; set; }
    
    public string Status { get; set; } = "ACTIVE"; // ACTIVE, DISPOSED, WRITTEN_OFF
    public DateTime? DisposalDate { get; set; }
    public decimal DisposalValue { get; set; }
    public decimal DisposalGainLoss { get; set; }
    public Guid? JournalEntryId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Account AssetAccount { get; set; } = null!;
    public Account AccumulatedDeprAccount { get; set; } = null!;
    public Account DepreciationExpenseAccount { get; set; } = null!;
}

public class AssetDepreciationHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AssetId { get; set; }
    public DateTime DepreciationDate { get; set; }
    public decimal Amount { get; set; }
    public decimal BookValueBefore { get; set; }
    public decimal BookValueAfter { get; set; }
    public Guid? JournalEntryId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public FixedAsset FixedAsset { get; set; } = null!;
}
