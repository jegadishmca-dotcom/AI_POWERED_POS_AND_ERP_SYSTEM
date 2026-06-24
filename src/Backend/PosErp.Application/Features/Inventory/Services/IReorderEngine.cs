using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Inventory.Services;

public class ReorderSuggestion
{
    public Guid ProductId { get; set; }
    public Guid InventoryLocationId { get; set; }
    public decimal CurrentStock { get; set; }
    public decimal RecommendedQuantity { get; set; }
    public decimal MinimumOrderQuantity { get; set; }
    public decimal EconomicOrderQuantity { get; set; }
    public int OrderMultiple { get; set; }
    public Guid? PreferredSupplierId { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public interface IReorderEngine
{
    /// <summary>
    /// Calculates reorder suggestions for a specific inventory location based on its policy,
    /// current stock levels, and daily sales velocity.
    /// </summary>
    Task<List<ReorderSuggestion>> GenerateReorderSuggestionsAsync(Guid locationId, DateTime businessDate);
}
