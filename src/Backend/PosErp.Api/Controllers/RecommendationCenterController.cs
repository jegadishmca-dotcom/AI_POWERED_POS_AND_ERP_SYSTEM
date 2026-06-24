using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Features.Ai.Services;
using PosErp.Infrastructure.Persistence;

namespace PosErp.Api.Controllers;

[Authorize(Roles = "Admin,Owner,Manager")]
[ApiController]
[Route("api/ai/recommendations")]
public class RecommendationCenterController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IRecommendationEngine _recommendationEngine;

    public RecommendationCenterController(ApplicationDbContext context, IRecommendationEngine recommendationEngine)
    {
        _context = context;
        _recommendationEngine = recommendationEngine;
    }

    [HttpGet]
    public async Task<IActionResult> GetRecommendations([FromQuery] string? businessArea)
    {
        var query = _context.AiBusinessInsights
            .Where(i => i.InsightCategory == "Recommendation");
            
        if (!string.IsNullOrEmpty(businessArea))
        {
            query = query.Where(i => i.BusinessArea == businessArea);
        }

        var recommendations = await query.OrderByDescending(i => i.ImpactScore).ToListAsync();
        return Ok(recommendations);
    }

    [HttpPost("generate")]
    [Authorize(Roles = "Admin,Owner")]
    public async Task<IActionResult> GenerateRecommendations([FromQuery] Guid? storeId)
    {
        var recommendations = await _recommendationEngine.GenerateRecommendationsAsync(storeId, DateTime.Today, CancellationToken.None);
        _context.AiBusinessInsights.AddRange(recommendations);
        await _context.SaveChangesAsync();
        return Ok(recommendations);
    }
}
