using Npgsql;
using PosErp.Application.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace PosErp.Infrastructure.Services;

public class DatabaseProvisioningService : IDatabaseProvisioningService
{
    private static readonly List<string> WipeTables = new()
    {
        "invoices",
        "invoice_items",
        "einvoice_metadata",
        "ewaybill_metadata",
        "journal_entries",
        "journal_entry_lines",
        "tax_transactions",
        "daily_finance_summary",
        "stock_ledger",
        "product_batches",
        "stock_adjustments",
        "stock_adjustment_items",
        "stock_take_headers",
        "stock_take_items",
        "inter_store_transfers",
        "inter_store_transfer_items",
        "grn_headers",
        "grn_items",
        "purchase_bill_headers",
        "purchase_bill_items",
        "purchase_order_headers",
        "purchase_order_items",
        "purchase_returns",
        "purchase_return_items",
        "sales_returns",
        "sales_return_items",
        "customer_receipts",
        "customer_receipt_allocations",
        "customer_ledger",
        "supplier_payments",
        "supplier_payment_allocations",
        "supplier_ledger",
        "wallet_ledger",
        "loyalty_ledger",
        "offer_usage_logs",
        "approval_requests",
        "approval_request_steps",
        "pending_price_approvals",
        "pos_sessions",
        "refresh_tokens",
        "petty_cash_ledger",
        "supplier_rebates",
        "executive_kpi_snapshots",
        "forecast_accuracy_snapshots",
        "store_business_dates",
        "fixed_assets",
        "asset_depreciation_history",
        "bank_transactions",
        "audit_logs",
        "ai_alerts",
        "ai_business_insights",
        "ai_cash_flow_forecasts",
        "ai_customer_intelligences",
        "ai_demand_forecasts",
        "ai_expiry_risk_predictions",
        "ai_financial_anomalies",
        "ai_inventory_shrinkage_analytics",
        "ai_kpi_history",
        "ai_kpi_results",
        "ai_store_performances",
        "ai_supplier_payment_recommendations"
    };

