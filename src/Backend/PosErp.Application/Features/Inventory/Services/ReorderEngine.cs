using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using PosErp.Domain.Entities.Inventory;

namespace PosErp.Application.Features.Inventory.Services;

public class ReorderEngine : IReorderEngine
{
    private readonly IApplicationDbContext _context;

    public ReorderEngine(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<ReorderSuggestion>> GenerateReorderSuggestionsAsync(Guid locationId, DateTime businessDate)
    {
        var suggestions = new List<ReorderSuggestion>();

        // 1. Get all active policies for the location
        var policies = await _context.ProductStoreInventoryPolicies
            .Where(p => p.InventoryLocationId == locationId && p.IsAutoReorderEnabled)
            .ToListAsync();

        if (!policies.Any())
            return suggestions;

        var productIds = policies.Select(p => p.ProductId).ToList();

        // 2. Get current stock balances for these products at the location
        // We aggregate StockLedger entries. Since StockLedgerEntry doesn't have an explicit 'InventoryLocationId' mapped initially,
        // we'll use StoreId/WarehouseId based on how they map to Location. 
        // For now, we assume StoreId = InventoryLocationId.
        
        var currentStocks = await _context.StockLedger
            .Where(s => (s.StoreId == locationId || s.WarehouseId == locationId) && productIds.Contains(s.ProductId))
            .GroupBy(s => s.ProductId)
            .Select(g => new
            {
                ProductId = g.Key,
                RunningBalance = g.OrderByDescending(x => x.CreatedAt).FirstOrDefault().RunningBalance
            })
            .ToDictionaryAsync(x => x.ProductId, x => x.RunningBalance);

        // 3. Calculate 30-day Sales Velocity (Average daily sales)
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

        // 4. Generate Suggestions
        foreach (var policy in policies)
        {
            var currentStock = currentStocks.ContainsKey(policy.ProductId) ? currentStocks[policy.ProductId] : 0m;
            var totalSold30Days = salesData.ContainsKey(policy.ProductId) ? salesData[policy.ProductId] : 0m;
            var dailySalesVelocity = totalSold30Days / 30m;

            // Trigger reorder if current stock <= ReorderPoint
            if (currentStock <= policy.ReorderPoint)
            {
                // Formula: (SalesVelocity * LeadTimeDays) + SafetyStock - CurrentStock
                var rawRecommendedQty = (dailySalesVelocity * policy.LeadTimeDays) + policy.SafetyStock - currentStock;

                if (rawRecommendedQty <= 0)
                    continue;

                // Apply EOQ if defined
                var recommendedQty = policy.EconomicOrderQuantity > 0 
                    ? Math.Max(rawRecommendedQty, policy.EconomicOrderQuantity) 
                    : rawRecommendedQty;

                // Apply Order Multiple (Round up to nearest multiple)
                if (policy.PreferredOrderMultiple > 1)
                {
                    var multiple = (decimal)policy.PreferredOrderMultiple;
                    recommendedQty = Math.Ceiling(recommendedQty / multiple) * multiple;
                }

                // Enforce Min Stock / Max Stock boundaries
                if (currentStock + recommendedQty > policy.MaxStockLevel && policy.MaxStockLevel > 0)
                {
                    recommendedQty = policy.MaxStockLevel - currentStock;
                    // Re-apply multiple downwards to not exceed max
                    if (policy.PreferredOrderMultiple > 1 && recommendedQty > 0)
                    {
                        var multiple = (decimal)policy.PreferredOrderMultiple;
                        recommendedQty = Math.Floor(recommendedQty / multiple) * multiple;
                    }
                }

                if (recommendedQty > 0)
                {
                    suggestions.Add(new ReorderSuggestion
                    {
                        ProductId = policy.ProductId,
                        InventoryLocationId = locationId,
                        CurrentStock = currentStock,
                        RecommendedQuantity = recommendedQty,
                        MinimumOrderQuantity = policy.MinStockLevel, // Storing MinStock context
                        EconomicOrderQuantity = policy.EconomicOrderQuantity,
                        OrderMultiple = policy.PreferredOrderMultiple,
                        PreferredSupplierId = policy.PreferredSupplierId,
                        Reason = $"Current stock ({currentStock}) is below reorder point ({policy.ReorderPoint}). Daily velocity: {dailySalesVelocity:F2}"
                    });
                }
            }
        }

        return suggestions;
    }
}
