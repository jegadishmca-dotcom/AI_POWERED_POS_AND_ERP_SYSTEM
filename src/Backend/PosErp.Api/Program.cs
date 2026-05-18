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

        // Retrieve or insert 'Owner' and 'Cashier' roles dynamically to satisfy FK constraints 
        // without violating UNIQUE name constraints if roles already exist under other IDs.
        Guid ownerRoleId = Guid.Empty;
        Guid cashierRoleId = Guid.Empty;

        try
        {
            await context.Database.OpenConnectionAsync();
            using (var command = context.Database.GetDbConnection().CreateCommand())
            {
                // Query Owner Role
                command.CommandText = "SELECT id FROM roles WHERE name = 'Owner' OR name = 'owner' LIMIT 1;";
                var result = await command.ExecuteScalarAsync();
                if (result != null && result != DBNull.Value)
                {
                    ownerRoleId = (Guid)result;
                }
                else
                {
                    ownerRoleId = Guid.Parse("00000000-0000-0000-0000-000000000001");
                    command.CommandText = "INSERT INTO roles (id, name, description, is_system) VALUES ('00000000-0000-0000-0000-000000000001', 'Owner', 'System Owner / Administrator', true);";
                    await command.ExecuteNonQueryAsync();
                }

                // Query Cashier Role
                command.CommandText = "SELECT id FROM roles WHERE name = 'Cashier' OR name = 'cashier' LIMIT 1;";
                result = await command.ExecuteScalarAsync();
                if (result != null && result != DBNull.Value)
                {
                    cashierRoleId = (Guid)result;
                }
                else
                {
                    cashierRoleId = Guid.Parse("00000000-0000-0000-0000-000000000002");
                    command.CommandText = "INSERT INTO roles (id, name, description, is_system) VALUES ('00000000-0000-0000-0000-000000000002', 'Cashier', 'POS Cashier Clerk', true);";
                    await command.ExecuteNonQueryAsync();
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error seeding or fetching roles: {ex.Message}");
            // Fallback to static IDs if query fails
            ownerRoleId = Guid.Parse("00000000-0000-0000-0000-000000000001");
            cashierRoleId = Guid.Parse("00000000-0000-0000-0000-000000000002");
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
                RoleId = ownerRoleId,
                IsActive = true
            };
            context.Users.Add(adminUser);
            
            // Seed Cashier User
            var cashierUser = new PosErp.Domain.Entities.Auth.User
            {
                Username = "cashier@supermarket.local",
                PasswordHash = passwordHasher.HashPassword("Cashier@123!"),
                FullName = "Terminal Cashier 01",
                RoleId = cashierRoleId,
                IsActive = true
            };
            context.Users.Add(cashierUser);
            
            await context.SaveChangesAsync();
            Console.WriteLine("Database seeded successfully with default users.");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"An error occurred seeding the DB: {ex.Message}");
    }
}

app.Run();
