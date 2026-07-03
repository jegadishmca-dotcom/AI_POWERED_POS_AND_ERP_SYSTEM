using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;
using Microsoft.Extensions.Configuration;
using PosErp.Application.Interfaces;
using PosErp.Infrastructure.Services;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Infrastructure.Persistence;

public class DbSafetyInterceptor : DbConnectionInterceptor
{
    private readonly ITenantProvider _tenantProvider;
    private readonly IConfiguration _configuration;
    private readonly IConnectionStringProvider _connectionStringProvider;

    public DbSafetyInterceptor(
        ITenantProvider tenantProvider, 
        IConfiguration configuration,
        IConnectionStringProvider connectionStringProvider)
    {
        _tenantProvider = tenantProvider;
        _configuration = configuration;
        _connectionStringProvider = connectionStringProvider;
    }

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        VerifyDatabaseSafety(connection);
        base.ConnectionOpened(connection, eventData);
    }

    public override Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        VerifyDatabaseSafety(connection);
        return base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
    }

    private void VerifyDatabaseSafety(DbConnection connection)
    {
        var deploymentMode = _configuration["SystemConfig:DeploymentMode"] ?? "SelfHosted";
        
        // Skip check during system startup migrations/seeding when there is no active tenant context in SaaS mode
        if (string.Equals(deploymentMode, "SaaS", StringComparison.OrdinalIgnoreCase) && _tenantProvider.TenantId == Guid.Empty)
        {
            return;
        }

        string expectedMode;
        if (string.Equals(deploymentMode, "SaaS", StringComparison.OrdinalIgnoreCase))
        {
            // For SaaS, active mode check can be inferred from current database metadata or from connection string provider resolution
            // Since ConnectionStringProvider already resolves the correct connection string based on Platform DB settings,
            // we check if the connection.Database name matches the derived active mode suffix.
            // Let's resolve the expected mode from the platform config
            expectedMode = "LIVE"; // Default to LIVE
            try
            {
                var platformConn = _configuration.GetConnectionString("DefaultConnection");
                using (var platformDbConn = new Npgsql.NpgsqlConnection(platformConn))
                {
                    platformDbConn.Open();
                    using (var cmd = platformDbConn.CreateCommand())
                    {
                        cmd.CommandText = "SELECT active_mode FROM tenant_environments WHERE tenant_id = @p0";
                        var p = cmd.CreateParameter();
                        p.ParameterName = "@p0";
                        p.Value = _tenantProvider.TenantId;
                        cmd.Parameters.Add(p);
                        var res = cmd.ExecuteScalar();
                        if (res != null)
                        {
                            expectedMode = res.ToString() ?? "LIVE";
                        }
                    }
                }
            }
            catch
            {
                // Fallback to default
            }
        }
        else
        {
            // Self-hosted: read active mode from operation_mode.json
            var connStrProvider = _connectionStringProvider as ConnectionStringProvider;
            expectedMode = connStrProvider?.GetSelfHostedActiveMode() ?? "LIVE";
        }

        var dbName = connection.Database;
        
        // If UAT, the connected database MUST end with _uat
        if (string.Equals(expectedMode, "UAT", StringComparison.OrdinalIgnoreCase))
        {
            if (!dbName.EndsWith("_uat", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"[FATAL SAFETY ASSERTION FAILED] Connection attempted to non-UAT database '{dbName}' while system is in 'UAT' operation mode. " +
                    $"Aborted to protect live production data.");
            }
        }
        // If LIVE, the connected database MUST end with _live (or match posdb/poserp for local development defaults)
        else
        {
            var isLiveDb = dbName.EndsWith("_live", StringComparison.OrdinalIgnoreCase);
            var isDevDbFallback = dbName.Equals("posdb", StringComparison.OrdinalIgnoreCase) || 
                                  dbName.Equals("poserp", StringComparison.OrdinalIgnoreCase);
            var isDevelopment = string.Equals(
                Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development", 
                "Development", 
                StringComparison.OrdinalIgnoreCase);

            if (!isLiveDb && (!isDevelopment || !isDevDbFallback))
            {
                throw new InvalidOperationException(
                    $"[FATAL SAFETY ASSERTION FAILED] Connection attempted to non-LIVE database '{dbName}' while system is in 'LIVE' operation mode (must end with '_live' in Production). " +
                    $"Aborted to prevent environment mismatch.");
            }
        }
    }
}
