using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Caching.Memory;
using PosErp.Application.Interfaces;
using System;
using System.IO;
using System.Text.Json;
using Npgsql;

namespace PosErp.Infrastructure.Services;

public class ConnectionStringProvider : IConnectionStringProvider
{
    private readonly IConfiguration _configuration;
    private readonly ITenantProvider _tenantProvider;
    private readonly IMemoryCache _memoryCache;
    private static readonly object FileLock = new object();

    public ConnectionStringProvider(
        IConfiguration configuration, 
        ITenantProvider tenantProvider,
        IMemoryCache memoryCache)
    {
        _configuration = configuration;
        _tenantProvider = tenantProvider;
        _memoryCache = memoryCache;
    }

    public string GetConnectionString()
    {
        var deploymentMode = _configuration["SystemConfig:DeploymentMode"] ?? "SelfHosted";
        var defaultConnection = _configuration.GetConnectionString("DefaultConnection") 
            ?? "Host=localhost;Database=poserp;Username=postgres;Password=postgres";

        if (string.Equals(deploymentMode, "SaaS", StringComparison.OrdinalIgnoreCase))
        {
            var tenantId = _tenantProvider.TenantId;
            if (tenantId == Guid.Empty)
            {
                // Fallback to default platform connection (for system startup, migrations, seed jobs)
                return defaultConnection;
            }

            // In SaaS, resolve connection string from memory cache or Platform Database
            var cacheKey = $"conn_{tenantId}";
            var connStr = _memoryCache.Get<string>(cacheKey);
            if (connStr == null)
            {
                connStr = ResolveSaaSConnectionString(tenantId, defaultConnection);
                _memoryCache.Set(cacheKey, connStr, TimeSpan.FromMinutes(2));
            }
            return connStr;
        }
        else
        {
            // Self-hosted: resolve based on local config file 'operation_mode.json'
            return ResolveSelfHostedConnectionString(defaultConnection);
        }
    }

    private string ResolveSaaSConnectionString(Guid tenantId, string defaultConnection)
    {
        try
        {
            // Query the platform database to find active mode and connection strings
            using (var conn = new NpgsqlConnection(defaultConnection))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT active_mode, live_connection_string, uat_connection_string FROM tenant_environments WHERE tenant_id = @p0";
                    var p = cmd.CreateParameter();
                    p.ParameterName = "@p0";
                    p.Value = tenantId;
                    cmd.Parameters.Add(p);

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            var mode = reader.GetString(0);
                            var liveConn = reader.GetString(1);
                            var uatConn = reader.GetString(2);

                            return string.Equals(mode, "UAT", StringComparison.OrdinalIgnoreCase) ? uatConn : liveConn;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ConnectionStringProvider] SaaS connection resolution error for tenant {tenantId}: {ex.Message}");
        }

        // If not found or error, fallback to deriving from default connection using the tenant ID
        var activeMode = GetSelfHostedActiveMode(); // check if active mode file overrides
        return DeriveConnectionStringForTenant(defaultConnection, tenantId, activeMode);
    }

    private string ResolveSelfHostedConnectionString(string defaultConnection)
    {
        var activeMode = GetSelfHostedActiveMode();
        
        // Check if explicit connections are configured
        var targetConn = string.Equals(activeMode, "UAT", StringComparison.OrdinalIgnoreCase)
            ? _configuration.GetConnectionString("UatConnection")
            : _configuration.GetConnectionString("LiveConnection");

        if (!string.IsNullOrEmpty(targetConn))
        {
            return targetConn;
        }

        // Otherwise, derive by modifying database name
        return DeriveConnectionString(defaultConnection, activeMode);
    }

    private string GetConfigFilePath()
    {
        var configDir = Environment.GetEnvironmentVariable("SYSTEM_CONFIG_DIR") ?? AppDomain.CurrentDomain.BaseDirectory;
        if (!string.Equals(configDir, AppDomain.CurrentDomain.BaseDirectory, StringComparison.OrdinalIgnoreCase))
        {
            Directory.CreateDirectory(configDir);
        }
        return Path.Combine(configDir, "operation_mode.json");
    }

    public string GetSelfHostedActiveMode()
    {
        var filePath = GetConfigFilePath();
        lock (FileLock)
        {
            if (File.Exists(filePath))
            {
                try
                {
                    var content = File.ReadAllText(filePath);
                    using (var doc = JsonDocument.Parse(content))
                    {
                        if (doc.RootElement.TryGetProperty("ActiveMode", out var prop))
                        {
                            return prop.GetString() ?? "LIVE";
                        }
                    }
                }
                catch
                {
                    // Fallback on error
                }
            }
        }
        return "LIVE";
    }

    public int GetSelfHostedTokenVersion()
    {
        var filePath = GetConfigFilePath();
        lock (FileLock)
        {
            if (File.Exists(filePath))
            {
                try
                {
                    var content = File.ReadAllText(filePath);
                    using (var doc = JsonDocument.Parse(content))
                    {
                        if (doc.RootElement.TryGetProperty("TokenVersion", out var prop))
                        {
                            return prop.GetInt32();
                        }
                    }
                }
                catch
                {
                    // Fallback on error
                }
            }
        }
        return 1;
    }

    public void SaveSelfHostedActiveMode(string mode, int? tokenVersion = null)
    {
        var filePath = GetConfigFilePath();
        lock (FileLock)
        {
            try
            {
                int version = tokenVersion ?? GetSelfHostedTokenVersion();
                var data = new { ActiveMode = mode, TokenVersion = version };
                var content = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(filePath, content);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ConnectionStringProvider] Failed to save operation mode: {ex.Message}");
            }
        }
    }

    private string DeriveConnectionString(string baseConnStr, string mode)
    {
        var builder = new NpgsqlConnectionStringBuilder(baseConnStr);
        var dbName = builder.Database ?? "posdb";
        
        // Remove existing suffixes if any
        if (dbName.EndsWith("_live", StringComparison.OrdinalIgnoreCase))
        {
            dbName = dbName.Substring(0, dbName.Length - 5);
        }
        else if (dbName.EndsWith("_uat", StringComparison.OrdinalIgnoreCase))
        {
            dbName = dbName.Substring(0, dbName.Length - 4);
        }

        builder.Database = string.Equals(mode, "UAT", StringComparison.OrdinalIgnoreCase)
            ? $"{dbName}_uat"
            : $"{dbName}_live";

        return builder.ToString();
    }

    private string DeriveConnectionStringForTenant(string baseConnStr, Guid tenantId, string mode)
    {
        var builder = new NpgsqlConnectionStringBuilder(baseConnStr);
        var tenantSuffix = tenantId.ToString().Substring(0, 8); // use first 8 chars of Guid as database name safe prefix
        
        builder.Database = string.Equals(mode, "UAT", StringComparison.OrdinalIgnoreCase)
            ? $"tenant_{tenantSuffix}_uat"
            : $"tenant_{tenantSuffix}_live";

        return builder.ToString();
    }
}
