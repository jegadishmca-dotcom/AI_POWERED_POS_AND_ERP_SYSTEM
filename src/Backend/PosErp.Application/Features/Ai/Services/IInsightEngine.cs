using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PosErp.Domain.Entities.Finance;

namespace PosErp.Application.Features.Ai.Services;

/// <summary>
/// Abstraction for the AI Insight Engine.
/// This interface allows for future ML model integration (Python/gRPC) without code redesign.
/// </summary>
public interface IInsightEngine
{
    /// <summary>
    /// Generates key business observations, risks, and opportunities across all areas.
    /// </summary>
    Task<List<AiBusinessInsight>> GenerateInsightsAsync(Guid? storeId, DateTime businessDate, CancellationToken cancellationToken);
    
    /// <summary>
    /// Evaluates customer purchasing patterns to identify VIP, At-Risk, and Dormant segments.
    /// </summary>
    Task<List<AiCustomerIntelligence>> GenerateCustomerIntelligenceAsync(Guid? storeId, DateTime businessDate, CancellationToken cancellationToken);
}
