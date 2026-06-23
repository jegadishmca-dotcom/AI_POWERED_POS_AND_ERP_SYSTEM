using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Analytics.Services;

public interface IAiAnalyticsService
{
    Task RecalculateAllAnalyticsAsync(CancellationToken cancellationToken);
    Task RecalculateIncrementalAnalyticsAsync(CancellationToken cancellationToken);
    Task<AiDashboardSummaryDto> GetDashboardSummaryAsync(Guid? storeId, CancellationToken cancellationToken);
    Task SubmitRecommendationFeedbackAsync(Guid recommendationId, string status, string? notes, Guid userId, CancellationToken cancellationToken);
}

public class AiDashboardSummaryDto
{
    public Guid? StoreId { get; set; }
    public string StoreName { get; set; } = "Consolidated / HQ";
    
    // KPIs Summary
    public List<KpiSummaryItemDto> Kpis { get; set; } = new();
    
    // Cash Flow Summary
    public CashFlowForecastSummaryDto CashFlowForecast { get; set; } = new();
    
    // Recommendations Summary
    public int PendingRecommendationsCount { get; set; }
    public decimal RecommendedPaymentTotal { get; set; }
    
    // Anomalies
    public int ActiveAnomaliesCount { get; set; }
    public List<AnomalySummaryItemDto> RecentAnomalies { get; set; } = new();
    
    // Expiry Risks
    public int CriticalExpiryBatchesCount { get; set; }
    public decimal ExpiryPotentialLossTotal { get; set; }
    
    // Shrinkage Summary
    public decimal TotalShrinkageLoss { get; set; }
    public decimal ShrinkageRatePct { get; set; }
    public string OverallShrinkageRisk { get; set; } = "LOW";
    
    // Active Alerts
    public List<AlertSummaryItemDto> ActiveAlerts { get; set; } = new();
    
    // Store Benchmarking (Only populated for Consolidated / HQ requests)
    public List<StoreBenchmarkDto>? StoreRankings { get; set; }
}

public class KpiSummaryItemDto
{
    public string KpiType { get; set; } = string.Empty;
    public string KpiName { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public decimal? HistoricalChangePct { get; set; } // Change compared to previous snapshot
}

public class CashFlowForecastSummaryDto
{
    public decimal CurrentAvailableCash { get; set; }
    public decimal Projected30DayInflows { get; set; }
    public decimal Projected30DayOutflows { get; set; }
    public decimal ProjectedEndingBalance { get; set; }
    public List<CashFlowForecastDailyDto> DailyForecasts { get; set; } = new();
}

public class CashFlowForecastDailyDto
{
    public DateTime Date { get; set; }
    public decimal ProjectedInflow { get; set; }
    public decimal ProjectedOutflow { get; set; }
    public decimal ProjectedBalance { get; set; }
    public string ConfidenceLevel { get; set; } = "HIGH";
}

public class AnomalySummaryItemDto
{
    public Guid Id { get; set; }
    public string AnomalyType { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime DetectedAt { get; set; }
}

public class AlertSummaryItemDto
{
    public Guid Id { get; set; }
    public string AlertType { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class StoreBenchmarkDto
{
    public Guid StoreId { get; set; }
    public string StoreCode { get; set; } = string.Empty;
    public string StoreName { get; set; } = string.Empty;
    public int Ranking { get; set; }
    public decimal TotalSales { get; set; }
    public decimal NetProfitMargin { get; set; }
    public decimal CashierVarianceRate { get; set; }
    public decimal ShrinkageRatePct { get; set; }
}
