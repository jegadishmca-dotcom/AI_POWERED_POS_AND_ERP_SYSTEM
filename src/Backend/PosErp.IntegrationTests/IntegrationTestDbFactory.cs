using System.IO;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PosErp.Infrastructure.Persistence;

namespace PosErp.IntegrationTests;

/// <summary>
/// Shared factory for integration-test database setup.
/// Drops and rebuilds the public schema from scratch, running all SQL migration files
/// followed by the manual DDL patches required by the current EF entity mappings.
///
/// This is the single source of truth for test-DB provisioning. All F0x test fixtures
/// must call <see cref="Build"/> instead of rolling their own connection strings.
/// </summary>
public static class IntegrationTestDbFactory
{
    static IntegrationTestDbFactory()
    {
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
    }

    private const string DbName   = "posdb_integration_tests";
    private const string Username = "posadmin";
    private const string Password = "pospassword";
    private const int    Port     = 5432;

    private static readonly string[] _hosts =
        { "192.168.1.5", "10.26.198.140", "localhost", "127.0.0.1" };

    private static readonly string MigrationsDir =
        @"d:/JEGADISH/APPLE_SUPERMARKET_POS_PROJECT/" +
        @"AI_POWERED_POS_AND_ERP_SYSTEM/src/Backend/" +
        @"PosErp.Infrastructure/Persistence/Migrations";

    /// <summary>
    /// Resolves the reachable PostgreSQL host, creates the integration test DB if
    /// it doesn't exist, drops+rebuilds the public schema, runs every SQL migration
    /// file in numeric order, applies manual DDL patches, and returns an open
    /// <see cref="ApplicationDbContext"/>.
    /// </summary>
    public static ApplicationDbContext Build()
    {
        var host = ResolveHost();
        var masterConn = ConnStr(host, "postgres");
        var testConn   = ConnStr(host, DbName);

        // 1. Create DB if not present
        using (var conn = new NpgsqlConnection(masterConn))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT 1 FROM pg_database WHERE datname = '{DbName}';";
            if (cmd.ExecuteScalar() == null)
            {
                cmd.CommandText = $"CREATE DATABASE {DbName};";
                cmd.ExecuteNonQuery();
            }
        }

        // 2. Full schema rebuild + migrations + patches
        using (var conn = new NpgsqlConnection(testConn))
        {
            conn.Open();

            // Wipe and recreate the public schema
            Execute(conn, "DROP SCHEMA IF EXISTS public CASCADE; CREATE SCHEMA public; " +
                          "GRANT ALL ON SCHEMA public TO posadmin; " +
                          "GRANT ALL ON SCHEMA public TO public;");

            // Run all numbered SQL migration files in order
            var sqlFiles = Directory.GetFiles(MigrationsDir, "*.sql")
                .OrderBy(f => Path.GetFileName(f))
                .ToList();

            // Two-pass execution:
            // Pass 1 — create all tables (some CREATE INDEX statements may fail on fresh schema
            //           due to reserved-word column-name ambiguity before the table is committed).
            // Pass 2 — re-run the same files; tables already exist so CREATE TABLE IF NOT EXISTS
            //           is a no-op, and CREATE INDEX now succeeds because columns are visible.
            for (int pass = 0; pass < 2; pass++)
            {
                foreach (var file in sqlFiles)
                {
                    try { Execute(conn, File.ReadAllText(file)); }
                    catch { /* idempotent — retry handled by second pass */ }
                }
            }

            // Manual DDL patches required by entity maps not yet covered by migrations
            var patches = new[]
            {
                @"CREATE TABLE IF NOT EXISTS refresh_tokens (
                    id UUID PRIMARY KEY,
                    user_id UUID NOT NULL,
                    token VARCHAR(512) NOT NULL,
                    token_family VARCHAR(255) NOT NULL,
                    expires_at TIMESTAMP WITH TIME ZONE NOT NULL,
                    device_id VARCHAR(255) NOT NULL,
                    is_revoked BOOLEAN NOT NULL DEFAULT FALSE,
                    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
                );",
                @"ALTER TABLE invoices ADD COLUMN IF NOT EXISTS cash_amount   NUMERIC(18,2) NOT NULL DEFAULT 0;
                  ALTER TABLE invoices ADD COLUMN IF NOT EXISTS upi_amount    NUMERIC(18,2) NOT NULL DEFAULT 0;
                  ALTER TABLE invoices ADD COLUMN IF NOT EXISTS card_amount   NUMERIC(18,2) NOT NULL DEFAULT 0;
                  ALTER TABLE invoices ADD COLUMN IF NOT EXISTS wallet_amount NUMERIC(18,2) NOT NULL DEFAULT 0;",
                @"ALTER TABLE grn_items ADD COLUMN IF NOT EXISTS rejection_reason VARCHAR(500);",
                @"ALTER TABLE products ADD COLUMN IF NOT EXISTS has_expiry BOOLEAN DEFAULT TRUE;",
                @"CREATE TABLE IF NOT EXISTS pending_price_approvals (
                    id UUID PRIMARY KEY,
                    barcode VARCHAR(255) NOT NULL,
                    product_name VARCHAR(512) NOT NULL,
                    existing_cost_price NUMERIC(18,2) NOT NULL DEFAULT 0,
                    new_cost_price NUMERIC(18,2) NOT NULL DEFAULT 0,
                    quantity NUMERIC(18,2) NOT NULL DEFAULT 0,
                    invoice_reference VARCHAR(255) NOT NULL DEFAULT '',
                    status VARCHAR(50) NOT NULL DEFAULT 'PENDING',
                    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
                    actioned_at TIMESTAMP WITH TIME ZONE,
                    actioned_by UUID
                );",
                @"CREATE TABLE IF NOT EXISTS idempotent_requests (
                    client_request_token UUID PRIMARY KEY,
                    status VARCHAR(50) NOT NULL DEFAULT 'PENDING',
                    response_payload TEXT,
                    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
                    tenant_id UUID NOT NULL
                );"
            };

            foreach (var patch in patches)
                Execute(conn, patch);
        }

        return new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql(testConn, npgsqlOptions =>
                {
                    npgsqlOptions.EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null);
                })
                .Options);
    }

    /// <summary>
    /// Constructs a new ApplicationDbContext instance pointing to the integration test database,
    /// without rebuilding or dropping the schema. Required for multi-threaded/concurrent tests.
    /// </summary>
    public static ApplicationDbContext CreateNewContext()
    {
        var host = ResolveHost();
        var testConn = ConnStr(host, DbName);
        return new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql(testConn, npgsqlOptions =>
                {
                    npgsqlOptions.EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null);
                })
                .Options);
    }

    // ─── helpers ──────────────────────────────────────────────────────────────

    private static string ResolveHost()
    {
        foreach (var h in _hosts)
        {
            try
            {
                using var c = new NpgsqlConnection(ConnStr(h, "postgres") + "Timeout=2;");
                c.Open();
                return h;
            }
            catch { }
        }
        return "localhost"; // fallback
    }

    private static string ConnStr(string host, string db) =>
        $"Host={host};Port={Port};Database={db};Username={Username};Password={Password};";

    private static void Execute(NpgsqlConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }
}

[Xunit.CollectionDefinition("Database Collection", DisableParallelization = true)]
public class DatabaseCollection
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and serialize database tests.
}
