using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Npgsql;
using PosErp.Infrastructure.Services;
using Xunit;

namespace PosErp.IntegrationTests;

[Collection("Database Collection")]
public class DatabaseProvisioningTests
{
    static DatabaseProvisioningTests()
    {
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
    }

    private string GetActiveHost()
    {
        var hosts = new[] { "192.168.1.5", "10.26.198.140", "localhost", "127.0.0.1" };
        foreach (var host in hosts)
        {
            try
            {
                var testConn = $"Host={host};Port=5432;Database=postgres;Username=posadmin;Password=pospassword;Timeout=2;";
                using var conn = new NpgsqlConnection(testConn);
                conn.Open();
                return host;
            }
            catch
            {
                // try next
            }
        }
        return "localhost";
    }

    [Fact]
    public async Task ProvisionEnvironmentPair_ShouldCreateAndSanitizeDatabasesCorrectly()
    {
        var host = GetActiveHost();
        var baseConnStr = $"Host={host};Port=5432;Database=posdb_integration_tests;Username=posadmin;Password=pospassword;";
        var tenantId = Guid.NewGuid();
        var tenantName = "IntegrationTestTenant";

        var provisioningService = new DatabaseProvisioningService();

        // Target database names derived by service:
        var expectedLiveDb = "tenant_integrationtesttenant_live";
        var expectedUatDb = "tenant_integrationtesttenant_uat";

        try
        {
            // Act: Provision UAT/LIVE pair
            await provisioningService.ProvisionEnvironmentPairAsync(baseConnStr, tenantName, tenantId);

            // Assert: Verify LIVE database metadata
            var liveConnStr = $"Host={host};Port=5432;Database={expectedLiveDb};Username=posadmin;Password=pospassword;";
            using (var conn = new NpgsqlConnection(liveConnStr))
            {
                await conn.OpenAsync();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT database_name, environment_mode, tenant_id FROM database_metadata LIMIT 1";
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        Assert.True(await reader.ReadAsync());
                        Assert.Equal(expectedLiveDb, reader.GetString(0));
                        Assert.Equal("LIVE", reader.GetString(1));
                        Assert.Equal(tenantId, reader.GetGuid(2));
                    }
                }
            }

            // Assert: Verify UAT database metadata and sanitization (truncate audit)
            var uatConnStr = $"Host={host};Port=5432;Database={expectedUatDb};Username=posadmin;Password=pospassword;";
            using (var conn = new NpgsqlConnection(uatConnStr))
            {
                await conn.OpenAsync();
                
                // Assert UAT Metadata
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT database_name, environment_mode, tenant_id FROM database_metadata LIMIT 1";
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        Assert.True(await reader.ReadAsync());
                        Assert.Equal(expectedUatDb, reader.GetString(0));
                        Assert.Equal("UAT", reader.GetString(1));
                        Assert.Equal(tenantId, reader.GetGuid(2));
                    }
                }

                // Assert Customer Balances were reset to 0 (only if columns exist in this schema)
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"SELECT COUNT(*) FROM information_schema.columns
                        WHERE table_schema='public' AND table_name='customers' AND column_name='wallet_balance'";
                    var walletColExists = (long)(await cmd.ExecuteScalarAsync() ?? 0L) > 0;
                    if (walletColExists)
                    {
                        cmd.CommandText = "SELECT COUNT(*) FROM customers WHERE wallet_balance <> 0 OR loyalty_points <> 0";
                        var nonZeroBalancesCount = (long)(await cmd.ExecuteScalarAsync() ?? 0L);
                        Assert.Equal(0L, nonZeroBalancesCount);
                    }
                }

                // Assert Document sequences were reset to 0 (only if table exists)
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"SELECT COUNT(*) FROM information_schema.tables
                        WHERE table_schema='public' AND table_name='document_sequences'";
                    var seqTableExists = (long)(await cmd.ExecuteScalarAsync() ?? 0L) > 0;
                    if (seqTableExists)
                    {
                        cmd.CommandText = "SELECT COUNT(*) FROM document_sequences WHERE current_number <> 0";
                        var nonZeroSequencesCount = (long)(await cmd.ExecuteScalarAsync() ?? 0L);
                        Assert.Equal(0L, nonZeroSequencesCount);
                    }
                }

                // Assert Transactional tables like invoices are empty (only if table exists)
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"SELECT COUNT(*) FROM information_schema.tables
                        WHERE table_schema='public' AND table_name='invoices'";
                    var invoicesTableExists = (long)(await cmd.ExecuteScalarAsync() ?? 0L) > 0;
                    if (invoicesTableExists)
                    {
                        cmd.CommandText = "SELECT COUNT(*) FROM invoices";
                        var invoicesCount = (long)(await cmd.ExecuteScalarAsync() ?? 0L);
                        Assert.Equal(0L, invoicesCount);
                    }
                }
            }
        }
        finally
        {
            // Clean up: Drop provisioned databases (each DDL must be a separate command — PostgreSQL
            // forbids DROP DATABASE inside a pipeline/multi-statement batch)
            var adminConnStr = $"Host={host};Port=5432;Database=postgres;Username=posadmin;Password=pospassword;";
            using (var conn = new NpgsqlConnection(adminConnStr))
            {
                await conn.OpenAsync();

                // 1. Terminate active connections to both target databases
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = $"SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname IN ('{expectedLiveDb}', '{expectedUatDb}');";
                    await cmd.ExecuteNonQueryAsync();
                }

                // 2. Drop LIVE database
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = $"DROP DATABASE IF EXISTS {expectedLiveDb};";
                    await cmd.ExecuteNonQueryAsync();
                }

                // 3. Drop UAT database
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = $"DROP DATABASE IF EXISTS {expectedUatDb};";
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }
    }
}
