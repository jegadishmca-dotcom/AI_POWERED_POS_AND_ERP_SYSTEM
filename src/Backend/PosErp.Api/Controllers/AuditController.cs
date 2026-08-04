using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;

namespace PosErp.Api.Controllers;

/// <summary>
/// Provides read-only access to the audit_logs table for ERP administrators.
/// Supports filtered queries by action type, date range, user, and entity type.
/// All endpoints require Manager or Owner role — audit data is sensitive.
/// </summary>
[ApiController]
[Route("api/audit")]
[Authorize(Roles = "Manager,Owner,Developer")]
public class AuditController : ControllerBase
{
    private readonly IApplicationDbContext _context;

    public AuditController(IApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// GET /api/audit/logs
    /// Returns paginated audit log entries, optionally filtered.
    ///
    /// Query parameters:
    ///   - actions    : comma-separated list of action names to include (default: all)
    ///   - entityType : filter by entity type (e.g. "CartLineItem", "Offer", "InventoryBatch")
    ///   - from       : ISO 8601 start date (UTC), e.g. 2026-08-01
    ///   - to         : ISO 8601 end date (UTC), e.g. 2026-08-31
    ///   - userId     : filter by a specific user GUID
    ///   - search     : free-text search inside the Details JSON column
    ///   - page       : page number (1-indexed, default 1)
    ///   - pageSize   : entries per page (default 50, max 200)
    /// </summary>
    [HttpGet("logs")]
    public async Task<IActionResult> GetLogs(
        [FromQuery] string? actions    = null,
        [FromQuery] string? entityType = null,
        [FromQuery] DateTime? from     = null,
        [FromQuery] DateTime? to       = null,
        [FromQuery] Guid? userId       = null,
        [FromQuery] string? search     = null,
        [FromQuery] int page           = 1,
        [FromQuery] int pageSize       = 50)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 1;
        if (pageSize > 200) pageSize = 200;

        var query = _context.AuditLogs.AsNoTracking().AsQueryable();

        // Filter: action types (comma-separated)
        if (!string.IsNullOrWhiteSpace(actions))
        {
            var actionList = actions
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(a => a.ToUpperInvariant())
                .ToArray();
            query = query.Where(l => actionList.Contains(l.Action.ToUpper()));
        }

        // Filter: entity type
        if (!string.IsNullOrWhiteSpace(entityType))
            query = query.Where(l => l.EntityType == entityType);

        // Filter: date range (inclusive, UTC)
        if (from.HasValue)
            query = query.Where(l => l.Timestamp >= from.Value.ToUniversalTime());
        if (to.HasValue)
            query = query.Where(l => l.Timestamp <= to.Value.ToUniversalTime().AddDays(1).AddSeconds(-1));

        // Filter: specific user
        if (userId.HasValue)
            query = query.Where(l => l.UserId == userId.Value);

        // Filter: free-text search in Details column
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(l => l.Details != null && l.Details.Contains(search));

        // Total count for pagination
        var total = await query.CountAsync();

        // Fetch page
        var items = await query
            .OrderByDescending(l => l.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(l => new
            {
                l.Id,
                l.UserId,
                l.UserName,
                l.Action,
                l.EntityType,
                l.EntityId,
                TimestampUtc = l.Timestamp,
                l.IpAddress,
                l.Details
            })
            .ToListAsync();

        return Ok(new
        {
            total,
            page,
            pageSize,
            totalPages = (int)Math.Ceiling((double)total / pageSize),
            items
        });
    }

    /// <summary>
    /// GET /api/audit/logs/cart-deletions
    /// Convenience shortcut: returns only cart line item deletion events
    /// (CASHIER_DIRECT_DELETE_LINE_ITEM and MANAGER_OVERRIDE_VOID_ITEM).
    /// Same pagination and date range parameters as /api/audit/logs.
    /// </summary>
    [HttpGet("logs/cart-deletions")]
    public async Task<IActionResult> GetCartDeletions(
        [FromQuery] DateTime? from   = null,
        [FromQuery] DateTime? to     = null,
        [FromQuery] Guid? userId     = null,
        [FromQuery] string? search   = null,
        [FromQuery] int page         = 1,
        [FromQuery] int pageSize     = 50)
    {
        return await GetLogs(
            actions:    "CASHIER_DIRECT_DELETE_LINE_ITEM,MANAGER_OVERRIDE_VOID_ITEM",
            entityType: null,
            from:       from,
            to:         to,
            userId:     userId,
            search:     search,
            page:       page,
            pageSize:   pageSize);
    }

    /// <summary>
    /// GET /api/audit/logs/actions
    /// Returns the distinct action names that exist in the audit_logs table.
    /// Used by the frontend to populate the action-type filter dropdown.
    /// </summary>
    [HttpGet("logs/actions")]
    public async Task<IActionResult> GetDistinctActions()
    {
        var actions = await _context.AuditLogs
            .AsNoTracking()
            .Select(l => l.Action)
            .Distinct()
            .OrderBy(a => a)
            .ToListAsync();

        return Ok(actions);
    }
}
