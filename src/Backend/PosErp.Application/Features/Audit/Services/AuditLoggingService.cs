using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Audit.Services;

public interface IAuditLoggingService
{
    Task LogActionAsync(Guid? userId, string action, string entityName, string entityId, object oldValues, object newValues, string ipAddress, CancellationToken cancellationToken);
}

public class AuditLoggingService : IAuditLoggingService
{
    private readonly IApplicationDbContext _context;

    public AuditLoggingService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task LogActionAsync(Guid? userId, string action, string entityName, string entityId, object oldValues, object newValues, string ipAddress, CancellationToken cancellationToken)
    {
        var oldJson = JsonSerializer.Serialize(oldValues);
        var newJson = JsonSerializer.Serialize(newValues);

        string sql = @"
            INSERT INTO audit_logs (user_id, action, entity_name, entity_id, old_values, new_values, ip_address)
            VALUES ({0}, {1}, {2}, {3}, CAST({4} AS JSONB), CAST({5} AS JSONB), {6})";

        await ((DbContext)_context).Database.ExecuteSqlRawAsync(sql, 
            userId, action, entityName, entityId, oldJson, newJson, ipAddress);
    }
}
