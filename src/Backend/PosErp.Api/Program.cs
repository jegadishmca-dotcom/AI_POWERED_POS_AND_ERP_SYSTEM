using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using Polly;
using PosErp.Api.Middlewares;
using PosErp.Application.Interfaces;
using PosErp.Infrastructure.Services;
using PosErp.Infrastructure.Persistence;
using PosErp.Infrastructure.Authentication;
using PosErp.Infrastructure.Identity;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using OpenTelemetry.Resources;
using PosErp.Infrastructure.Printing;
using PosErp.Application.Features.Inventory.Services;
using PosErp.Application.Features.Offers.Services;
using PosErp.Application.Features.Crm.Services;
using PosErp.Application.Features.Finance.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using System.Threading.RateLimiting;
using System.Text;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.Extensions.Caching.Memory;

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
var builder = WebApplication.CreateBuilder(args);

// Add Connection String Provider, Provisioning, & Safety Interceptor services
builder.Services.AddMemoryCache();
builder.Services.AddScoped<PosErp.Infrastructure.Services.ConnectionStringProvider>();
builder.Services.AddScoped<IConnectionStringProvider>(provider => provider.GetRequiredService<PosErp.Infrastructure.Services.ConnectionStringProvider>());
builder.Services.AddScoped<PosErp.Infrastructure.Persistence.DbSafetyInterceptor>();
builder.Services.AddScoped<IDatabaseProvisioningService, PosErp.Infrastructure.Services.DatabaseProvisioningService>();

// Add Database Context (PostgreSQL via PgBouncer / direct connection string)
builder.Services.AddDbContext<ApplicationDbContext>((serviceProvider, options) =>
{
    var connectionStringProvider = serviceProvider.GetRequiredService<IConnectionStringProvider>();
    var safetyInterceptor = serviceProvider.GetRequiredService<PosErp.Infrastructure.Persistence.DbSafetyInterceptor>();
    
    options.UseNpgsql(connectionStringProvider.GetConnectionString(),
        npgsqlOptions => 
        {
            npgsqlOptions.CommandTimeout(180);
            npgsqlOptions.EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null);
        });
        
    options.AddInterceptors(safetyInterceptor);
});

builder.Services.AddScoped<IApplicationDbContext>(provider => 
    provider.GetRequiredService<ApplicationDbContext>());


// Health checks — must be registered before MapHealthChecks("/health") is called
builder.Services.AddHealthChecks();

// Add Hangfire Services
builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UsePostgreSqlStorage(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddHangfireServer(options =>
{
    options.WorkerCount = 2;
});

// Add Controllers and Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Phase 6: Reliability - Polly Circuit Breakers & Retries
builder.Services.AddHttpClient("ExternalApis")
    .AddTransientHttpErrorPolicy(policyBuilder => policyBuilder.WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))))
    .AddTransientHttpErrorPolicy(policyBuilder => policyBuilder.CircuitBreakerAsync(5, TimeSpan.FromSeconds(30)));

// Health Checks
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? "Host=localhost;Database=poserp;Username=postgres;Password=postgres";
string redisConnStr = builder.Configuration.GetSection("Redis:ConnectionString").Value ?? "localhost:6379";
builder.Services.AddHealthChecks()
    .AddNpgSql(connectionString, name: "Database")
    .AddRedis(redisConnStr, name: "RedisCache")
    .AddHangfire(options => { options.MinimumAvailableServers = 1; }, name: "Hangfire");

// Phase 6: Monitoring - OpenTelemetry
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("PosErp.Api"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
    )
    .WithMetrics(metrics => metrics
        .AddRuntimeInstrumentation()
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddPrometheusExporter()
    );

// Phase 6: Rate Limiting
/*
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("PosApi", opt =>
    {
        opt.PermitLimit = 100;
        opt.Window = TimeSpan.FromSeconds(1);
    });
    options.AddFixedWindowLimiter("AiApi", opt =>
    {
        opt.PermitLimit = 20;
        opt.Window = TimeSpan.FromSeconds(1);
    });
    options.AddFixedWindowLimiter("AuthApi", opt =>
    {
        opt.PermitLimit = 5;
        opt.Window = TimeSpan.FromSeconds(1);
    });
});
*/

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantProvider, TenantProvider>();
builder.Services.AddScoped<PosErp.Application.Interfaces.IAccountResolutionService, PosErp.Infrastructure.Services.AccountResolutionService>();

// Redis Configuration
string redisConnectionString = builder.Configuration.GetSection("Redis:ConnectionString").Value ?? "localhost:6379";
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var configOptions = ConfigurationOptions.Parse(redisConnectionString);
    configOptions.AbortOnConnectFail = false;
    configOptions.ConnectTimeout = 1000;
    configOptions.SyncTimeout = 500;
    configOptions.AsyncTimeout = 500;
    return ConnectionMultiplexer.Connect(configOptions);
});
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = redisConnectionString;
    options.InstanceName = "AppleSupermarket_";
});

// Register Memory Cache for OfferEngine
builder.Services.AddMemoryCache();

// Register MediatR
builder.Services.AddMediatR(cfg => {
    cfg.RegisterServicesFromAssembly(typeof(IApplicationDbContext).Assembly);
});

// Register Infrastructure Services
builder.Services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<IPrintService, EscPosPrintService>();
builder.Services.AddScoped<IEmailService, PosErp.Infrastructure.Services.SmtpEmailService>();
builder.Services.AddScoped<INotificationService, PosErp.Infrastructure.Services.NotificationService>();

// S1: JWT secret from environment variable (set JWT__Secret on Render / secrets manager).
// NEVER use the fallback value in Production — startup will throw if it is missing.
var jwtSecret = builder.Configuration["JWT__Secret"]
    ?? builder.Configuration["JWT:Secret"]
    ?? (builder.Environment.IsDevelopment()
        ? "DevOnlyFallbackKey_ReplaceWithEnvVarInProduction_MinLength64Chars1234567890ABCD"
        : throw new InvalidOperationException(
            "FATAL: JWT__Secret environment variable is not set. " +
            "Set it on Render under Environment > Secret Files or Environment Variables."));

