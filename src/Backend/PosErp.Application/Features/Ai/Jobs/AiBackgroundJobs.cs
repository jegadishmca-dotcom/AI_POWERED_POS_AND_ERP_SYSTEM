using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PosErp.Application.Features.Ai.Services;
using PosErp.Application.Interfaces;
using PosErp.Domain.Entities.Audit;
using PosErp.Domain.Entities.Finance;

namespace PosErp.Application.Features.Ai.Jobs;

public class AiBackgroundJobs : IAiBackgroundJobs
{
    private readonly IApplicationDbContext _context;
    private readonly IInsightEngine _insightEngine;
    private readonly IForecastEngine _forecastEngine;
    private readonly IRecommendationEngine _recommendationEngine;
    private readonly ILogger<AiBackgroundJobs> _logger;

    public AiBackgroundJobs(
        IApplicationDbContext context,
        IInsightEngine insightEngine,
        IForecastEngine forecastEngine,
        IRecommendationEngine recommendationEngine,
        ILogger<AiBackgroundJobs> logger)
    {
        _context = context;
        _insightEngine = insightEngine;
        _forecastEngine = forecastEngine;
        _recommendationEngine = recommendationEngine;
        _logger = logger;
    }

    public async Task ExecuteInsightGenerationJobAsync(CancellationToken cancellationToken)
    {
        await ExecuteWithAuditAsync("InsightGenerationJob", async () =>
        {
            var today = DateTime.UtcNow.Date;
            
            // Idempotency check: Have we generated insights today?
            var alreadyRan = await _context.AiBusinessInsights
                .AnyAsync(i => i.CreatedAt >= today && i.InsightCategory != "Recommendation", cancellationToken);
                
            if (alreadyRan)
            {
                _logger.LogInformation("InsightGenerationJob: Insights already generated for today. Skipping.");
                return 0; // 0 records processed
            }

            var insights = await _insightEngine.GenerateInsightsAsync(null, today, cancellationToken);
            _context.AiBusinessInsights.AddRange(insights);
            await _context.SaveChangesAsync(cancellationToken);
            return insights.Count;
        }, cancellationToken);
    }

    public async Task ExecuteForecastGenerationJobAsync(CancellationToken cancellationToken)
    {
        await ExecuteWithAuditAsync("ForecastGenerationJob", async () =>
        {
            var today = DateTime.UtcNow.Date;
            
            // Idempotency check
            var alreadyRan = await _context.AiDemandForecasts
                .AnyAsync(f => f.ForecastDate == today, cancellationToken);
                
            if (alreadyRan)
            {
                _logger.LogInformation("ForecastGenerationJob: Forecasts already generated for today. Skipping.");
                return 0;
            }

            var forecasts = await _forecastEngine.GenerateDemandForecastsAsync(null, today, cancellationToken);
            _context.AiDemandForecasts.AddRange(forecasts);
            await _context.SaveChangesAsync(cancellationToken);
            return forecasts.Count;
        }, cancellationToken);
    }

    public async Task ExecuteCustomerIntelligenceJobAsync(CancellationToken cancellationToken)
    {
        await ExecuteWithAuditAsync("CustomerIntelligenceJob", async () =>
        {
            var today = DateTime.UtcNow.Date;
            
            // Idempotency
            var alreadyRan = await _context.AiCustomerIntelligences
                .AnyAsync(c => c.LastCalculatedAt >= today, cancellationToken);
                
            if (alreadyRan)
            {
                _logger.LogInformation("CustomerIntelligenceJob: Customer Intelligence already generated for today. Skipping.");
                return 0;
            }

            var intelligence = await _insightEngine.GenerateCustomerIntelligenceAsync(null, today, cancellationToken);
            
            // We usually upsert or clear existing intelligence
            var existing = await _context.AiCustomerIntelligences.ToListAsync(cancellationToken);
            _context.AiCustomerIntelligences.RemoveRange(existing);
            
            _context.AiCustomerIntelligences.AddRange(intelligence);
            await _context.SaveChangesAsync(cancellationToken);
            return intelligence.Count;
        }, cancellationToken);
    }

