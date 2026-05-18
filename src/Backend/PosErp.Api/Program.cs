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
                    await context.Database.ExecuteSqlRawAsync(sqlContent);

                    await context.Database.ExecuteSqlRawAsync(
                        "INSERT INTO migration_history (migration_name) VALUES ({0})", filename);
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
        
        if (!await context.Users.AnyAsync())
        {
            var passwordHasher = services.GetRequiredService<IPasswordHasher>();
            
            // Seed Admin User
            var adminUser = new PosErp.Domain.Entities.Auth.User
            {
                Username = "admin@supermarket.local",
                PasswordHash = passwordHasher.HashPassword("Admin@123!"),
                FullName = "System Administrator",
                RoleId = ownerRole.Id,
                IsActive = true
            };
            context.Users.Add(adminUser);
            
            // Seed Cashier User
            var cashierUser = new PosErp.Domain.Entities.Auth.User
            {
                Username = "cashier@supermarket.local",
                PasswordHash = passwordHasher.HashPassword("Cashier@123!"),
                FullName = "Terminal Cashier 01",
                RoleId = cashierRole.Id,
                IsActive = true
            };
            context.Users.Add(cashierUser);
            
            await context.SaveChangesAsync();
            Console.WriteLine("Database seeded successfully with default users.");
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