var key = Encoding.UTF8.GetBytes(jwtSecret);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = "PosErp",
        ValidAudience = "PosErpClient",
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ClockSkew = TimeSpan.Zero
    };
    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = async context =>
        {
            var config = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
            var connProvider = context.HttpContext.RequestServices.GetRequiredService<IConnectionStringProvider>();
            var tenantProvider = context.HttpContext.RequestServices.GetRequiredService<ITenantProvider>();
            var memoryCache = context.HttpContext.RequestServices.GetRequiredService<IMemoryCache>();

            var tokenVersionClaim = context.Principal?.FindFirst("token_version")?.Value;
            if (string.IsNullOrEmpty(tokenVersionClaim))
            {
                context.Fail("Missing token version claim.");
                return;
            }

            var deploymentMode = config["SystemConfig:DeploymentMode"] ?? "SelfHosted";
            var cacheKey = string.Equals(deploymentMode, "SaaS", StringComparison.OrdinalIgnoreCase)
                ? $"token_ver_{tenantProvider.TenantId}"
                : "token_ver_selfhosted";

            if (!memoryCache.TryGetValue(cacheKey, out int currentVersion))
            {
                currentVersion = 1;

                if (string.Equals(deploymentMode, "SaaS", StringComparison.OrdinalIgnoreCase))
                {
                    var platformConn = config.GetConnectionString("DefaultConnection") 
                        ?? "Host=localhost;Database=poserp;Username=postgres;Password=postgres";
                    try
                    {
                        await using var conn = new Npgsql.NpgsqlConnection(platformConn);
                        await conn.OpenAsync();
                        await using var cmd = conn.CreateCommand();
                        cmd.CommandText = "SELECT token_version FROM tenant_environments WHERE tenant_id = @p0";
                        var p = cmd.CreateParameter();
                        p.ParameterName = "@p0";
                        p.Value = tenantProvider.TenantId;
                        cmd.Parameters.Add(p);
                        var res = await cmd.ExecuteScalarAsync();
                        if (res != null && res != DBNull.Value)
                        {
                            currentVersion = Convert.ToInt32(res);
                        }
                    }
                    catch
                    {
                        // Fail-safe default
                    }
                }
                else
                {
                    var connProviderImpl = connProvider as PosErp.Infrastructure.Services.ConnectionStringProvider;
                    if (connProviderImpl != null)
                    {
                        currentVersion = connProviderImpl.GetSelfHostedTokenVersion();
                    }
                }

                memoryCache.Set(cacheKey, currentVersion, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
                });
            }

            if (tokenVersionClaim != currentVersion.ToString())
            {
                context.Fail("Token version mismatch. Session invalidated due to environment toggle.");
            }
        }
    };
});

// Register Application Layer Services
builder.Services.AddScoped<IStockLedgerService, StockLedgerService>();
builder.Services.AddScoped<IProductBatchService, ProductBatchService>();
builder.Services.AddScoped<IOfferEngine, OfferEngine>();
builder.Services.AddScoped<IOfferExportService, PosErp.Infrastructure.Services.Offers.OffersImportExportService>();
builder.Services.AddScoped<IOfferImportService, PosErp.Infrastructure.Services.Offers.OffersImportExportService>();
builder.Services.AddScoped<IWalletService, WalletService>();
builder.Services.AddScoped<ILoyaltyService, LoyaltyService>();
builder.Services.AddScoped<PosErp.Application.Features.Audit.Services.IAuditLoggingService, PosErp.Application.Features.Audit.Services.AuditLoggingService>();
builder.Services.AddScoped<ICustomerTierService, CustomerTierService>();
builder.Services.AddScoped<IFinancialPostingService, FinancialPostingService>();
builder.Services.AddScoped<IFinancialReportingService, FinancialReportingService>();
builder.Services.AddScoped<IEInvoiceService, EInvoiceService>();
builder.Services.AddScoped<IEmailSettingsManager, EmailSettingsManager>();
builder.Services.AddScoped<PosErp.Application.Features.Finance.Services.IPeriodLockService, PosErp.Application.Features.Finance.Services.PeriodLockService>();
builder.Services.AddScoped<PosErp.Application.Features.Finance.Services.IDocumentSequenceService, PosErp.Application.Features.Finance.Services.DocumentSequenceService>();
builder.Services.AddScoped<PosErp.Application.Features.Finance.Services.IApprovalWorkflowService, PosErp.Application.Features.Finance.Services.ApprovalWorkflowService>();
builder.Services.AddScoped<PosErp.Application.Features.Finance.Services.IAllocationEngine, PosErp.Application.Features.Finance.Services.AllocationEngine>();
builder.Services.AddScoped<PosErp.Application.Features.Analytics.Services.IAiAnalyticsService, PosErp.Application.Features.Analytics.Services.AiAnalyticsService>();
builder.Services.AddScoped<PosErp.Application.Features.Analytics.Services.INaturalLanguageQueryService, PosErp.Application.Features.Analytics.Services.NaturalLanguageQueryService>();
builder.Services.AddScoped<PosErp.Application.Features.Loyalty.Jobs.ILoyaltyBackgroundJobs, PosErp.Application.Features.Loyalty.Jobs.LoyaltyBackgroundJobs>();

// Phase 5 AI Engines
builder.Services.AddScoped<PosErp.Application.Features.Ai.Services.IInsightEngine, PosErp.Application.Features.Ai.Services.InsightEngine>();
builder.Services.AddScoped<PosErp.Application.Features.Ai.Services.IForecastEngine, PosErp.Application.Features.Ai.Services.ForecastEngine>();
builder.Services.AddScoped<PosErp.Application.Features.Ai.Services.IRecommendationEngine, PosErp.Application.Features.Ai.Services.RecommendationEngine>();
builder.Services.AddScoped<PosErp.Application.Features.Ai.Jobs.IAiBackgroundJobs, PosErp.Application.Features.Ai.Jobs.AiBackgroundJobs>();

// Procurement Recommendation Engine (was missing from DI — caused 500 on /api/procurement/recommendations)
builder.Services.AddScoped<PosErp.Application.Features.Inventory.Services.IPurchaseRecommendationEngine, PosErp.Application.Features.Inventory.Services.PurchaseRecommendationEngine>();