    public async Task ProvisionEnvironmentPairAsync(string baseConnectionString, string tenantName, Guid tenantId)
    {
        var builder = new NpgsqlConnectionStringBuilder(baseConnectionString);
        var sourceDb = builder.Database ?? "posdb";

        // Determine destination database names
        string liveDbName;
        string uatDbName;

        if (tenantId == Guid.Empty)
        {
            // Self-hosted: base database suffix split
            liveDbName = $"{sourceDb}_live";
            uatDbName = $"{sourceDb}_uat";
        }
        else
        {
            // SaaS: tenant suffix mapping
            var prefix = tenantName.ToLower().Replace(" ", "_");
            liveDbName = $"tenant_{prefix}_live";
            uatDbName = $"tenant_{prefix}_uat";
        }

        // Establish administrative connection to postgres db
        builder.Database = "postgres";
        var adminConnStr = builder.ToString();

        // ─────────────────────────────────────────────────────────────────────
        // CLONING STRATEGY — READ BEFORE MODIFYING
        //
        // This method uses CREATE DATABASE ... TEMPLATE x (PostgreSQL template
        // clone). This requires an EXCLUSIVE LOCK on the source database and
        // will FAIL with "database is being accessed by other users" if any
        // sessions remain connected to it.
        //
        // Template clone is ONLY SAFE here because ProvisionEnvironmentPairAsync
        // is called ONCE during initial tenant on-boarding, against a freshly
        // migrated source database that has NO active POS app connections yet.
        //
        // For RECURRING UAT resets (Section 3.2 — periodic refresh of _uat
        // from a current _live snapshot), use RefreshUatFromLiveSnapshotAsync
        // instead. That method uses pg_dump + pg_restore so the _live database
        // can remain fully online during the snapshot.
        // ─────────────────────────────────────────────────────────────────────

        // 1. Terminate any stray connections to source (safe at initial provision;
        //    pg_terminate_backend is also included as a belt-and-braces measure).
        Console.WriteLine($"[Provisioning] Terminating active connections to template database '{sourceDb}'...");
        await RunAdminQueryAsync(adminConnStr, $@"
            SELECT pg_terminate_backend(pid) 
            FROM pg_stat_activity 
            WHERE datname = '{sourceDb}' AND pid <> pg_backend_pid();
        ");

        // 2. Template-clone source into _live
        Console.WriteLine($"[Provisioning] Creating LIVE database '{liveDbName}' from template '{sourceDb}'...");
        await RunAdminQueryAsync(adminConnStr, $"DROP DATABASE IF EXISTS {liveDbName};");
        await RunAdminQueryAsync(adminConnStr, $"CREATE DATABASE {liveDbName} TEMPLATE {sourceDb};");

        // 3. Write metadata to LIVE database
        builder.Database = liveDbName;
        var liveConnStr = builder.ToString();
        await UpdateDatabaseMetadataAsync(liveConnStr, liveDbName, "LIVE", tenantId);

        // 4. Terminate again (belt-and-braces) then template-clone source into _uat
        await RunAdminQueryAsync(adminConnStr, $@"
            SELECT pg_terminate_backend(pid) 
            FROM pg_stat_activity 
            WHERE datname = '{sourceDb}' AND pid <> pg_backend_pid();
        ");

        Console.WriteLine($"[Provisioning] Creating UAT database '{uatDbName}' from template '{sourceDb}'...");
        await RunAdminQueryAsync(adminConnStr, $"DROP DATABASE IF EXISTS {uatDbName};");
        await RunAdminQueryAsync(adminConnStr, $"CREATE DATABASE {uatDbName} TEMPLATE {sourceDb};");

        // 5. Connect to UAT and sanitize (wipe transactional data, reset balances/sequences)
        builder.Database = uatDbName;
        var uatConnStr = builder.ToString();
        await SanitizeUatDatabaseAsync(uatConnStr, uatDbName, tenantId);

        Console.WriteLine($"[Provisioning] Environment databases '{liveDbName}' and '{uatDbName}' provisioned successfully!");
    }

    /// <summary>
    /// RECURRING UAT REFRESH (Section 3.2) — dump-based approach.
    ///
    /// Uses pg_dump + pg_restore so the source _live database remains fully
    /// online with active connections during the operation. This is the correct
    /// approach for periodic UAT resets once the store is trading.
    ///
    /// Template clone (ProvisionEnvironmentPairAsync) is intentionally NOT used
    /// here because CREATE DATABASE ... TEMPLATE requires an exclusive lock on
    /// the source and will fail if any sessions are connected — which will
    /// always be the case for an active _live database.
    ///
    /// SECURITY NOTES:
    /// - The database password is passed ONLY via the subprocess's own PGPASSWORD
    ///   environment variable set on ProcessStartInfo.EnvironmentVariables.
    ///   It is never embedded in command-line arguments (visible in `ps` output
    ///   and process logs) and never set on the host application's own environment.
    /// - The dump file is written to a dedicated temp subfolder (not the shared
    ///   system temp root), with a GUID+timestamp filename, and chmod 600 is
    ///   applied immediately after creation on Linux. The try/finally block
    ///   guarantees deletion even on failure.
    /// - This path MUST be excluded from any log-shipping or backup configuration
    ///   (e.g. Filebeat, rsync, Restic). Add the directory pattern to .backupignore
    ///   or equivalent. The directory: /tmp/pos_uat_dumps/ (Linux) or
    ///   %TEMP%\pos_uat_dumps\ (Windows).
    /// </summary>
    public async Task RefreshUatFromLiveSnapshotAsync(string liveConnectionString, string uatConnectionString, Guid tenantId)
    {
        throw new InvalidOperationException(
            "FATAL SECURITY GUARD: RefreshUatFromLiveSnapshotAsync is hard-blocked. " +
            "posdb_uat currently contains active store transaction data. Overwriting it from posdb_live is strictly prohibited.");

        var liveBuilder = new NpgsqlConnectionStringBuilder(liveConnectionString);
        var uatBuilder = new NpgsqlConnectionStringBuilder(uatConnectionString);
        var liveDb = liveBuilder.Database ?? throw new InvalidOperationException("Live connection string missing database name.");
        var uatDb = uatBuilder.Database ?? throw new InvalidOperationException("UAT connection string missing database name.");
        var host = liveBuilder.Host ?? "localhost";
        var port = liveBuilder.Port > 0 ? liveBuilder.Port : 5432;
        var user = liveBuilder.Username ?? "posadmin";
        // Password is extracted here only to pass as a subprocess env var.
        // It is NEVER appended to the command-line arguments string.
        var password = liveBuilder.Password ?? "";

        Console.WriteLine($"[Provisioning] Starting dump-based UAT refresh: '{liveDb}' → '{uatDb}'...");

        // Create a dedicated temp subfolder — isolated from system-wide /tmp so it
        // can be excluded from log-shipping/backup configs by directory pattern.
        var dumpDir = Path.Combine(Path.GetTempPath(), "pos_uat_dumps");
        Directory.CreateDirectory(dumpDir);

        // GUID + timestamp ensures no filename collision across concurrent runs.
        var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd_HHmmss");
        var dumpFile = Path.Combine(dumpDir, $"uat_{timestamp}_{Guid.NewGuid():N}.dump");

        try
        {
            // Apply chmod 600 on Linux immediately after path is decided but before
            // any data is written — ensures the file is private from creation.
            if (!OperatingSystem.IsWindows())
            {
                // Create the file first so chmod has a target.
                using (File.Create(dumpFile)) { }
                await RunProcessAsync("chmod", $"600 \"{dumpFile}\"", Array.Empty<(string, string)>());
            }

            // 1. Dump _live — password via PGPASSWORD only, never in the argument string.
            Console.WriteLine($"[Provisioning] Dumping '{liveDb}' via pg_dump...");
            await RunProcessAsync(
                "pg_dump",
                $"-h {host} -p {port} -U {user} -Fc -f \"{dumpFile}\" {liveDb}",
                pgPasswordEnv: password);

            // 2. Drop and recreate the _uat database
            uatBuilder.Database = "postgres";
            var adminConnStr = uatBuilder.ToString();
            Console.WriteLine($"[Provisioning] Dropping and recreating '{uatDb}'...");
            await RunAdminQueryAsync(adminConnStr, $"SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = '{uatDb}' AND pid <> pg_backend_pid();");
            await RunAdminQueryAsync(adminConnStr, $"DROP DATABASE IF EXISTS {uatDb};");
            await RunAdminQueryAsync(adminConnStr, $"CREATE DATABASE {uatDb};");

            // 3. Restore — password via PGPASSWORD only, never in the argument string.
            Console.WriteLine($"[Provisioning] Restoring dump into '{uatDb}' via pg_restore...");
            await RunProcessAsync(
                "pg_restore",
                $"-h {host} -p {port} -U {user} -d {uatDb} --no-owner --role={user} \"{dumpFile}\"",
                pgPasswordEnv: password);

            // 4. Sanitize: wipe transactions, reset balances/sequences, update metadata
            await SanitizeUatDatabaseAsync(uatConnectionString, uatDb, tenantId);

            Console.WriteLine($"[Provisioning] UAT refresh from live snapshot complete.");
        }
        finally
        {
            // Guaranteed deletion — runs on both success and any failure path.
            // Suppress errors so a delete failure does not mask the original exception.
            try { if (File.Exists(dumpFile)) File.Delete(dumpFile); }
            catch (Exception ex) { Console.WriteLine($"[Provisioning] WARNING: failed to delete temp dump file '{dumpFile}': {ex.Message}"); }
        }
    }

    /// <summary>
    /// Verifies that pg_dump and pg_restore binaries are available at startup.
    /// Call this from Program.cs startup to fail loudly rather than discovering
    /// missing postgresql-client tools only when a UAT refresh is first attempted.
    /// </summary>
    public static void AssertPgClientToolsAvailable()
    {
        foreach (var tool in new[] { "pg_dump", "pg_restore" })
        {
            try
            {
                var psi = new ProcessStartInfo(tool, "--version")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var proc = Process.Start(psi)
                    ?? throw new InvalidOperationException($"Could not start {tool}.");
                var version = proc.StandardOutput.ReadToEnd();
                proc.WaitForExit();
                if (proc.ExitCode != 0)
                    throw new InvalidOperationException($"{tool} --version exited with code {proc.ExitCode}.");
                Console.WriteLine($"[Provisioning] {tool} available: {version.Trim()}");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"[STARTUP FAILURE] Required binary '{tool}' is not available. " +
                    $"Ensure postgresql-client (version-matched to your Postgres server) is installed in the Docker image. " +
                    $"Inner: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Runs an external process with optional extra environment variables set
    /// ONLY on the subprocess's own ProcessStartInfo — never on the app's environment.
    /// </summary>
    /// <param name="executable">Binary to execute.</param>
    /// <param name="arguments">Command-line arguments. Must NOT contain credentials.</param>
    /// <param name="envVars">Additional env vars set only on the subprocess.</param>
    /// <param name="pgPasswordEnv">
    /// When provided, sets PGPASSWORD on the subprocess's environment ONLY.
    /// Using a dedicated parameter (rather than embedding in envVars) makes
    /// call sites self-documenting about the security boundary.
    /// </param>
    private static async Task RunProcessAsync(
        string executable,
        string arguments,
        (string Key, string Value)[]? envVars = null,
        string? pgPasswordEnv = null)
    {
        var psi = new ProcessStartInfo(executable, arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        // Set extra env vars on the SUBPROCESS only — app process is never mutated.
        if (envVars != null)
            foreach (var (key, value) in envVars)
                psi.EnvironmentVariables[key] = value;

        // PGPASSWORD is set on the subprocess's own environment so it never
        // appears in command-line arguments (visible in `ps` / process logs).
        if (!string.IsNullOrEmpty(pgPasswordEnv))
            psi.EnvironmentVariables["PGPASSWORD"] = pgPasswordEnv;

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start process: {executable}");

        var stdErr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
            throw new InvalidOperationException($"{executable} exited with code {process.ExitCode}: {stdErr}");
    }

    private async Task RunAdminQueryAsync(string connectionString, string sql)
    {
        using (var conn = new NpgsqlConnection(connectionString))
        {
            await conn.OpenAsync();
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = sql;
                await cmd.ExecuteNonQueryAsync();
            }
        }
    }

    private async Task UpdateDatabaseMetadataAsync(string connectionString, string dbName, string mode, Guid tenantId)
    {
        using (var conn = new NpgsqlConnection(connectionString))
        {
            await conn.OpenAsync();
            using (var cmd = conn.CreateCommand())
            {
                // Create database_metadata table if not exists (insurance)
                cmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS database_metadata (
                        id INT PRIMARY KEY DEFAULT 1,
                        database_name VARCHAR(255) NOT NULL,
                        environment_mode VARCHAR(50) NOT NULL,
                        tenant_id UUID NOT NULL,
                        updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
                        CONSTRAINT chk_single_row CHECK (id = 1)
                    );
                ";
                await cmd.ExecuteNonQueryAsync();

                cmd.CommandText = @"
                    INSERT INTO database_metadata (id, database_name, environment_mode, tenant_id, updated_at)
                    VALUES (1, @dbName, @mode, @tenantId, NOW())
                    ON CONFLICT (id) DO UPDATE SET
                        database_name = EXCLUDED.database_name,
                        environment_mode = EXCLUDED.environment_mode,
                        tenant_id = EXCLUDED.tenant_id,
                        updated_at = NOW();
                ";
                cmd.Parameters.AddWithValue("@dbName", dbName);
                cmd.Parameters.AddWithValue("@mode", mode);
                cmd.Parameters.AddWithValue("@tenantId", tenantId);
                await cmd.ExecuteNonQueryAsync();
            }
        }
    }

    private async Task SanitizeUatDatabaseAsync(string connectionString, string dbName, Guid tenantId)
    {
        Console.WriteLine($"[Provisioning] Sanitizing UAT database '{dbName}'...");
        using (var conn = new NpgsqlConnection(connectionString))
        {
            await conn.OpenAsync();

            // 0. Discover which wipe-candidate tables actually exist in this database.
            //    This makes the service safe on minimal test schemas and future schema changes.
            var existingWipeTables = new List<string>();
            using (var cmd = conn.CreateCommand())
            {
                var inList = string.Join(",", WipeTables.Select(t => $"'{t}'"));
                cmd.CommandText = $@"
                    SELECT table_name FROM information_schema.tables
                    WHERE table_schema = 'public'
                      AND table_type = 'BASE TABLE'
                      AND table_name IN ({inList});";
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                        existingWipeTables.Add(reader.GetString(0));
                }
            }

            bool customersWalletExists = false;
            bool sequencesExists = false;
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT table_name, column_name FROM information_schema.columns
                    WHERE table_schema = 'public'
                      AND (
                            (table_name = 'customers' AND column_name = 'wallet_balance')
                         OR (table_name = 'document_sequences' AND column_name = 'current_number')
                      );";
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var tbl = reader.GetString(0);
                        var col = reader.GetString(1);
                        if (tbl == "customers" && col == "wallet_balance") customersWalletExists = true;
                        if (tbl == "document_sequences" && col == "current_number") sequencesExists = true;
                    }
                }
            }

            using (var transaction = await conn.BeginTransactionAsync())
            {
                try
                {
                    // 1. Wipe transactional tables that exist in this schema.
                    //    Environment-aware guard:
                    //    - Development/test: missing tables are silently skipped (integration test DBs
                    //      are minimal schemas that may not have every table yet).
                    //    - Any other environment (Staging, Production): a missing WIPE-list table
                    //      indicates schema drift (a migration was missed or failed). Hard-fail the
                    //      provisioning run so an incomplete UAT wipe is never produced silently.
                    var isDevelopment = string.Equals(
                        Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development",
                        "Development",
                        StringComparison.OrdinalIgnoreCase);

                    var missingTables = WipeTables.Except(existingWipeTables, StringComparer.OrdinalIgnoreCase).ToList();
                    if (missingTables.Count > 0)
                    {
                        if (isDevelopment)
                        {
                            Console.WriteLine($"[Provisioning] WARNING (dev/test): {missingTables.Count} WIPE-list table(s) not found in schema, skipping: {string.Join(", ", missingTables)}");
                        }
                        else
                        {
                            throw new InvalidOperationException(
                                $"[PROVISIONING ABORTED] {missingTables.Count} expected WIPE-list table(s) are missing from the database schema. " +
                                $"This indicates schema drift (a migration may have failed). Missing: {string.Join(", ", missingTables)}. " +
                                $"Fix the schema before retrying the UAT provisioning run.");
                        }
                    }

                    if (existingWipeTables.Count > 0)
                    {
                        var tableList = string.Join(", ", existingWipeTables);
                        using (var cmd = conn.CreateCommand())
                        {
                            cmd.CommandText = $"TRUNCATE TABLE {tableList} CASCADE;";
                            await cmd.ExecuteNonQueryAsync();
                        }
                        Console.WriteLine($"[Provisioning] Truncated {existingWipeTables.Count} transactional table(s).");
                    }
                    else
                    {
                        Console.WriteLine("[Provisioning] No matching transactional tables found — skipping TRUNCATE.");
                    }

                    // 2. Reset customer loyalty/wallet balances (if columns exist)
                    if (customersWalletExists)
                    {
                        using (var cmd = conn.CreateCommand())
                        {
                            cmd.CommandText = "UPDATE customers SET wallet_balance = 0.00, loyalty_points = 0.00;";
                            await cmd.ExecuteNonQueryAsync();
                        }
                    }

                    // 3. Reset document sequence counters (if table exists)
                    if (sequencesExists)
                    {
                        using (var cmd = conn.CreateCommand())
                        {
                            cmd.CommandText = "UPDATE document_sequences SET current_number = 0;";
                            await cmd.ExecuteNonQueryAsync();
                        }
                    }

                    await transaction.CommitAsync();
                    Console.WriteLine($"[Provisioning] UAT sanitization complete.");
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    throw new InvalidOperationException($"Failed sanitizing UAT database: {ex.Message}", ex);
                }
            }
        }

        // 4. Update metadata in UAT database
        await UpdateDatabaseMetadataAsync(connectionString, dbName, "UAT", tenantId);
    }
}
