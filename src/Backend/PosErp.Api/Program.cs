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
using PosErp.Api.Middlewares;
using PosErp.Application.Interfaces;
using PosErp.Infrastructure.Persistence;
using PosErp.Infrastructure.Authentication;
using PosErp.Infrastructure.Identity;
using PosErp.Infrastructure.Printing;
using PosErp.Application.Features.Inventory.Services;
using PosErp.Application.Features.Offers.Services;
using PosErp.Application.Features.Crm.Services;
using PosErp.Application.Features.Finance.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add Database Context (PostgreSQL via PgBouncer / direct connection string)
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IApplicationDbContext>(provider => 
    provider.GetRequiredService<ApplicationDbContext>());

// Add Controllers and Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Health Checks
builder.Services.AddHealthChecks();

// Redis Configuration
string redisConnectionString = builder.Configuration.GetSection("Redis:ConnectionString").Value ?? "localhost:6379";
builder.Services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(redisConnectionString));
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = redisConnectionString;
    options.InstanceName = "AppleSupermarket_";
});

// Register MediatR
builder.Services.AddMediatR(cfg => {
    cfg.RegisterServicesFromAssembly(typeof(IApplicationDbContext).Assembly);
});

// Register Infrastructure Services
builder.Services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<IPrintService, EscPosPrintService>();

// JWT Authentication Configuration
var secret = "SuperSecretKeyForDevelopmentPurposesOnlyReplaceInProdSuperSecretKeyForDevelopmentPurposesOnlyReplaceInProd";
var key = Encoding.UTF8.GetBytes(secret);

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
});

// Register Application Layer Services
builder.Services.AddScoped<IStockLedgerService, StockLedgerService>();
builder.Services.AddScoped<IProductBatchService, ProductBatchService>();
builder.Services.AddScoped<IOfferEngine, OfferEngine>();
builder.Services.AddScoped<IWalletService, WalletService>();
builder.Services.AddScoped<ILoyaltyService, LoyaltyService>();
builder.Services.AddScoped<ICustomerTierService, CustomerTierService>();
builder.Services.AddScoped<IFinancialPostingService, FinancialPostingService>();
builder.Services.AddScoped<IFinancialReportingService, FinancialReportingService>();
builder.Services.AddScoped<IEInvoiceService, EInvoiceService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment() || app.Environment.IsProduction())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseMiddleware<RateLimitingMiddleware>();

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

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

        // Scan and execute all pending raw SQL migrations in alphabetical order
        var migrationsDir = Path.Combine(AppContext.BaseDirectory, "Persistence", "Migrations");
        if (Directory.Exists(migrationsDir))
        {
            var sqlFiles = Directory.GetFiles(migrationsDir, "*.sql")
                                    .OrderBy(f => Path.GetFileName(f))
                                    .ToList();

            foreach (var sqlFile in sqlFiles)
            {
                var filename = Path.GetFileName(sqlFile);
                bool exists = false;

                var connection = context.Database.GetDbConnection();
                var wasOpen = connection.State == System.Data.ConnectionState.Open;
                if (!wasOpen) await connection.OpenAsync();

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

                if (!wasOpen) await connection.CloseAsync();

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

        // GST Slab master + HsnMasterIndia2026 seeding
        await PosErp.Api.Infrastructure.GstMasterSeeder.SeedAsync(context);

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
    }
    catch (Exception ex)
    {
        Console.WriteLine($"An error occurred seeding the DB: {ex.Message}");
        if (ex.InnerException != null) Console.WriteLine($"Inner: {ex.InnerException.Message}");
    }
}

app.Run();
