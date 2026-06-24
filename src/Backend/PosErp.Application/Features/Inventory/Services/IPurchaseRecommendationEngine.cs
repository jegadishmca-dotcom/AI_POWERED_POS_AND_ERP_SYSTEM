using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Inventory.Services;

public enum RecommendationPriority
{
    Critical, // Stockout within 7 days
    High,     // Stockout within 14 days
    Medium,   // Stockout within 30 days
    Low       // Monitor only
}

public class PurchaseRecommendation
{
    public Guid ProductId { get; set; }
    public Guid InventoryLocationId { get; set; }
    public decimal RecommendedQuantity { get; set; }
    public RecommendationPriority Priority { get; set; }
    public int DaysUntilStockout { get; set; }
    public Guid? SupplierId { get; set; }
    public string Justification { get; set; } = string.Empty;
}

public interface IPurchaseRecommendationEngine
{
    Task<List<PurchaseRecommendation>> GenerateRecommendationsAsync(Guid locationId, DateTime businessDate);
}
