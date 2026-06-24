using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;

namespace PosErp.Application.Features.Inventory.Services;

public class PurchaseRecommendationEngine : IPurchaseRecommendationEngine
{
    private readonly IApplicationDbContext _context;
    private readonly IReorderEngine _reorderEngine;

    public PurchaseRecommendationEngine(IApplicationDbContext context, IReorderEngine reorderEngine)
    {
        _context = context;
        _reorderEngine = reorderEngine;
    }

    public async Task<List<PurchaseRecommendation>> GenerateRecommendationsAsync(Guid locationId, DateTime businessDate)
    {
        // 1. Get raw reorder suggestions
        var suggestions = await _reorderEngine.GenerateReorderSuggestionsAsync(locationId, businessDate);

        if (!suggestions.Any())
            return new List<PurchaseRecommendation>();

        var productIds = suggestions.Select(s => s.ProductId).ToList();

        // 2. Calculate daily sales velocity to determine Days Until Stockout
        var thirtyDaysAgo = businessDate.AddDays(-30);
        var salesData = await _context.InvoiceItems
            .Where(i => i.Invoice.StoreId == locationId && i.BusinessDate >= thirtyDaysAgo && i.BusinessDate <= businessDate && productIds.Contains(i.ProductId))
            .GroupBy(i => i.ProductId)
            .Select(g => new
            {
                ProductId = g.Key,
                TotalSold = g.Sum(x => x.Quantity)
            })
            .ToDictionaryAsync(x => x.ProductId, x => x.TotalSold);

        var recommendations = new List<PurchaseRecommendation>();

        foreach (var suggestion in suggestions)
        {
            var totalSold = salesData.ContainsKey(suggestion.ProductId) ? salesData[suggestion.ProductId] : 0m;
            var dailySalesVelocity = totalSold / 30m;

            int daysUntilStockout = int.MaxValue;
            if (dailySalesVelocity > 0)
            {
                daysUntilStockout = (int)Math.Floor(suggestion.CurrentStock / dailySalesVelocity);
            }
            else if (suggestion.CurrentStock <= 0)
            {
                daysUntilStockout = 0; // Already out of stock
            }

            // Priority Classification
            RecommendationPriority priority;
            if (daysUntilStockout <= 7)
                priority = RecommendationPriority.Critical;
            else if (daysUntilStockout <= 14)
                priority = RecommendationPriority.High;
            else if (daysUntilStockout <= 30)
                priority = RecommendationPriority.Medium;
            else
                priority = RecommendationPriority.Low;

            recommendations.Add(new PurchaseRecommendation
            {
                ProductId = suggestion.ProductId,
                InventoryLocationId = suggestion.InventoryLocationId,
                RecommendedQuantity = suggestion.RecommendedQuantity,
                Priority = priority,
                DaysUntilStockout = daysUntilStockout,
                SupplierId = suggestion.PreferredSupplierId,
                Justification = $"Days until stockout: {daysUntilStockout}. " + suggestion.Reason
            });
        }

        return recommendations.OrderBy(r => r.DaysUntilStockout).ToList();
    }
}
