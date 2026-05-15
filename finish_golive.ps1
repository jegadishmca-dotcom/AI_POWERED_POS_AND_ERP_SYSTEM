$backendDir = "d:\JEGADISH\AI_POWERED_POS_AND_ERP_SYSTEM\src\Backend"
$infraDir = "d:\JEGADISH\AI_POWERED_POS_AND_ERP_SYSTEM\infrastructure"

# 1. Rate Limiting Middleware
@"
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System;
using System.Net;
using System.Threading.Tasks;

namespace PosErp.Api.Middlewares;

public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimitingMiddleware> _logger;
    private readonly IConnectionMultiplexer _redis;
    private const int MaxRequestsPerMinute = 100;

    public RateLimitingMiddleware(RequestDelegate next, ILogger<RateLimitingMiddleware> logger, IConnectionMultiplexer redis)
    {
        _next = next;
        _logger = logger;
        _redis = redis;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var endpoint = context.Request.Path.Value;

        // Only rate limit API calls
        if (endpoint != null && endpoint.StartsWith("/api"))
        {
            var db = _redis.GetDatabase();
            var key = $"rate_limit:{ipAddress}";

            var count = await db.StringIncrementAsync(key);
            if (count == 1)
            {
                await db.KeyExpireAsync(key, TimeSpan.FromMinutes(1));
            }

            if (count > MaxRequestsPerMinute)
            {
                _logger.LogWarning($"Rate limit exceeded for IP: {ipAddress}");
                context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync("{\"error\": \"Too many requests. Please try again later.\"}");
                return;
            }
        }

        await _next(context);
    }
}
"@ | Out-File -FilePath "$backendDir\PosErp.Api\Middlewares\RateLimitingMiddleware.cs" -Encoding utf8

# 2. Audit Logging Service
@"
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

public class AuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? UserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string OldValues { get; set; } = string.Empty; // JSONB
    public string NewValues { get; set; } = string.Empty; // JSONB
    public string IpAddress { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
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
        // In a real implementation, we map this to the `audit_logs` table via EF Core.
        // We simulate raw SQL execution here for the sake of the scaffold.
        var oldJson = JsonSerializer.Serialize(oldValues);
        var newJson = JsonSerializer.Serialize(newValues);

        string sql = @"
            INSERT INTO audit_logs (user_id, action, entity_name, entity_id, old_values, new_values, ip_address)
            VALUES ({0}, {1}, {2}, {3}, CAST({4} AS JSONB), CAST({5} AS JSONB), {6})";

        await ((DbContext)_context).Database.ExecuteSqlRawAsync(sql, 
            userId, action, entityName, entityId, oldJson, newJson, ipAddress);
    }
}
"@ | Out-File -FilePath "$backendDir\PosErp.Application\Features\Audit\Services\AuditLoggingService.cs" -Encoding utf8

# 3. Update Program.cs
$programContent = @"
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PosErp.Api.Middlewares;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Health Checks
builder.Services.AddHealthChecks();

// Redis Configuration
string redisConnectionString = builder.Configuration.GetSection("Redis:ConnectionString").Value ?? "localhost:6379";
builder.Services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(redisConnectionString));

var app = builder.WebApplication.Create();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseMiddleware<RateLimitingMiddleware>();

app.UseHttpsRedirection();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
"@
$programContent | Out-File -FilePath "$backendDir\PosErp.Api\Program.cs" -Encoding utf8

# 4. Backup Script
@"
#!/bin/bash
# Backup Script for PostgreSQL Database

BACKUP_DIR="/var/backups/postgres"
DB_USER=\${DB_USER:-"postgres"}
DB_NAME=\${DB_NAME:-"PosErpDb"}
DATE=\$(date +%Y%m%d_%H%M%S)
BACKUP_FILE="\$BACKUP_DIR/\$DB_NAME-\$DATE.sql.gz"

mkdir -p \$BACKUP_DIR

echo "Starting backup of \$DB_NAME..."
docker exec -t erp-db-1 pg_dump -U \$DB_USER \$DB_NAME | gzip > \$BACKUP_FILE

if [ $? -eq 0 ]; then
  echo "Backup successful: \$BACKUP_FILE"
  # Optional: Upload to S3
  # aws s3 cp \$BACKUP_FILE s3://my-erp-backups/
else
  echo "Backup failed!"
  exit 1
fi

# Clean up backups older than 30 days
find \$BACKUP_DIR -type f -name "*.sql.gz" -mtime +30 -exec rm {} \;
echo "Old backups cleaned up."
"@ | Out-File -FilePath "$infraDir\backup_script.sh" -Encoding utf8

Write-Host "Final Polish Completed"
