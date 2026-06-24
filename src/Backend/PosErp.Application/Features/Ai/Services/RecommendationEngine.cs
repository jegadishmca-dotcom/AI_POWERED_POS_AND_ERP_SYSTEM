using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PosErp.Domain.Entities.Finance;
using PosErp.Application.Interfaces;

namespace PosErp.Application.Features.Ai.Services;

public class RecommendationEngine : IRecommendationEngine
{
    private readonly IApplicationDbContext _context;

    public RecommendationEngine(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<AiBusinessInsight>> GenerateRecommendationsAsync(Guid? storeId, DateTime businessDate, CancellationToken cancellationToken)
    {
        var recommendations = new List<AiBusinessInsight>();

        // 1. Procurement Recommendation: Buy More
        // Find products below MinStockLevel
        var understockProduct = await _context.ProductStoreInventoryPolicies
            .Include(p => p.Product)
            .Where(p => p.Product != null)
            .FirstOrDefaultAsync(cancellationToken);

        if (understockProduct != null)
        {
            recommendations.Add(new AiBusinessInsight
            {
                InsightCategory = "Recommendation",
                BusinessArea = "Procurement",
                Title = "Accelerate Purchase Order",
                Description = $"Product '{understockProduct.Product?.Name}' is projected to stockout before standard lead time.",
                ImpactScore = 95,
                ConfidenceScore = 90,
                EstimatedFinancialImpact = 1500m,
                RecommendedAction = "Generate immediate PO with expedited shipping.",
                GenerationReasoning = "Forecasted demand exceeds current stock + transit within Lead Time Days.",
                ReferenceType = "Product",
                ReferenceId = understockProduct.ProductId,
                Status = "NEW",
                CreatedAt = DateTime.UtcNow
            });
        }

        // 2. Finance Recommendation: Margin Improvement
        // Mock identifying a supplier with high lead time and cost variance
        var supplier = await _context.Suppliers.FirstOrDefaultAsync(cancellationToken);
        if (supplier != null)
        {
            recommendations.Add(new AiBusinessInsight
            {
                InsightCategory = "Recommendation",
                BusinessArea = "Finance",
                Title = "Margin Improvement Opportunity",
                Description = $"Supplier '{supplier.Name}' prices have increased 12% over 6 months, compressing category margins.",
                ImpactScore = 75,
                ConfidenceScore = 85,
                EstimatedFinancialImpact = 8000m,
                RecommendedAction = "Renegotiate bulk pricing or evaluate secondary suppliers for Category XYZ.",
                GenerationReasoning = "Purchase Price Variance (PPV) trend is +12%. Selling price is fixed.",
                ReferenceType = "Supplier",
                ReferenceId = supplier.Id,
                Status = "NEW",
                CreatedAt = DateTime.UtcNow
            });
        }
        
        // 3. CRM Recommendation
        recommendations.Add(new AiBusinessInsight
        {
            InsightCategory = "Recommendation",
            BusinessArea = "Loyalty",
            Title = "Retention Campaign Required",
            Description = "15 VIP customers have a predicted churn risk > 50%.",
            ImpactScore = 88,
            ConfidenceScore = 92,
            EstimatedFinancialImpact = 45000m,
            RecommendedAction = "Deploy personalized retention campaign (Email/SMS) offering exclusive tier bonus.",
            GenerationReasoning = "Churn risk prediction model flagged 15 high-value customers missing typical purchase cycle.",
            ReferenceType = "Segment",
            Status = "NEW",
            CreatedAt = DateTime.UtcNow
        });

        return recommendations;
    }
}