// Register Materialized View Periodic Refresher
builder.Services.AddHostedService<PosErp.Infrastructure.Jobs.StockPositionRefreshService>();
builder.Services.AddSingleton<PosErp.Infrastructure.Jobs.DailyReportEmailService>();
builder.Services.AddHostedService<PosErp.Infrastructure.Jobs.DailyReportEmailService>(
    sp => sp.GetRequiredService<PosErp.Infrastructure.Jobs.DailyReportEmailService>());
builder.Services.AddHostedService<PosErp.Infrastructure.Jobs.RefreshTokenCleanupService>();

// M1: CORS — restrict to known frontend origin in Production; allow all in Development.
// Set FRONTEND_URL environment variable on Render (e.g. https://apple-supermarket.vercel.app)
var frontendUrl = builder.Configuration["FRONTEND_URL"] ?? "";
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        if (builder.Environment.IsDevelopment() || string.IsNullOrWhiteSpace(frontendUrl))
        {
            // Development: allow any origin for local testing
            policy.SetIsOriginAllowed(_ => true)
                  .AllowCredentials()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        }
        else
        {
            // Production: restrict to known Vercel frontend URL
            policy.WithOrigins(frontendUrl)
                  .AllowCredentials()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        }
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
// S2: Swagger only in Development — never expose interactive API docs in Production.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");

app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseMiddleware<RateLimitingMiddleware>();

app.UseHttpsRedirection();

// Phase 6: Security Headers Middleware
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    context.Response.Headers.Append("Content-Security-Policy", "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; font-src 'self'; connect-src 'self'");
    context.Response.Headers.Append("Strict-Transport-Security", "max-age=31536000; includeSubDomains");
    await next();
});

//app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<DemoSandboxMiddleware>();

// Configure Hangfire Dashboard
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new PosErp.Api.Infrastructure.HangfireAuthorizationFilter() }
});

// Schedule background AI jobs
using (var scope = app.Services.CreateScope())
{
    var manager = scope.ServiceProvider.GetRequiredService<IRecurringJobManager>();
    manager.AddOrUpdate<PosErp.Application.Features.Analytics.Services.IAiAnalyticsService>(
        "ai-daily-full-rebuild",
        service => service.RecalculateAllAnalyticsAsync(CancellationToken.None),
        Cron.Daily);

    manager.AddOrUpdate<PosErp.Application.Features.Analytics.Services.IAiAnalyticsService>(
        "ai-hourly-incremental-refresh",
        service => service.RecalculateIncrementalAnalyticsAsync(CancellationToken.None),
        Cron.Hourly);

    manager.AddOrUpdate<PosErp.Application.Features.Loyalty.Jobs.ILoyaltyBackgroundJobs>(
        "loyalty-point-expiration",
        service => service.ExpirePointsJob(),
        Cron.Daily(2, 0)); // 02:00 AM

    manager.AddOrUpdate<PosErp.Application.Features.Loyalty.Jobs.ILoyaltyBackgroundJobs>(
        "loyalty-tier-downgrade",
        service => service.EvaluateTierDowngradeJob(),
        Cron.Monthly(1, 1, 0)); // 1st day of month at 01:00 AM

    manager.AddOrUpdate<PosErp.Application.Features.Loyalty.Jobs.ILoyaltyBackgroundJobs>(
        "loyalty-birthday-bonus",
        service => service.BirthdayBonusJob(),
        Cron.Daily(3, 0)); // 03:00 AM

    manager.AddOrUpdate<PosErp.Application.Features.Loyalty.Jobs.ILoyaltyBackgroundJobs>(
        "loyalty-anniversary-bonus",
        service => service.AnniversaryBonusJob(),
        Cron.Daily(3, 30)); // 03:30 AM

    manager.AddOrUpdate<PosErp.Application.Features.Loyalty.Jobs.ILoyaltyBackgroundJobs>(
        "loyalty-health-maintenance",
        service => service.LoyaltyMaintenanceJob(),
        Cron.Weekly(DayOfWeek.Sunday, 4, 0)); // Sunday at 04:00 AM

    // Phase 5 AI Scheduled Jobs
    manager.AddOrUpdate<PosErp.Application.Features.Ai.Jobs.IAiBackgroundJobs>(
        "ai-insight-generation",
        service => service.ExecuteInsightGenerationJobAsync(CancellationToken.None),
        Cron.Daily(1, 0)); // 01:00 AM

    manager.AddOrUpdate<PosErp.Application.Features.Ai.Jobs.IAiBackgroundJobs>(
        "ai-forecast-generation",
        service => service.ExecuteForecastGenerationJobAsync(CancellationToken.None),
        Cron.Daily(2, 0)); // 02:00 AM

    manager.AddOrUpdate<PosErp.Application.Features.Ai.Jobs.IAiBackgroundJobs>(
        "ai-customer-intelligence",
        service => service.ExecuteCustomerIntelligenceJobAsync(CancellationToken.None),
        Cron.Daily(3, 0)); // 03:00 AM

    manager.AddOrUpdate<PosErp.Application.Features.Ai.Jobs.IAiBackgroundJobs>(
        "ai-executive-snapshot",
        service => service.ExecuteExecutiveSnapshotJobAsync(CancellationToken.None),
        Cron.Daily(23, 0)); // 11:00 PM

    manager.AddOrUpdate<PosErp.Application.Features.Ai.Jobs.IAiBackgroundJobs>(
        "ai-forecast-accuracy",
        service => service.ExecuteForecastAccuracyJobAsync(CancellationToken.None),
        Cron.Daily(4, 0)); // 04:00 AM (after forecast)

    manager.AddOrUpdate<PosErp.Application.Features.Ai.Jobs.IAiBackgroundJobs>(
        "ai-alert-generation",
        service => service.ExecuteAlertGenerationJobAsync(CancellationToken.None),
        Cron.Hourly()); // Hourly
}

app.MapControllers();
app.MapHealthChecks("/health");
app.MapPrometheusScrapingEndpoint();

