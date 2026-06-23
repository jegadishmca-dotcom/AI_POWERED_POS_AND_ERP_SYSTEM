using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using PosErp.Application.Features.Analytics.Services;
using Hangfire;

namespace PosErp.Api.Controllers;

[ApiController]
[Route("api/ai")]
public class AiFinanceController : ControllerBase
{
    private readonly IAiAnalyticsService _analyticsService;
    private readonly INaturalLanguageQueryService _nlQueryService;
    private readonly IApplicationDbContext _context;

    public AiFinanceController(
        IAiAnalyticsService analyticsService,
        INaturalLanguageQueryService nlQueryService,
        IApplicationDbContext context)
    {
        _analyticsService = analyticsService;
        _nlQueryService = nlQueryService;
        _context = context;
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboardSummary([FromQuery] Guid? storeId, CancellationToken cancellationToken)
    {
        var summary = await _analyticsService.GetDashboardSummaryAsync(storeId, cancellationToken);
        return Ok(summary);
    }

    [HttpGet("kpis")]
    public async Task<IActionResult> GetKpis([FromQuery] Guid? storeId, CancellationToken cancellationToken)
    {
        var kpis = await _context.AiKpiResults
            .AsNoTracking()
            .Where(k => k.StoreId == storeId)
            .ToListAsync(cancellationToken);
        return Ok(kpis);
    }

    [HttpGet("cash-flow")]
    public async Task<IActionResult> GetCashFlowForecast([FromQuery] Guid? storeId, CancellationToken cancellationToken)
    {
        var forecasts = await _context.AiCashFlowForecasts
            .AsNoTracking()
            .Where(f => f.StoreId == storeId)
            .OrderBy(f => f.ForecastDate)
            .ToListAsync(cancellationToken);
        return Ok(forecasts);
    }

    [HttpGet("supplier-recommendations")]
    public async Task<IActionResult> GetSupplierRecommendations([FromQuery] Guid? storeId, CancellationToken cancellationToken)
    {
        var query = _context.AiSupplierPaymentRecommendations.AsNoTracking();
        if (storeId.HasValue)
        {
            query = from r in query
                    join b in _context.PurchaseBills on r.PurchaseBillId equals b.Id
                    where b.StoreId == storeId
                    select r;
        }
        var recs = await query.ToListAsync(cancellationToken);
        return Ok(recs);
    }

    [HttpPost("supplier-recommendations/{id}/feedback")]
    public async Task<IActionResult> SubmitFeedback(Guid id, [FromBody] FeedbackRequest request, CancellationToken cancellationToken)
    {
        var callerIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        Guid.TryParse(callerIdStr, out Guid userId);
        if (userId == Guid.Empty)
        {
            // Fallback for tests/unauthenticated calls in sandbox
            userId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        }

        try
        {
            await _analyticsService.SubmitRecommendationFeedbackAsync(id, request.Status, request.Notes, userId, cancellationToken);
            return Ok(new { message = "Feedback submitted successfully." });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("anomalies")]
    public async Task<IActionResult> GetAnomalies(CancellationToken cancellationToken)
    {
        var anomalies = await _context.AiFinancialAnomalies
            .AsNoTracking()
            .Where(a => !a.IsResolved)
            .ToListAsync(cancellationToken);
        return Ok(anomalies);
    }

    [HttpPost("anomalies/{id}/resolve")]
    public async Task<IActionResult> ResolveAnomaly(Guid id, CancellationToken cancellationToken)
    {
        var callerIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        Guid.TryParse(callerIdStr, out Guid userId);
        if (userId == Guid.Empty)
        {
            userId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        }

        var anomaly = await _context.AiFinancialAnomalies.FindAsync(new object[] { id }, cancellationToken);
        if (anomaly == null) return NotFound(new { message = "Anomaly not found." });

        anomaly.IsResolved = true;
        anomaly.ResolvedAt = DateTime.UtcNow;
        anomaly.ResolvedBy = userId;

        await _context.SaveChangesAsync(cancellationToken);
        return Ok(new { message = "Anomaly marked as resolved." });
    }

    [HttpGet("shrinkage")]
    public async Task<IActionResult> GetShrinkageAnalytics([FromQuery] Guid? storeId, CancellationToken cancellationToken)
    {
        var shrinkage = await _context.AiInventoryShrinkageAnalytics
            .AsNoTracking()
            .Where(x => x.StoreId == storeId)
            .ToListAsync(cancellationToken);
        return Ok(shrinkage);
    }

    [HttpGet("expiry-risks")]
    public async Task<IActionResult> GetExpiryRisks([FromQuery] Guid? storeId, CancellationToken cancellationToken)
    {
        var expiries = await _context.AiExpiryRiskPredictions
            .AsNoTracking()
            .Where(x => x.StoreId == storeId)
            .ToListAsync(cancellationToken);
        return Ok(expiries);
    }

    [HttpGet("alerts")]
    public async Task<IActionResult> GetAlerts([FromQuery] Guid? storeId, CancellationToken cancellationToken)
    {
        var alerts = await _context.AiAlerts
            .AsNoTracking()
            .Where(a => a.StoreId == storeId && !a.IsRead)
            .ToListAsync(cancellationToken);
        return Ok(alerts);
    }

    [HttpPost("alerts/{id}/read")]
    public async Task<IActionResult> MarkAlertAsRead(Guid id, CancellationToken cancellationToken)
    {
        var alert = await _context.AiAlerts.FindAsync(new object[] { id }, cancellationToken);
        if (alert == null) return NotFound(new { message = "Alert not found." });

        alert.IsRead = true;
        await _context.SaveChangesAsync(cancellationToken);
        return Ok(new { message = "Alert marked as read." });
    }

    [HttpPost("recalculate")]
    public async Task<IActionResult> RecalculateAll()
    {
        // Enqueue background processing job via Hangfire
        string jobId = BackgroundJob.Enqueue<IAiAnalyticsService>(
            service => service.RecalculateAllAnalyticsAsync(CancellationToken.None));
        
        return Accepted(new { jobId = jobId, message = "Background analytics recalculation enqueued." });
    }

    [HttpPost("query")]
    public async Task<IActionResult> ExecuteNlQuery([FromBody] NlQueryRequest request, [FromQuery] Guid? storeId, CancellationToken cancellationToken)
    {
        var result = await _nlQueryService.ParseAndExecuteQueryAsync(request.Query, storeId, cancellationToken);
        return Ok(result);
    }
}

public class FeedbackRequest
{
    public string Status { get; set; } = string.Empty; // ACCEPTED, REJECTED, PENDING
    public string? Notes { get; set; }
}

public class NlQueryRequest
{
    public string Query { get; set; } = string.Empty;
}
