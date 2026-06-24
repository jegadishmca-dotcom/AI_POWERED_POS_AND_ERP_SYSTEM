using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PosErp.Domain.Entities.Finance;
using PosErp.Application.Interfaces;

namespace PosErp.Application.Features.Ai.Services;

public class InsightEngine : IInsightEngine
{
    private readonly IApplicationDbContext _context;

    public InsightEngine(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<AiBusinessInsight>> GenerateInsightsAsync(Guid? storeId, DateTime businessDate, CancellationToken cancellationToken)
    {
        var insights = new List<AiBusinessInsight>();

        // 1. Observation: Fast-moving products nearing stockout
        // Fetch products with High Velocity but < 7 Days Until Stockout
        var nearingStockoutQuery = _context.ProductBatches
            .Include(b => b.Product)
            .Where(b => b.AvailableQuantity > 0 && b.IsActive);
            
        if (storeId.HasValue) nearingStockoutQuery = nearingStockoutQuery.Where(b => b.StoreId == storeId);
        
        var batches = await nearingStockoutQuery.ToListAsync(cancellationToken);
        
        // Mock logic: randomly flag 1 product for demonstration
        var criticalProduct = batches.FirstOrDefault();
        if (criticalProduct != null)
        {
            insights.Add(new AiBusinessInsight
            {
                InsightCategory = "Risk",
                BusinessArea = "Inventory",
                Title = "High-Velocity Product Nearing Stockout",
                Description = $"Product '{criticalProduct.Product?.Name}' is moving fast but has less than 7 days of stock remaining.",
                ImpactScore = 85,
                ConfidenceScore = 90,
                EstimatedFinancialImpact = criticalProduct.CostPrice * 100, // mock estimate
                RecommendedAction = "Trigger emergency reorder from preferred supplier.",
                GenerationReasoning = $"Velocity is 15 units/day. Current stock: {criticalProduct.AvailableQuantity}. Days until stockout: 3.",
                ReferenceType = "Product",
                ReferenceId = criticalProduct.ProductId,
                Status = "NEW",
                CreatedAt = DateTime.UtcNow
            });
        }

        // 2. Observation: High-margin products with declining sales
        // Mock logic for demonstration
        var highMarginProduct = await _context.Products.FirstOrDefaultAsync(cancellationToken);
        if (highMarginProduct != null)
        {
            insights.Add(new AiBusinessInsight
            {
                InsightCategory = "Opportunity",
                BusinessArea = "Sales",
                Title = "Declining Sales on High-Margin Product",
                Description = $"Product '{highMarginProduct.Name}' maintains a 42% margin but sales dropped 35% over 30 days.",
                ImpactScore = 70,
                ConfidenceScore = 85,
                EstimatedFinancialImpact = 5000m,
                RecommendedAction = "Include in next weekly promotion or prominent end-cap display.",
                GenerationReasoning = "Margin > 40%. Sales volume 30d vs previous 30d shows -35% variance.",
                ReferenceType = "Product",
                ReferenceId = highMarginProduct.Id,
                Status = "NEW",
                CreatedAt = DateTime.UtcNow
            });
        }

        return insights;
    }

    public async Task<List<AiCustomerIntelligence>> GenerateCustomerIntelligenceAsync(Guid? storeId, DateTime businessDate, CancellationToken cancellationToken)
    {
        var intelligence = new List<AiCustomerIntelligence>();

        // Heuristic: Process top 50 customers to assign segments
        var customers = await _context.Customers.Take(50).ToListAsync(cancellationToken);

        foreach (var customer in customers)
        {
            // Simple heuristic mapping
            string segment = "Active";
            string churnCat = "Low";
            decimal churnRisk = 10.0m;
            string recommendedAction = "Send monthly newsletter.";
            string ltvCategory = "Standard";

            if (customer.LifetimeSpend > 10000)
            {
                segment = "VIP";
                ltvCategory = "High Value";
                churnRisk = 5.0m;
                recommendedAction = "Assign personal account manager.";
            }
            else if (customer.RunningLoyaltyPoints > 5000)
            {
                segment = "High Value";
                ltvCategory = "High Value";
                churnRisk = 15.0m;
                recommendedAction = "Offer double points weekend.";
            }

            // Simulate At-Risk/Dormant
            if (customer.Dob.HasValue && customer.Dob.Value.Month == businessDate.Month)
            {
                segment = "At Risk"; // Mock trigger
                churnRisk = 80.0m;
                churnCat = "High";
                recommendedAction = "Send personalized Birthday Discount of 20% to prevent churn.";
            }

            intelligence.Add(new AiCustomerIntelligence
            {
                CustomerId = customer.Id,
                SegmentType = segment,
                ChurnRiskPct = churnRisk,
                LtvPrediction = customer.LifetimeSpend * 1.2m, // mock LTV
                LifetimeValueCategory = ltvCategory,
                PredictedNextPurchaseDate = businessDate.AddDays(14),
                ChurnCategory = churnCat,
                RecommendedAction = recommendedAction,
                LastCalculatedAt = DateTime.UtcNow
            });
        }

        return intelligence;
    }
}
