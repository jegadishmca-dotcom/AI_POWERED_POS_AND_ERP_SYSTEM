using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PosErp.Infrastructure.Persistence;

namespace PosErp.Api.Controllers;

[Authorize(Roles = "Admin,Owner,Manager")]
[ApiController]
[Route("api/ai/alerts")]
public class AlertCenterController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public AlertCenterController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetAlerts([FromQuery] string? severity, [FromQuery] bool includeResolved = false)
    {
        try
        {
            var query = _context.AiAlerts.AsQueryable();

            if (!includeResolved)
            {
                query = query.Where(a => a.ResolvedAt == null);
            }

            if (!string.IsNullOrEmpty(severity))
            {
                query = query.Where(a => a.AlertSeverity == severity);
            }

            var alerts = await query
                .OrderByDescending(a => a.CreatedAt)
                .Select(a => new {
                    a.Id,
                    a.StoreId,
                    a.AlertType,
                    a.AlertSeverity,
                    a.Title,
                    a.Message,
                    a.IsRead,
                    a.CreatedAt,
                    a.ResolvedAt,
                    a.ResolvedBy
                })
                .ToListAsync();
            var jsonString = System.Text.Json.JsonSerializer.Serialize(alerts);
            return Content(jsonString, "application/json");
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Error = ex.Message, Inner = ex.InnerException?.Message, StackTrace = ex.StackTrace });
        }
    }

    [HttpPut("{id}/acknowledge")]
    public async Task<IActionResult> AcknowledgeAlert(Guid id)
    {
        var alert = await _context.AiAlerts.FindAsync(id);
        if (alert == null) return NotFound();

        alert.IsRead = true;
        await _context.SaveChangesAsync();
        return Ok(alert);
    }
    
    [HttpPut("{id}/resolve")]
    public async Task<IActionResult> ResolveAlert(Guid id)
    {
        var alert = await _context.AiAlerts.FindAsync(id);
        if (alert == null) return NotFound();

        alert.ResolvedAt = DateTime.UtcNow;
        // In a real app we'd get the user ID from claims: User.FindFirst(ClaimTypes.NameIdentifier)?.Value
        // alert.ResolvedBy = Guid.Parse(User.GetUserId());

        await _context.SaveChangesAsync();
        return Ok(alert);
    }
}
