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

        var userAgent = _httpContextAccessor.HttpContext?.Request?.Headers["User-Agent"].ToString();

        var log = new PosErp.Domain.Entities.Audit.AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            UserName = "System", // Or passed in
            Action = action,
            EntityType = entityName,
            EntityId = entityId,
            Timestamp = DateTime.UtcNow,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            TenantId = _tenantProvider.TenantId,
            Details = $"Old: {oldJson}, New: {newJson}"
        };

        _context.AuditLogs.Add(log);
        await ((DbContext)_context).SaveChangesAsync(cancellationToken);
    }
}
