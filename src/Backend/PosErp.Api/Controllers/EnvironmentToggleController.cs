using BCrypt.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using Npgsql;
using PosErp.Application.Interfaces;
using PosErp.Infrastructure.Persistence;
using PosErp.Infrastructure.Services;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace PosErp.Api.Controllers;

/// <summary>
/// Step 3: Toggle UI &amp; Backend Flow — LIVE / UAT environment switching.
///
/// Security design:
/// - Toggle requires Developer password verification (local BCrypt, v1 design per approved plan).
/// - Lockout state tracked in toggle_lockout_state (atomically updated database table).
/// - 5 consecutive failed attempts trigger a 15-minute lockout.
/// - Successful toggle calls IHostApplicationLifetime.StopApplication() for SelfHosted.
/// - In SaaS mode, connection caches are evicted to reload immediately without container restart.
/// - All toggle attempts are written to audit_logs.
/// </summary>
[ApiController]
[Route("api/environment")]
[Authorize]
public class EnvironmentToggleController : ControllerBase
{
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    private readonly ApplicationDbContext _db;
    private readonly ConnectionStringProvider _connectionStringProvider;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly IConfiguration _configuration;
    private readonly ITenantProvider _tenantProvider;
    private readonly IMemoryCache _memoryCache;

    public EnvironmentToggleController(
        ApplicationDbContext db,
        ConnectionStringProvider connectionStringProvider,
        IHostApplicationLifetime lifetime,
        IConfiguration configuration,
        ITenantProvider tenantProvider,
        IMemoryCache memoryCache)
    {
        _db = db;
        _connectionStringProvider = connectionStringProvider;
        _lifetime = lifetime;
        _configuration = configuration;
        _tenantProvider = tenantProvider;
        _memoryCache = memoryCache;
    }

    // GET /api/environment/mode
    [HttpGet("mode")]
    public async Task<IActionResult> GetCurrentMode()
    {
        var deploymentMode = _configuration["SystemConfig:DeploymentMode"] ?? "SelfHosted";
        var isSaaS = string.Equals(deploymentMode, "SaaS", StringComparison.OrdinalIgnoreCase);
        
        string activeMode = "LIVE";
        string? tenantName = null;

        if (isSaaS)
        {
            var tenantId = _tenantProvider.TenantId;
            if (tenantId == Guid.Empty)
            {
                return BadRequest(new { Error = "Tenant ID is required but missing." });
            }

            // Fetch tenant name from current store (which is tenant-filtered automatically)
            var store = await _db.Stores.FirstOrDefaultAsync();
            tenantName = store?.StoreName;

            // Resolve active mode for this tenant from platform database
            var defaultConnection = _configuration.GetConnectionString("DefaultConnection") 
                ?? "Host=localhost;Database=poserp;Username=postgres;Password=postgres";
            
            try
            {
                await using var conn = new NpgsqlConnection(defaultConnection);
                await conn.OpenAsync();
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT active_mode FROM tenant_environments WHERE tenant_id = @p0";
                var p = cmd.CreateParameter();
                p.ParameterName = "@p0";
                p.Value = tenantId;
                cmd.Parameters.Add(p);
                
                var result = await cmd.ExecuteScalarAsync();
                if (result != null && result != DBNull.Value)
                {
                    activeMode = result.ToString() ?? "LIVE";
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[EnvironmentToggle] Failed to resolve SaaS active mode: {ex.Message}");
            }
        }
        else
        {
            activeMode = _connectionStringProvider.GetSelfHostedActiveMode();
        }

        return Ok(new
        {
            ActiveMode = activeMode,
            DeploymentMode = deploymentMode,
            IsUat = string.Equals(activeMode, "UAT", StringComparison.OrdinalIgnoreCase),
            TenantName = tenantName
        });
    }

    // POST /api/environment/toggle
    // Body: { "developerPassword": "...", "targetMode": "UAT" | "LIVE" }
    [HttpPost("toggle")]
    public async Task<IActionResult> ToggleEnvironment([FromBody] ToggleEnvironmentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.DeveloperPassword))
            return BadRequest(new { Error = "Developer password is required." });

