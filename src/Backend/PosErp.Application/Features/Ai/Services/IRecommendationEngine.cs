using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PosErp.Domain.Entities.Finance;

namespace PosErp.Application.Features.Ai.Services;

/// <summary>
/// Abstraction for the AI Recommendation Engine.
/// Provides actionable recommendations for procurement, inventory, CRM, and finance.
/// </summary>
public interface IRecommendationEngine
{
    /// <summary>
    /// Generates actionable AI recommendations across multiple business areas.
    /// </summary>
    Task<List<AiBusinessInsight>> GenerateRecommendationsAsync(Guid? storeId, DateTime businessDate, CancellationToken cancellationToken);
}
