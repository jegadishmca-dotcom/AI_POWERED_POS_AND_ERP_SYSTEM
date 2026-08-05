using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;

namespace PosErp.Application.Features.Audit.Services;

public interface IAuditLoggingService
{
    Task LogActionAsync(Guid? userId, string action, string entityName, string entityId, object oldValues, object newValues, string ipAddress, CancellationToken cancellationToken);
}

public class AuditLoggingService : IAuditLoggingService
{
    private readonly IApplicationDbContext _context;
    private readonly ITenantProvider _tenantProvider;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuditLoggingService(
        IApplicationDbContext context, 
        ITenantProvider tenantProvider,
        IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _tenantProvider = tenantProvider;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task LogActionAsync(Guid? userId, string action, string entityName, string entityId, object oldValues, object newValues, string ipAddress, CancellationToken cancellationToken)
    {
        var oldJson = JsonSerializer.Serialize(oldValues);
        var newJson = JsonSerializer.Serialize(newValues);

        var httpContext = _httpContextAccessor.HttpContext;
        var userAgent = httpContext?.Request?.Headers["User-Agent"].ToString();

        // 1. Resolve actual user name dynamically from DB or HttpContext claims
        string resolvedUserName = "System";

        if (userId.HasValue && userId.Value != Guid.Empty)
        {
            var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId.Value, cancellationToken);
            if (user != null)
            {
                resolvedUserName = !string.IsNullOrWhiteSpace(user.FullName)
                    ? $"{user.FullName} ({user.Username})"
                    : user.Username;
            }
        }

        if (resolvedUserName == "System" && httpContext?.User?.Identity?.IsAuthenticated == true)
        {
            var claimName = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value
                         ?? httpContext.User.FindFirst("name")?.Value
                         ?? httpContext.User.FindFirst("unique_name")?.Value;
            if (!string.IsNullOrWhiteSpace(claimName))
            {
                resolvedUserName = claimName;
            }
        }

        // 2. Resolve real client IP address (stripping ::ffff: and proxy gateways)
        string resolvedIp = ResolveClientIpAddress(ipAddress);

        var log = new PosErp.Domain.Entities.Audit.AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            UserName = resolvedUserName,
            Action = action,
            EntityType = entityName,
            EntityId = entityId,
            Timestamp = DateTime.UtcNow,
            IpAddress = resolvedIp,
            UserAgent = userAgent,
            TenantId = _tenantProvider.TenantId,
            Details = $"Old: {oldJson}, New: {newJson}"
        };

        _context.AuditLogs.Add(log);
        await ((DbContext)_context).SaveChangesAsync(cancellationToken);
    }

    private string ResolveClientIpAddress(string? passedIp)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext != null)
        {
            var forwardedFor = httpContext.Request.Headers["X-Forwarded-For"].ToString();
            if (!string.IsNullOrWhiteSpace(forwardedFor))
            {
                var firstIp = forwardedFor.Split(',')[0].Trim().Replace("::ffff:", "");
                if (!string.IsNullOrWhiteSpace(firstIp) && firstIp != "127.0.0.1" && firstIp != "::1")
                {
                    return firstIp;
                }
            }
            var realIp = httpContext.Request.Headers["X-Real-IP"].ToString().Replace("::ffff:", "");
            if (!string.IsNullOrWhiteSpace(realIp) && realIp != "127.0.0.1" && realIp != "::1")
            {
                return realIp;
            }
            if (httpContext.Connection.RemoteIpAddress != null)
            {
                var connIp = httpContext.Connection.RemoteIpAddress.ToString().Replace("::ffff:", "");
                if (connIp != "127.0.0.1" && connIp != "::1" && !connIp.StartsWith("172."))
                {
                    return connIp;
                }
            }
        }
        if (!string.IsNullOrWhiteSpace(passedIp) && passedIp != "unknown")
        {
            var cleanPassed = passedIp.Replace("::ffff:", "");
            if (cleanPassed != "127.0.0.1" && cleanPassed != "::1" && !cleanPassed.StartsWith("172."))
            {
                return cleanPassed;
            }
        }
        return "192.168.1.4"; // Local LAN Dev IP fallback
    }
}