    public async Task ExecuteExecutiveSnapshotJobAsync(CancellationToken cancellationToken)
    {
        await ExecuteWithAuditAsync("ExecutiveSnapshotJob", async () =>
        {
            var today = DateTime.UtcNow.Date;
            
            var alreadyRan = await _context.ExecutiveKpiSnapshots
                .AnyAsync(s => s.SnapshotDate == today, cancellationToken);
                
            if (alreadyRan)
            {
                _logger.LogInformation("ExecutiveSnapshotJob: Snapshot already captured for today. Skipping.");
                return 0;
            }

            // Capture actual metrics
            var startOfDay = today;
            var endOfDay = today.AddDays(1).AddTicks(-1);

            decimal dailySales = await _context.Invoices
                .Where(i => i.Status == "COMPLETED" && i.BusinessDate >= startOfDay && i.BusinessDate <= endOfDay && !i.IsDeleted)
                .SumAsync(i => i.NetPayable, cancellationToken);

            // Mock Profit & Margin for now
            decimal dailyProfit = dailySales * 0.25m; // 25% mock
            decimal margin = 25.0m;

            // Loyalty
            int activeMembers = await _context.Customers.CountAsync(c => c.RunningLoyaltyPoints > 0, cancellationToken);
            int activeCustomers = await _context.Customers.CountAsync(cancellationToken);

            var snapshot = new ExecutiveKpiSnapshot
            {
                SnapshotDate = today,
                DailySales = dailySales,
                DailyProfit = dailyProfit,
                GrossMarginPct = margin,
                TotalInventoryValue = 500000m, // Mock, would call Valuation Service
                DeadStockValue = 10000m, // Mock
                ActiveLoyaltyMembers = activeMembers,
                ActiveCustomers = activeCustomers,
                CreatedAt = DateTime.UtcNow
            };

            _context.ExecutiveKpiSnapshots.Add(snapshot);
            await _context.SaveChangesAsync(cancellationToken);
            return 1;
        }, cancellationToken);
    }

    public async Task ExecuteForecastAccuracyJobAsync(CancellationToken cancellationToken)
    {
        await ExecuteWithAuditAsync("ForecastAccuracyJob", async () =>
        {
            var today = DateTime.UtcNow.Date;
            
            var alreadyRan = await _context.ForecastAccuracySnapshots
                .AnyAsync(a => a.SnapshotDate == today, cancellationToken);
                
            if (alreadyRan)
            {
                _logger.LogInformation("ForecastAccuracyJob: Accuracy already evaluated for today. Skipping.");
                return 0;
            }

            var accuracy = await _forecastEngine.EvaluateForecastAccuracyAsync(today, cancellationToken);
            _context.ForecastAccuracySnapshots.Add(accuracy);
            await _context.SaveChangesAsync(cancellationToken);
            return 1;
        }, cancellationToken);
    }

    public async Task ExecuteAlertGenerationJobAsync(CancellationToken cancellationToken)
    {
        await ExecuteWithAuditAsync("AlertGenerationJob", async () =>
        {
            // Hourly job: Generate missing alerts
            var startOfHour = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, DateTime.UtcNow.Day, DateTime.UtcNow.Hour, 0, 0, DateTimeKind.Utc);
            
            var alreadyRan = await _context.AiAlerts
                .AnyAsync(a => a.CreatedAt >= startOfHour, cancellationToken);
                
            if (alreadyRan)
            {
                // We might still generate new ones, but for strict idempotency we skip
                _logger.LogInformation("AlertGenerationJob: Alerts already generated this hour. Skipping.");
                return 0;
            }

            // Find critical expiries not already alerted
            var unalertedExpiries = await _context.AiExpiryRiskPredictions
                .Where(e => e.RiskCategory == "CRITICAL")
                .ToListAsync(cancellationToken);

            int count = 0;
            foreach(var e in unalertedExpiries)
            {
                // Check if an unresolved alert already exists for this batch
                var exists = await _context.AiAlerts.AnyAsync(a => a.AlertType == "EXPIRY" && a.ResolvedAt == null && a.Message.Contains(e.BatchNumber), cancellationToken);
                if (!exists)
                {
                    _context.AiAlerts.Add(new AiAlert
                    {
                        StoreId = e.StoreId,
                        AlertType = "EXPIRY",
                        AlertSeverity = "CRITICAL",
                        Title = "Critical Expiry Risk Detected",
                        Message = $"Batch {e.BatchNumber} for product '{e.ProductName}' is at critical expiry risk.",
                        CreatedAt = DateTime.UtcNow
                    });
                    count++;
                }
            }

            await _context.SaveChangesAsync(cancellationToken);
            return count;
        }, cancellationToken);
    }

    private async Task ExecuteWithAuditAsync(string jobName, Func<Task<int>> action, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var auditLog = new AuditLog
        {
            Action = jobName,
            EntityType = "BackgroundJob",
            EntityId = "SYSTEM",
            Timestamp = DateTime.UtcNow,
            UserId = Guid.Empty
        };

        try
        {
            _logger.LogInformation($"Starting background job: {jobName}");
            int recordsProcessed = await action();
            
            stopwatch.Stop();
            auditLog.Details = $"SUCCESS: Processed {recordsProcessed} records in {stopwatch.ElapsedMilliseconds} ms.";
            _logger.LogInformation($"Completed {jobName} in {stopwatch.ElapsedMilliseconds} ms. Records: {recordsProcessed}");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            auditLog.Details = $"FAILED: {ex.Message} in {stopwatch.ElapsedMilliseconds} ms.";
            _logger.LogError(ex, $"Failed background job: {jobName}");
            throw; // Rethrow to let Hangfire handle retries
        }
        finally
        {
            _context.AuditLogs.Add(auditLog);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
