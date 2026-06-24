using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Features.Ai.Services;
using PosErp.Infrastructure.Persistence;

namespace PosErp.Api.Controllers;

[Authorize(Roles = "Admin,Owner,Manager")]
[ApiController]
[Route("api/ai/insights")]
public class AiInsightsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IInsightEngine _insightEngine;

    public AiInsightsController(ApplicationDbContext context, IInsightEngine insightEngine)
    {
        _context = context;
        _insightEngine = insightEngine;
    }

    [HttpGet]
    public async Task<IActionResult> GetInsights([FromQuery] string? status)
    {
        var query = _context.AiBusinessInsights.AsQueryable();
        
        if (!string.IsNullOrEmpty(status))
        {
            query = query.Where(i => i.Status == status);
        }

        var insights = await query.OrderByDescending(i => i.ImpactScore).ToListAsync();
        return Ok(insights);
    }
    
    [HttpPost("generate")]
    [Authorize(Roles = "Admin,Owner")]
    public async Task<IActionResult> GenerateInsights([FromQuery] Guid? storeId)
    {
        var insights = await _insightEngine.GenerateInsightsAsync(storeId, DateTime.Today, CancellationToken.None);
        _context.AiBusinessInsights.AddRange(insights);
        await _context.SaveChangesAsync();
        return Ok(insights);
    }

    [HttpPut("{id}/status")]
    public async Task<IActionResult> UpdateInsightStatus(Guid id, [FromBody] UpdateInsightStatusRequest request)
    {
        var insight = await _context.AiBusinessInsights.FindAsync(id);
        if (insight == null) return NotFound();

        insight.Status = request.Status;
        if (request.Status == "RESOLVED" || request.Status == "IGNORED")
        {
            insight.ResolvedDate = DateTime.UtcNow;
            insight.ResolutionNotes = request.ResolutionNotes;
        }

        await _context.SaveChangesAsync();
        return Ok(insight);
    }
}

public class UpdateInsightStatusRequest
{
    public string Status { get; set; } = string.Empty;
    public string? ResolutionNotes { get; set; }
}