        if (!string.Equals(request.TargetMode, "LIVE", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(request.TargetMode, "UAT", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { Error = "TargetMode must be 'LIVE' or 'UAT'." });

        var deploymentMode = _configuration["SystemConfig:DeploymentMode"] ?? "SelfHosted";
        var isSaaS = string.Equals(deploymentMode, "SaaS", StringComparison.OrdinalIgnoreCase);
        var tenantId = _tenantProvider.TenantId;

        if (isSaaS && tenantId == Guid.Empty)
        {
            return BadRequest(new { Error = "Tenant ID is required but missing." });
        }

        var requestingUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var requestingUserName = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? "unknown";

        // Clean up any expired lockouts before checking status
        await CleanExpiredLockoutsAsync();

        if (string.IsNullOrEmpty(requestingUserId) || !Guid.TryParse(requestingUserId, out var requestingUserGuid))
        {
            return Unauthorized(new { Error = "User is not authenticated." });
        }

        // 1. Check lockout status for the requesting user
        var lockedUntil = await GetActiveRequestingUserLockoutAsync(requestingUserGuid);
        if (lockedUntil.HasValue)
        {
            await WriteAuditLogAsync("EnvironmentToggle", "LOCKOUT_BLOCKED",
                requestingUserId, requestingUserName,
                $"Toggle attempt blocked: lockout active until {lockedUntil.Value:u}. Requested mode: {request.TargetMode}");

            return StatusCode(429, new
            {
                Error = "Too many failed attempts. The toggle is locked for 15 minutes.",
                LockoutUntil = lockedUntil.Value
            });
        }

        // 2. Fetch active Developer users by role join (no hardcoded emails)
        var developerUsers = await (from u in _db.Users
                                    join r in _db.Roles on u.RoleId equals r.Id
                                    where r.Name == "Developer" && u.IsActive && !u.IsDeleted
                                    select u).ToListAsync();

        if (!developerUsers.Any())
        {
            return BadRequest(new { Error = "No active Developer accounts configured in the system." });
        }

        // 3. Verify Developer password against active accounts
        PosErp.Domain.Entities.Auth.User? matchingDeveloper = null;
        foreach (var devUser in developerUsers)
        {
            if (!string.IsNullOrEmpty(devUser.PasswordHash) &&
                BCrypt.Net.BCrypt.Verify(request.DeveloperPassword, devUser.PasswordHash))
            {
                matchingDeveloper = devUser;
                break;
            }
        }

        if (matchingDeveloper == null)
        {
            // Lockout tracking: increment failed count atomically on the requesting user
            var (failedCount, lockoutTime) = await IncrementFailedAttemptAsync(requestingUserGuid);

            await WriteAuditLogAsync("EnvironmentToggle", "APPROVAL_FAILED",
                requestingUserId, requestingUserName,
                $"Developer password verification failed. Attempt {failedCount}/{MaxFailedAttempts}. Requested mode: {request.TargetMode}");

            if (lockoutTime.HasValue)
            {
                await WriteAuditLogAsync("EnvironmentToggle", "LOCKOUT_ACTIVATED",
                    requestingUserId, requestingUserName,
                    $"Lockout activated after {MaxFailedAttempts} failed attempts. Duration: 15 minutes.");

                return StatusCode(429, new
                {
                    Error = "Maximum attempts exceeded. Toggle locked for 15 minutes.",
                    LockoutUntil = lockoutTime.Value
                });
            }

            var remaining = MaxFailedAttempts - failedCount;
            return Unauthorized(new { Error = "Developer password incorrect.", AttemptsRemaining = remaining });
        }

        // 4. Approval passed — reset failed count for the requesting user
        await ResetLockoutStateAsync(requestingUserGuid);

        string currentMode;
        if (isSaaS)
        {
            currentMode = "LIVE"; // fallback baseline
            var defaultConnection = _configuration.GetConnectionString("DefaultConnection") 
                ?? "Host=localhost;Database=poserp;Username=postgres;Password=postgres";
            
            try
            {
                await using var conn = new NpgsqlConnection(defaultConnection);
                await conn.OpenAsync();
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT active_mode FROM tenant_environments WHERE tenant_id = @p0";
                var p = cmd.CreateParameter();
                p.ParameterName = "@p0";
                p.Value = tenantId;
                cmd.Parameters.Add(p);
                var result = await cmd.ExecuteScalarAsync();
                if (result != null && result != DBNull.Value) currentMode = result.ToString() ?? "LIVE";
            }
            catch { }
        }
        else
        {
            currentMode = _connectionStringProvider.GetSelfHostedActiveMode();
        }

        if (string.Equals(currentMode, request.TargetMode, StringComparison.OrdinalIgnoreCase))
        {
            return Ok(new { Message = $"Already in {request.TargetMode} mode. No change needed." });
        }

        // 5. Persist target mode scoped to environment type
        if (isSaaS)
        {
            // SaaS: Update tenant_environments platform table and increment token_version
            var defaultConnection = _configuration.GetConnectionString("DefaultConnection") 
                ?? "Host=localhost;Database=poserp;Username=postgres;Password=postgres";
                
            await using (var conn = new NpgsqlConnection(defaultConnection))
            {
                await conn.OpenAsync();
                await using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "UPDATE tenant_environments SET active_mode = @mode, token_version = token_version + 1, updated_at = NOW() WHERE tenant_id = @tenantId";
                    cmd.Parameters.AddWithValue("@mode", request.TargetMode.ToUpper());
                    cmd.Parameters.AddWithValue("@tenantId", tenantId);
                    await cmd.ExecuteNonQueryAsync();
                }
            }

            // Evict from memory connection cache and token version cache
            _memoryCache.Remove($"conn_{tenantId}");
            _memoryCache.Remove($"token_ver_{tenantId}");

            // Forcibly invalidate all active sessions/refresh tokens for this tenant.
            // Since this database context (ApplicationDbContext) is connected directly to this
            // tenant's database connection, executing raw SQL will clear tokens for this tenant only.
            await _db.Database.ExecuteSqlRawAsync("DELETE FROM refresh_tokens;");
        }
        else
        {
            // Self-hosted: Write to operation_mode.json and increment token_version
            var nextVersion = _connectionStringProvider.GetSelfHostedTokenVersion() + 1;
            _connectionStringProvider.SaveSelfHostedActiveMode(request.TargetMode.ToUpper(), nextVersion);
            _memoryCache.Remove("token_ver_selfhosted");
        }

        // 6. Write final audit log to database and await durability before responding to client
        var auditWriteTask = WriteAuditLogAsync("EnvironmentToggle", "MODE_CHANGED",
            requestingUserId, requestingUserName,
            $"Environment mode switched from {currentMode} to {request.TargetMode.ToUpper()} by approved Developer.");
        
        await auditWriteTask;

        // 7. Explicit shutdown sequence for Self-hosted mode using HTTP OnCompleted callback
        if (!isSaaS)
        {
            HttpContext.Response.OnCompleted(async () =>
            {
                // Delay 500ms *after response completes* to ensure it flushes to TCP stack safely
                await Task.Delay(500);
                _lifetime.StopApplication();
            });

            return Ok(new
            {
                Message = $"Environment switching to {request.TargetMode.ToUpper()}. Container is restarting.",
                PreviousMode = currentMode,
                NewMode = request.TargetMode.ToUpper(),
                Note = "The API will be unavailable briefly while the container restarts."
            });
        }

        // SaaS switches modes instantly without restarting the server container
        return Ok(new
        {
            Message = $"Environment switched to {request.TargetMode.ToUpper()} successfully.",
            PreviousMode = currentMode,
            NewMode = request.TargetMode.ToUpper(),
            Note = "Mode is active immediately."
        });
    }

