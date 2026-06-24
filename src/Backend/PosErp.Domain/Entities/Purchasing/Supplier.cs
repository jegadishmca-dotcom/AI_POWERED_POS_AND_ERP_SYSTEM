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

public class SupplierScorecard
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SupplierId { get; set; }
    
    public DateTime ScorecardDate { get; set; } = DateTime.UtcNow;
    
    public decimal OnTimeDeliveryPercentage { get; set; }
    public decimal PriceCompetitivenessScore { get; set; }
    public decimal QualityScore { get; set; }
    public decimal RejectionRate { get; set; }
    public decimal OverallRating { get; set; }
    
    public DateTime? LastPurchaseDate { get; set; }
    
    public Supplier Supplier { get; set; } = null!;
}
