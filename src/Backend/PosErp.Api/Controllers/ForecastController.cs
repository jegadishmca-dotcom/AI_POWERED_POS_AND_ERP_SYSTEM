using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Features.Ai.Services;
using PosErp.Infrastructure.Persistence;

namespace PosErp.Api.Controllers;

[Authorize(Roles = "Admin,Owner,Manager")]
[ApiController]
[Route("api/ai/forecasts")]
public class ForecastController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IForecastEngine _forecastEngine;

    public ForecastController(ApplicationDbContext context, IForecastEngine forecastEngine)
    {
        _context = context;
        _forecastEngine = forecastEngine;
    }

    [HttpGet]
    public async Task<IActionResult> GetForecasts([FromQuery] string type = "PRODUCT")
    {
        var forecasts = await _context.AiDemandForecasts
            .Where(f => f.ForecastType == type)
            .OrderByDescending(f => f.ForecastDate)
            .Take(100)
            .ToListAsync();
            
        return Ok(forecasts);
    }

    [HttpGet("accuracy")]
    public async Task<IActionResult> GetForecastAccuracy()
    {
        var accuracy = await _context.ForecastAccuracySnapshots
            .OrderByDescending(a => a.SnapshotDate)
            .Take(10)
            .ToListAsync();
            
        return Ok(accuracy);
    }
    
    [HttpPost("generate")]
    [Authorize(Roles = "Admin,Owner")]
    public async Task<IActionResult> GenerateForecasts([FromQuery] Guid? storeId)
    {
        var forecasts = await _forecastEngine.GenerateDemandForecastsAsync(storeId, DateTime.Today, CancellationToken.None);
        _context.AiDemandForecasts.AddRange(forecasts);
        
        var accuracy = await _forecastEngine.EvaluateForecastAccuracyAsync(DateTime.Today, CancellationToken.None);
        _context.ForecastAccuracySnapshots.Add(accuracy);
        
        await _context.SaveChangesAsync();
        return Ok(new { ForecastsGenerated = forecasts.Count, CurrentAccuracy = accuracy });
    }
}