// Seed Database
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        
        // Ensure database exists
        context.Database.EnsureCreated();

        // Create migration history table if not exists
        await context.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS migration_history (
                migration_name VARCHAR(255) PRIMARY KEY,
                applied_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
            );
        ");

        // Create tenant_environments platform table if not exists (for SaaS mode)
        await context.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS tenant_environments (
                tenant_id UUID PRIMARY KEY,
                active_mode VARCHAR(50) NOT NULL DEFAULT 'LIVE',
                live_connection_string TEXT NOT NULL,
                uat_connection_string TEXT NOT NULL,
                token_version INT NOT NULL DEFAULT 1,
                updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
            );
        ");

        // Ensure token_version column is present if table already exists
        await context.Database.ExecuteSqlRawAsync(@"
            ALTER TABLE tenant_environments ADD COLUMN IF NOT EXISTS token_version INT NOT NULL DEFAULT 1;
        ");

        // Perform environment and tenant safety assertion
        await VerifyDatabaseMetadataAsync(context, services);

        var connection = context.Database.GetDbConnection();
        var wasOpen = connection.State == System.Data.ConnectionState.Open;
        if (!wasOpen) await connection.OpenAsync();

        // Scan and execute all pending raw SQL migrations in alphabetical order
        var migrationsDir = Path.Combine(AppContext.BaseDirectory, "Persistence", "Migrations");
        if (Directory.Exists(migrationsDir))
        {
            var sqlFiles = Directory.GetFiles(migrationsDir, "*.sql")
                                    .OrderBy(f => Path.GetFileName(f))
                                    .ToList();

            // Prevent crash if tables were created manually without migration_history
            using (var checkCmd = connection.CreateCommand())
            {
                checkCmd.CommandText = "SELECT EXISTS(SELECT 1 FROM information_schema.tables WHERE table_name = 'roles')";
                var rolesExist = (bool)(await checkCmd.ExecuteScalarAsync() ?? false);

                if (rolesExist)
                {
                    // Seed migration_history for the original 17 migrations
                    foreach (var f in sqlFiles)
                    {
                        var filename = Path.GetFileName(f);
                        var prefixStr = filename.Split('_')[0];
                        if (int.TryParse(prefixStr, out int prefixNum) && prefixNum <= 17)
                        {
                            var seedCmd = connection.CreateCommand();
                            seedCmd.CommandText = "INSERT INTO migration_history (migration_name) VALUES (@p0) ON CONFLICT DO NOTHING";
                            var p = seedCmd.CreateParameter();
                            p.ParameterName = "@p0";
                            p.Value = filename;
                            seedCmd.Parameters.Add(p);
                            await seedCmd.ExecuteNonQueryAsync();
                        }
                    }
                }
            }

            foreach (var sqlFile in sqlFiles)
            {
                var filename = Path.GetFileName(sqlFile);
                bool exists = false;

                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "SELECT EXISTS(SELECT 1 FROM migration_history WHERE migration_name = @p0)";
                    var param = cmd.CreateParameter();
                    param.ParameterName = "@p0";
                    param.Value = filename;
                    cmd.Parameters.Add(param);

                    var result = await cmd.ExecuteScalarAsync();
                    exists = result != null && (bool)result;
                }

                if (!exists)
                {
                    Console.WriteLine($"Applying database migration: {filename}...");
                    var sqlContent = await File.ReadAllTextAsync(sqlFile);

                    // Use raw ADO.NET to avoid ExecuteSqlRawAsync interpreting { } in JSON as format placeholders
                    var conn = context.Database.GetDbConnection();
                    var connWasOpen = conn.State == System.Data.ConnectionState.Open;
                    if (!connWasOpen) await conn.OpenAsync();

                    using (var execCmd = conn.CreateCommand())
                    {
                        execCmd.CommandText = sqlContent;
                        await execCmd.ExecuteNonQueryAsync();
                    }

                    using (var histCmd = conn.CreateCommand())
                    {
                        histCmd.CommandText = "INSERT INTO migration_history (migration_name) VALUES (@mig)";
                        var migParam = histCmd.CreateParameter();
                        migParam.ParameterName = "@mig";
                        migParam.Value = filename;
                        histCmd.Parameters.Add(migParam);
                        await histCmd.ExecuteNonQueryAsync();
                    }

                    if (!connWasOpen) await conn.CloseAsync();
                    Console.WriteLine($"Migration {filename} applied successfully!");
                }
            }
            if (!wasOpen) await connection.CloseAsync();
        }
        
        // Execute raw DDL to guarantee refresh_tokens table exists (EnsureCreated skips if other tables are present)
        await context.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS refresh_tokens (
                id UUID PRIMARY KEY,
                user_id UUID NOT NULL,
                token VARCHAR(512) NOT NULL,
                token_family VARCHAR(255) NOT NULL,
                expires_at TIMESTAMP WITH TIME ZONE NOT NULL,
                device_id VARCHAR(255) NOT NULL,
                is_revoked BOOLEAN NOT NULL DEFAULT FALSE,
                created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
            );
        ");

        // DDL patch: add individual tender amount columns to invoices (idempotent)
        await context.Database.ExecuteSqlRawAsync(@"
            ALTER TABLE invoices ADD COLUMN IF NOT EXISTS cash_amount   NUMERIC(18,2) NOT NULL DEFAULT 0;
            ALTER TABLE invoices ADD COLUMN IF NOT EXISTS upi_amount    NUMERIC(18,2) NOT NULL DEFAULT 0;
            ALTER TABLE invoices ADD COLUMN IF NOT EXISTS card_amount   NUMERIC(18,2) NOT NULL DEFAULT 0;
            ALTER TABLE invoices ADD COLUMN IF NOT EXISTS wallet_amount NUMERIC(18,2) NOT NULL DEFAULT 0;
        ");

        // DDL patch: add rejection_reason column to grn_items (idempotent)
        await context.Database.ExecuteSqlRawAsync(@"
            ALTER TABLE grn_items ADD COLUMN IF NOT EXISTS rejection_reason VARCHAR(500);
        ");

        // DDL patch: add has_expiry column to products (idempotent)
        // Required by ProductBatchService to know if expiry date is mandatory
        await context.Database.ExecuteSqlRawAsync(@"
            ALTER TABLE products ADD COLUMN IF NOT EXISTS has_expiry BOOLEAN DEFAULT TRUE;
        ");

        // DDL patch: create pending_price_approvals table
        await context.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS pending_price_approvals (
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
            );
        ");

        // Correct has_expiry for seeded non-perishable items
        await context.Database.ExecuteSqlRawAsync(@"
            UPDATE products 
            SET has_expiry = FALSE 
            WHERE product_code IN ('PROD-003', 'PROD-005', 'PROD-006', 'PROD-007')
              AND has_expiry = TRUE;
        ");

        // GST Slab master + HsnMasterIndia2026 seeding
        await PosErp.Api.Infrastructure.GstMasterSeeder.SeedAsync(context);

        // Ensure posdb_audit database and environment_audit_logs table exist for Self-hosted environment toggle auditing (GAP-003)
        var startupConfig = services.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>();
        var startupDeploymentMode = startupConfig["SystemConfig:DeploymentMode"] ?? "SelfHosted";
        if (string.Equals(startupDeploymentMode, "SelfHosted", StringComparison.OrdinalIgnoreCase))
        {
            var defaultConnection = startupConfig.GetConnectionString("DefaultConnection") 
                ?? "Host=localhost;Database=poserp;Username=postgres;Password=postgres";
            
            try
            {
                var connBuilder = new Npgsql.NpgsqlConnectionStringBuilder(defaultConnection);
                var auditDbName = "posdb_audit";

                // Connect to postgres system database to create posdb_audit
                connBuilder.Database = "postgres";
                using (var adminConn = new Npgsql.NpgsqlConnection(connBuilder.ToString()))
                {
                    await adminConn.OpenAsync();
                    using (var cmd = adminConn.CreateCommand())
                    {
                        cmd.CommandText = $"CREATE DATABASE {auditDbName};";
                        try
                        {
                            await cmd.ExecuteNonQueryAsync();
                            Console.WriteLine($"[Startup] Created dedicated audit database '{auditDbName}' successfully.");
                        }
                        catch (Npgsql.PostgresException ex) when (ex.SqlState == "42P04") // duplicate_database
                        {
                            // Already exists, ignore
                        }
                    }
                }

                // Connect to posdb_audit database to create environment_audit_logs table
                connBuilder.Database = auditDbName;
                using (var auditConn = new Npgsql.NpgsqlConnection(connBuilder.ToString()))
                {
                    await auditConn.OpenAsync();
                    using (var cmd = auditConn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            CREATE TABLE IF NOT EXISTS environment_audit_logs (
                                id UUID PRIMARY KEY,
                                user_id UUID,
                                user_name VARCHAR(255),
                                action VARCHAR(100),
                                entity_type VARCHAR(100),
                                details TEXT,
                                timestamp TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
                                ip_address VARCHAR(100),
                                tenant_id UUID
                            );";
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Startup WARNING] Failed to provision dedicated audit database/table: {ex.Message}");
            }
        }

        // ── PRODUCT TAX SLAB CORRECTION (idempotent) ─────────────────────────
        // Seeded products were created when only the old 18% slab existed.
        // Now we correct each product to its legally correct Indian GST slab.
        //
        // SLAB_0   = 10000000-0000-0000-0000-000000000001 → 0%  (Exempt)
        // SLAB_5   = 10000000-0000-0000-0000-000000000002 → 5%
        // SLAB_18  = 10000000-0000-0000-0000-000000000004 → 18%
        //
        // Tata Salt 1kg        → GST 0%  (Salt is FULLY EXEMPT, Notif 2/2017-CT(R) Sl.102)
        // Aashirvaad Atta 5kg  → GST 5%  (Branded pre-packed atta, w.e.f. 18-Jul-2022)
        // Britannia Bourbon    → GST 18% (Biscuits, Notif 1/2017-CT(R) Sch-III Sl.77)
        // Cadbury Dairy Milk   → GST 18% (Chocolate, Notif 1/2017-CT(R) Sch-III Sl.68)
        // Surf Excel Easy Wash → GST 18% (Detergent, Notif 1/2017-CT(R) Sch-III Sl.167)
        await context.Database.ExecuteSqlRawAsync(@"
            -- Tata Salt 1kg: 0% GST (fully exempt under Indian GST law)
            UPDATE products
            SET    tax_slab_id = '10000000-0000-0000-0000-000000000001'
            WHERE  product_code = 'PROD-003'
              AND  tax_slab_id != '10000000-0000-0000-0000-000000000001';

            -- Aashirvaad Shudh Chakki Atta 5kg: 5% GST (branded pre-packed atta)
            UPDATE products
            SET    tax_slab_id = '10000000-0000-0000-0000-000000000002'
            WHERE  product_code = 'PROD-002'
              AND  tax_slab_id != '10000000-0000-0000-0000-000000000002';

            -- Britannia Bourbon, Cadbury Dairy Milk, Surf Excel: already 18% — ensure correct
            UPDATE products
            SET    tax_slab_id = '10000000-0000-0000-0000-000000000004'
            WHERE  product_code IN ('PROD-001', 'PROD-004', 'PROD-005')
              AND  tax_slab_id != '10000000-0000-0000-0000-000000000004';
        ");
        Console.WriteLine("[TAX] Product GST slabs corrected: Salt=0%, Atta=5%, Biscuit/Choc/Detergent=18%");

        // Retrieve or insert 'Owner' and 'Cashier' roles dynamically using EF Core
        var ownerRole = await context.Roles.FirstOrDefaultAsync(r => r.Name == "Owner");
        if (ownerRole == null)
        {
            ownerRole = new PosErp.Domain.Entities.Auth.Role
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                Name = "Owner",
                Description = "System Owner / Administrator"
            };
            context.Roles.Add(ownerRole);
            await context.SaveChangesAsync();
        }

        var cashierRole = await context.Roles.FirstOrDefaultAsync(r => r.Name == "Cashier");
        if (cashierRole == null)
        {
            cashierRole = new PosErp.Domain.Entities.Auth.Role
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000002"),
                Name = "Cashier",
                Description = "POS Cashier Clerk"
            };
            context.Roles.Add(cashierRole);
            await context.SaveChangesAsync();
        }

        var supervisorRole = await context.Roles.FirstOrDefaultAsync(r => r.Name == "Supervisor");
        if (supervisorRole == null)
        {
            supervisorRole = new PosErp.Domain.Entities.Auth.Role
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000003"),
                Name = "Supervisor",
                Description = "POS Shift Supervisor / Override Manager"
            };
            context.Roles.Add(supervisorRole);
            await context.SaveChangesAsync();
        }

        var managerRole = await context.Roles.FirstOrDefaultAsync(r => r.Name == "Manager");
        if (managerRole == null)
        {
            managerRole = new PosErp.Domain.Entities.Auth.Role
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000004"),
                Name = "Manager",
                Description = "Store Operations Manager"
            };
            context.Roles.Add(managerRole);
            await context.SaveChangesAsync();
        }

        var developerRole = await context.Roles.FirstOrDefaultAsync(r => r.Name == "Developer");
        if (developerRole == null)
        {
            developerRole = new PosErp.Domain.Entities.Auth.Role
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000005"),
                Name = "Developer",
                Description = "System Developer / Operations Admin"
            };
            context.Roles.Add(developerRole);
            await context.SaveChangesAsync();
        }
        
        var passwordHasher = services.GetRequiredService<IPasswordHasher>();
        bool usersChanged = false;
        
        // Seed Admin User
        if (!await context.Users.AnyAsync(u => u.Username == "admin@supermarket.local"))
        {
            var adminUser = new PosErp.Domain.Entities.Auth.User
            {
                Username = "admin@supermarket.local",
                PasswordHash = passwordHasher.HashPassword("Admin@123!"),
                PinHash = passwordHasher.HashPassword("1234"), // Default override PIN — CHANGE AFTER FIRST LOGIN
                FullName = "System Administrator",
                RoleId = ownerRole.Id,
                IsActive = true
            };
            context.Users.Add(adminUser);
            usersChanged = true;
        }
        else
        {
            // Ensure existing admin has a PinHash set (for upgrades from older versions)
            var existingAdmin = await context.Users.FirstOrDefaultAsync(u => u.Username == "admin@supermarket.local");
            if (existingAdmin != null && existingAdmin.PinHash == null)
            {
                existingAdmin.PinHash = passwordHasher.HashPassword("1234");
                usersChanged = true;
                Console.WriteLine("[PIN] Default override PIN set for admin user. Please change it via Settings.");
            }
        }

        // Seed Developer User
        if (!await context.Users.AnyAsync(u => u.Username == "developer@supermarket.local"))
        {
            var configuration = services.GetRequiredService<IConfiguration>();
            var devPasswordHash = configuration["SystemConfig:DeveloperPasswordHash"];
            var devUser = new PosErp.Domain.Entities.Auth.User
            {
                Username = "developer@supermarket.local",
                PasswordHash = !string.IsNullOrEmpty(devPasswordHash) ? devPasswordHash : "", // Empty/disabled if not configured in environment
                PinHash = null, // No default PIN seeded for Developer
                FullName = "System Developer",
                RoleId = developerRole.Id,
                IsActive = !string.IsNullOrEmpty(devPasswordHash) // Active only if password hash is configured
            };
            context.Users.Add(devUser);
            usersChanged = true;
            Console.WriteLine("[DEV] Developer user seeded (disabled by default unless SystemConfig:DeveloperPasswordHash is configured).");
        }
        
        // Seed Demo Sandbox User
        if (!await context.Users.AnyAsync(u => u.Username == "demo@supermarket.com"))
        {
            var demoUser = new PosErp.Domain.Entities.Auth.User
            {
                Username = "demo@supermarket.com",
                PasswordHash = passwordHasher.HashPassword("Demo@123456"),
                PinHash = passwordHasher.HashPassword("1234"), // Default override PIN
                FullName = "Demo Sandbox User",
                RoleId = ownerRole.Id,
                IsActive = true
            };
            context.Users.Add(demoUser);
            usersChanged = true;
            Console.WriteLine("[SEED] Seeded demo@supermarket.com sandbox user.");
        }
        else
        {
            // Ensure existing demo user has the correct password (one-time upgrade)
            var existingDemo = await context.Users.FirstOrDefaultAsync(u => u.Username == "demo@supermarket.com");
            if (existingDemo != null && !passwordHasher.VerifyPassword("Demo@123456", existingDemo.PasswordHash))
            {
                existingDemo.PasswordHash = passwordHasher.HashPassword("Demo@123456");
                usersChanged = true;
                Console.WriteLine("[SEED] Updated demo@supermarket.com password to meet policy requirements.");
            }
        }
        
        // Seed Cashier 01 User
        if (!await context.Users.AnyAsync(u => u.Username == "cashier@supermarket.local"))
        {
            var cashierUser = new PosErp.Domain.Entities.Auth.User
            {
                Username = "cashier@supermarket.local",
                PasswordHash = passwordHasher.HashPassword("Cashier@123!"),
                FullName = "Terminal Cashier 01",
                RoleId = cashierRole.Id,
                IsActive = true
            };
            context.Users.Add(cashierUser);
            usersChanged = true;
        }

        // Seed Cashier 02 User
        if (!await context.Users.AnyAsync(u => u.Username == "cashier02@supermarket.local"))
        {
            var cashierUser2 = new PosErp.Domain.Entities.Auth.User
            {
                Username = "cashier02@supermarket.local",
                PasswordHash = passwordHasher.HashPassword("Cashier@123!"),
                FullName = "Terminal Cashier 02",
                RoleId = cashierRole.Id,
                IsActive = true
            };
            context.Users.Add(cashierUser2);
            usersChanged = true;
        }

        // Seed Cashier 03 User
        if (!await context.Users.AnyAsync(u => u.Username == "cashier03@supermarket.local"))
        {
            var cashierUser3 = new PosErp.Domain.Entities.Auth.User
            {
                Username = "cashier03@supermarket.local",
                PasswordHash = passwordHasher.HashPassword("Cashier@123!"),
                FullName = "Terminal Cashier 03",
                RoleId = cashierRole.Id,
                IsActive = true
            };
            context.Users.Add(cashierUser3);
            usersChanged = true;
        }
        
        if (usersChanged)
        {
            await context.SaveChangesAsync();
            Console.WriteLine("Database seeded/updated successfully with default users.");
        }

        // Seed default Tax Slab if empty
        var taxSlab = await context.TaxSlabs.FirstOrDefaultAsync();
        if (taxSlab == null)
        {
            taxSlab = new PosErp.Domain.Entities.Catalog.TaxSlab
            {
                Id = Guid.NewGuid(),
                Name = "GST 18%",
                CgstRate = 9.0m,
                SgstRate = 9.0m,
                IgstRate = 18.0m,
                CessRate = 0.0m
            };
            context.TaxSlabs.Add(taxSlab);
            await context.SaveChangesAsync();
        }

        // Seed default Customer Tiers if empty
        if (!await context.CustomerTiers.AnyAsync())
        {
            var t1 = new PosErp.Domain.Entities.Crm.CustomerTier
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000011"),
                Name = "Silver",
                Level = 1,
                MinimumSpend = 0.00m,
                PointsEarnMultiplier = 1.0m
            };
            var t2 = new PosErp.Domain.Entities.Crm.CustomerTier
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000012"),
                Name = "Gold",
                Level = 2,
                MinimumSpend = 5000.00m,
                PointsEarnMultiplier = 1.2m
            };
            var t3 = new PosErp.Domain.Entities.Crm.CustomerTier
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000013"),
                Name = "Platinum",
                Level = 3,
                MinimumSpend = 15000.00m,
                PointsEarnMultiplier = 1.5m
            };
            context.CustomerTiers.AddRange(t1, t2, t3);
            await context.SaveChangesAsync();
            Console.WriteLine("Database seeded successfully with default customer tiers.");
        }

        // Seed default Chart of Accounts (COA)
        if (!await context.Accounts.AnyAsync())
        {
            var accounts = new List<PosErp.Domain.Entities.Finance.Account>
            {
                new() { AccountCode = "1000", Name = "Cash Tender", AccountType = "ASSET" },
                new() { AccountCode = "1100", Name = "Digital Tender", AccountType = "ASSET" },
                new() { AccountCode = "2100", Name = "Wallet Redemption", AccountType = "LIABILITY" },
                new() { AccountCode = "2200", Name = "Output CGST", AccountType = "LIABILITY" },
                new() { AccountCode = "2201", Name = "Output SGST", AccountType = "LIABILITY" },
                new() { AccountCode = "4000", Name = "Sales Revenue", AccountType = "REVENUE" }
            };
            context.Accounts.AddRange(accounts);
            await context.SaveChangesAsync();
            Console.WriteLine("Database seeded successfully with default Chart of Accounts.");
        }

        // Seed initial products if empty
        if (!await context.Products.AnyAsync())
        {


            var p1 = new PosErp.Domain.Entities.Catalog.Product
            {
                Id = Guid.NewGuid(),
                ProductCode = "PROD-001",
                Name = "Britannia Bourbon 150g",
                TamilName = "பிரிட்டானியா போர்பன்",
                Description = "Chocolate sandwich biscuits",
                TaxSlabId = taxSlab.Id,
                Mrp = 30.00m,
                SellingPrice = 30.00m,
                PurchasePrice = 24.00m,
                IsWeighable = false,
                IsActive = true
            };
            p1.Barcodes.Add(new PosErp.Domain.Entities.Catalog.Barcode
            {
                Id = Guid.NewGuid(),
                BarcodeValue = "8901063012345",
                IsPrimary = true
            });

            var p2 = new PosErp.Domain.Entities.Catalog.Product
            {
                Id = Guid.NewGuid(),
                ProductCode = "PROD-002",
                Name = "Aashirvaad Shudh Chakki Atta 5kg",
                TamilName = "ஆசிர்வாத் கோதுமை மாவு",
                Description = "Whole wheat flour",
                TaxSlabId = taxSlab.Id,
                Mrp = 290.00m,
                SellingPrice = 290.00m,
                PurchasePrice = 240.00m,
                IsWeighable = false,
                IsActive = true
            };
            p2.Barcodes.Add(new PosErp.Domain.Entities.Catalog.Barcode
            {
                Id = Guid.NewGuid(),
                BarcodeValue = "8901725181224",
                IsPrimary = true
            });

            var p3 = new PosErp.Domain.Entities.Catalog.Product
            {
                Id = Guid.NewGuid(),
                ProductCode = "PROD-003",
                Name = "Tata Salt 1kg",
                TamilName = "டாடா உப்பு",
                Description = "Iodized table salt",
                TaxSlabId = taxSlab.Id,
                Mrp = 28.00m,
                SellingPrice = 28.00m,
                PurchasePrice = 22.00m,
                IsWeighable = false,
                IsActive = true
            };
            p3.Barcodes.Add(new PosErp.Domain.Entities.Catalog.Barcode
            {
                Id = Guid.NewGuid(),
                BarcodeValue = "8901058002313",
                IsPrimary = true
            });

            var p4 = new PosErp.Domain.Entities.Catalog.Product
            {
                Id = Guid.NewGuid(),
                ProductCode = "PROD-004",
                Name = "Cadbury Dairy Milk Silk 150g",
                TamilName = "டைரி மில்க் சில்க்",
                Description = "Smooth milk chocolate",
                TaxSlabId = taxSlab.Id,
                Mrp = 170.00m,
                SellingPrice = 170.00m,
                PurchasePrice = 136.00m,
                IsWeighable = false,
                IsActive = true
            };
            p4.Barcodes.Add(new PosErp.Domain.Entities.Catalog.Barcode
            {
                Id = Guid.NewGuid(),
                BarcodeValue = "7622210825988",
                IsPrimary = true
            });

            var p5 = new PosErp.Domain.Entities.Catalog.Product
            {
                Id = Guid.NewGuid(),
                ProductCode = "PROD-005",
                Name = "Surf Excel Easy Wash 1kg",
                TamilName = "சர்ஃப் எக்செல்",
                Description = "Premium detergent powder",
                TaxSlabId = taxSlab.Id,
                Mrp = 140.00m,
                SellingPrice = 140.00m,
                PurchasePrice = 112.00m,
                IsWeighable = false,
                IsActive = true
            };
            p5.Barcodes.Add(new PosErp.Domain.Entities.Catalog.Barcode
            {
                Id = Guid.NewGuid(),
                BarcodeValue = "8901030753448",
                IsPrimary = true
            });

            context.Products.AddRange(p1, p2, p3, p4, p5);
            await context.SaveChangesAsync();
            Console.WriteLine("Database seeded successfully with initial products.");
        }

        // Seed initial stock if empty
        if (!await context.StockLedger.AnyAsync())
        {
            var storeId = Guid.Empty;
            var allProducts = await context.Products.ToListAsync();
            foreach (var product in allProducts)
            {
                context.StockLedger.Add(new PosErp.Domain.Entities.Inventory.StockLedgerEntry
                {
                    StoreId = storeId,
                    ProductId = product.Id,
                    MovementType = "INITIAL_SEED",
                    Quantity = 1000,
                    UnitCost = product.PurchasePrice,
                    RunningBalance = 1000,
                    BusinessDate = DateTime.UtcNow.Date,
                    ReferenceNumber = "SEED-001",
                    CreatedAt = DateTime.UtcNow
                });
            }
            await context.SaveChangesAsync();
            Console.WriteLine("Database seeded successfully with initial stock.");
        }

        // Seed default Terminal if empty
        var terminalId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        if (!await context.Terminals.AnyAsync(t => t.Id == terminalId))
        {
            var terminal = new PosErp.Domain.Entities.Auth.Terminal
            {
                Id = terminalId,
                TerminalCode = "POS-01",
                Name = "Main Terminal",
                IsActive = true
            };
            context.Terminals.Add(terminal);
            await context.SaveChangesAsync();
            Console.WriteLine("Database seeded successfully with default Terminal.");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"An error occurred seeding the DB: {ex.Message}");
        if (ex.InnerException != null) Console.WriteLine($"Inner: {ex.InnerException.Message}");
    }
}

