using System;

namespace PosErp.Domain.Entities.Offers;

public class Offer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    
    public string OfferType { get; set; } = string.Empty; // PERCENTAGE, FLAT, BOGO, COMBO
    public string RulesJson { get; set; } = "{}"; // Complex rules engine configuration
    
    public string? PromoCode { get; set; } // Null if automatically applied
    public int Priority { get; set; } = 0; // Higher runs first
    public bool IsStackable { get; set; } = false; // Can this combine with others?
    public bool IsExclusive { get; set; } = false; // If true, overrides all other offers
    public int? MaxUsagePerInvoice { get; set; }
    
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