    // ─── Private helpers ──────────────────────────────────────────────────────

    private async Task WriteAuditLogAsync(string entityType, string action, string? userId, string userName, string details)
    {
        try
        {
            await using var conn = new NpgsqlConnection(_db.Database.GetConnectionString());
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO audit_logs (id, user_id, user_name, action, entity_type, details, timestamp, ip_address, tenant_id)
                VALUES (@id, @uid, @uname, @action, @etype, @details, NOW(), @ip, @tenantId);";

            Guid? parsedUserId = Guid.TryParse(userId, out var g) ? g : null;
            cmd.Parameters.AddWithValue("@id", Guid.NewGuid());
            cmd.Parameters.AddWithValue("@uid", (object?)parsedUserId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@uname", userName);
            cmd.Parameters.AddWithValue("@action", action);
            cmd.Parameters.AddWithValue("@etype", entityType);
            cmd.Parameters.AddWithValue("@details", details);
            cmd.Parameters.AddWithValue("@ip", HttpContext?.Connection?.RemoteIpAddress?.ToString() ?? "unknown");
            cmd.Parameters.AddWithValue("@tenantId", _tenantProvider.TenantId);
            await cmd.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[EnvironmentToggle] Audit log write failed: {ex.Message}");
        }
    }

    private async Task<DateTimeOffset?> GetActiveRequestingUserLockoutAsync(Guid accountId)
    {
        try
        {
            await using var conn = new NpgsqlConnection(_db.Database.GetConnectionString());
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT locked_until
                FROM toggle_lockout_state
                WHERE account_id = @accountId AND locked_until > NOW();";
            cmd.Parameters.AddWithValue("@accountId", accountId);

            var result = await cmd.ExecuteScalarAsync();
            if (result == null || result == DBNull.Value) return null;
            return (DateTime)result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[EnvironmentToggle] Lockout query failed: {ex.Message}");
            return null;
        }
    }

    private async Task<(int FailedCount, DateTimeOffset? LockedUntil)> IncrementFailedAttemptAsync(Guid accountId)
    {
        try
        {
            await using var conn = new NpgsqlConnection(_db.Database.GetConnectionString());
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO toggle_lockout_state (account_id, failed_count, locked_until, updated_at)
                VALUES (@accountId, 1, NULL, NOW())
                ON CONFLICT (account_id) DO UPDATE
                SET failed_count = toggle_lockout_state.failed_count + 1,
                    locked_until = CASE WHEN toggle_lockout_state.failed_count + 1 >= @maxFailed THEN NOW() + interval '15 minutes' ELSE NULL END,
                    updated_at = NOW()
                RETURNING failed_count, locked_until;";

            cmd.Parameters.AddWithValue("@accountId", accountId);
            cmd.Parameters.AddWithValue("@maxFailed", MaxFailedAttempts);

            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var failedCount = reader.GetInt32(0);
                var lockedUntil = reader.IsDBNull(1) ? (DateTimeOffset?)null : reader.GetFieldValue<DateTime>(1);
                return (failedCount, lockedUntil);
            }
            return (0, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[EnvironmentToggle] Increment failed attempt failed for {accountId}: {ex.Message}");
            return (0, null);
        }
    }

    private async Task ResetLockoutStateAsync(Guid accountId)
    {
        try
        {
            await using var conn = new NpgsqlConnection(_db.Database.GetConnectionString());
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO toggle_lockout_state (account_id, failed_count, locked_until, updated_at)
                VALUES (@accountId, 0, NULL, NOW())
                ON CONFLICT (account_id) DO UPDATE
                SET failed_count = 0,
                    locked_until = NULL,
                    updated_at = NOW();";

            cmd.Parameters.AddWithValue("@accountId", accountId);
            await cmd.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[EnvironmentToggle] Reset lockout failed for {accountId}: {ex.Message}");
        }
    }

    private async Task CleanExpiredLockoutsAsync()
    {
        try
        {
            await using var conn = new NpgsqlConnection(_db.Database.GetConnectionString());
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                UPDATE toggle_lockout_state
                SET failed_count = 0, locked_until = NULL, updated_at = NOW()
                WHERE locked_until IS NOT NULL AND locked_until <= NOW();";
            await cmd.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[EnvironmentToggle] Clean expired lockouts failed: {ex.Message}");
        }
    }
}

public class ToggleEnvironmentRequest
{
    public string DeveloperPassword { get; set; } = string.Empty;
    public string TargetMode { get; set; } = string.Empty;
}
