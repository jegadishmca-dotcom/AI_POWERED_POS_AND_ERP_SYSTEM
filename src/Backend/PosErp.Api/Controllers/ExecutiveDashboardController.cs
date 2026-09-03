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

    private static DateTime GetTodayIst()
    {
        TimeZoneInfo istTz;
        try
        {
            istTz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");
        }
        catch
        {
            istTz = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
        }
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, istTz).Date;
    }

    [HttpGet("kpis")]
    public async Task<IActionResult> GetExecutiveKpis()
    {
        var todayIst = GetTodayIst();
        var latestSnapshot = await _context.ExecutiveKpiSnapshots
            .Where(s => s.SnapshotDate <= todayIst)
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
        var todayIst = GetTodayIst();
        var start = todayIst.AddDays(-days);
        var snapshots = await _context.ExecutiveKpiSnapshots
            .Where(s => s.SnapshotDate >= start && s.SnapshotDate <= todayIst)
            .OrderBy(s => s.SnapshotDate)
            .ToListAsync();
            
        return Ok(snapshots);
    }
}
