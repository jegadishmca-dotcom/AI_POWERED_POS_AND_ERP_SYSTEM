using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PosErp.Infrastructure.Persistence;

namespace PosErp.Api.Controllers;

[Authorize(Roles = "Admin,Owner")]
[ApiController]
[Route("api/executive/dashboard")]
public class ExecutiveDashboardController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public ExecutiveDashboardController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet("kpis")]
    public async Task<IActionResult> GetExecutiveKpis()
    {
        var latestSnapshot = await _context.ExecutiveKpiSnapshots
            .OrderByDescending(s => s.SnapshotDate)
            .FirstOrDefaultAsync();

        if (latestSnapshot == null)
        {
            // Fallback or empty if jobs haven't run
            return Ok(new
            {
                DailySales = 0,
                DailyProfit = 0,
                GrossMarginPct = 0,
                TotalInventoryValue = 0,
                DeadStockValue = 0,
                ActiveLoyaltyMembers = 0,
                ActiveCustomers = 0
            });
        }

        // We can also compute live numbers here if snapshot is stale
        // But the architectural intent is to use the snapshot for performance
        return Ok(latestSnapshot);
    }
    
    [HttpGet("trends")]
    public async Task<IActionResult> GetKpiTrends([FromQuery] int days = 30)
    {
        var start = DateTime.UtcNow.AddDays(-days).Date;
        var snapshots = await _context.ExecutiveKpiSnapshots
            .Where(s => s.SnapshotDate >= start)
            .OrderBy(s => s.SnapshotDate)
            .ToListAsync();
            
        return Ok(snapshots);
    }
}