async Task VerifyDatabaseMetadataAsync(ApplicationDbContext context, IServiceProvider services)
{
    var configuration = services.GetRequiredService<IConfiguration>();
    var tenantProvider = services.GetRequiredService<ITenantProvider>();
    
    var deploymentMode = configuration["SystemConfig:DeploymentMode"] ?? "SelfHosted";
    
    string expectedMode;
    Guid expectedTenantId = Guid.Empty;

    if (string.Equals(deploymentMode, "SaaS", StringComparison.OrdinalIgnoreCase))
    {
        // In SaaS mode, the startup checks verify the platform/management database, 
        // request-level checks are handled dynamically.
        return;
    }
    else
    {
        // Self-hosted: read active mode from operation_mode.json
        var connStrProvider = services.GetRequiredService<IConnectionStringProvider>() as PosErp.Infrastructure.Services.ConnectionStringProvider;
        expectedMode = connStrProvider?.GetSelfHostedActiveMode() ?? "LIVE";
    }

    // Verify the database metadata
    var connection = context.Database.GetDbConnection();
    var wasOpen = connection.State == System.Data.ConnectionState.Open;
    if (!wasOpen) await connection.OpenAsync();

    string actualDbName = connection.Database;
    
    // Assert database name matches expected mode suffix (physical safeguard)
    if (string.Equals(expectedMode, "UAT", StringComparison.OrdinalIgnoreCase))
    {
        if (!actualDbName.EndsWith("_uat", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"[FATAL SAFETY ASSERTION FAILED] Connected database '{actualDbName}' does not match expected environment mode 'UAT'. " +
                $"Connection aborted to prevent data corruption.");
        }
    }
    else
    {
        var isLiveDb = actualDbName.EndsWith("_live", StringComparison.OrdinalIgnoreCase);
        var isDevDbFallback = actualDbName.Equals("posdb", StringComparison.OrdinalIgnoreCase) || 
                              actualDbName.Equals("poserp", StringComparison.OrdinalIgnoreCase);
        var isDevelopment = string.Equals(
            Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development", 
            "Development", 
            StringComparison.OrdinalIgnoreCase);

        if (!isLiveDb && (!isDevelopment || !isDevDbFallback))
        {
            throw new InvalidOperationException(
                $"[FATAL SAFETY ASSERTION FAILED] Connected database '{actualDbName}' does not match expected environment mode 'LIVE' (must end with '_live' in Production). " +
                $"Connection aborted to prevent data corruption.");
        }
    }

    // Query database_metadata table if it exists
    bool tableExists = false;
    using (var cmd = connection.CreateCommand())
    {
        cmd.CommandText = "SELECT EXISTS(SELECT 1 FROM information_schema.tables WHERE table_name = 'database_metadata')";
        tableExists = (bool)(await cmd.ExecuteScalarAsync() ?? false);
    }

    if (tableExists)
    {
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SELECT database_name, environment_mode, tenant_id FROM database_metadata LIMIT 1";
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                if (reader.Read())
                {
                    var dbName = reader.GetString(0);
                    var envMode = reader.GetString(1);
                    var tenantId = reader.GetGuid(2);

                    if (!string.Equals(envMode, expectedMode, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException(
                            $"[FATAL SAFETY ASSERTION FAILED] Database metadata mode '{envMode}' does not match expected mode '{expectedMode}'.");
                    }
                    }
                }
            }
        }

        if (!wasOpen) await connection.CloseAsync();
    }
// Startup check: verify pg_dump and pg_restore binaries are available.
// These are required by RefreshUatFromLiveSnapshotAsync (recurring UAT refresh).
// Fails loudly here so the container is rejected at boot rather than discovering
// missing postgresql-client tools only when a UAT refresh is first attempted.
// Ensure postgresql-client (version-matched to Postgres server) is in the Dockerfile.
PosErp.Infrastructure.Services.DatabaseProvisioningService.AssertPgClientToolsAvailable();

app.Run();
