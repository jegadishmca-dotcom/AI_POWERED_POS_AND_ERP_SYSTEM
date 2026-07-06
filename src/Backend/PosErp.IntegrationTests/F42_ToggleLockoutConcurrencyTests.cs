using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Primitives;
using Npgsql;
using PosErp.Application.Interfaces;
using PosErp.Domain.Entities.Auth;
using PosErp.Infrastructure.Persistence;
using PosErp.Infrastructure.Services;
using PosErp.Api.Controllers;
using PosErp.Infrastructure.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace PosErp.IntegrationTests;

[Collection("Database Collection")]
public class F42_ToggleLockoutConcurrencyTests : IDisposable
{
    private readonly ApplicationDbContext _context;

    public F42_ToggleLockoutConcurrencyTests()
    {
        _context = IntegrationTestDbFactory.Build();
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
    public async Task LockoutConcurrencyTest_ShouldIncrementAtomicallyAndLockout()
    {
        // 1. Seed Developer role and user in the integration test database
        var devRole = new Role
        {
            Id = Guid.NewGuid(),
            Name = "Developer",
            Description = "Developer Role for concurrency test"
        };
        _context.Roles.Add(devRole);

        var devUser = new User
        {
            Id = Guid.NewGuid(),
            Username = "concurrency-dev@supermarket.local",
            PasswordHash = "hashed",
            FullName = "Concurrency Developer",
            RoleId = devRole.Id,
            IsActive = true
        };
        _context.Users.Add(devUser);
        await _context.SaveChangesAsync();

        // 2. Fire 10 parallel failed attempts simulating simultaneous requests
        var tasks = new List<Task<(int FailedCount, DateTimeOffset? LockedUntil)>>();
        var connectionString = _context.Database.GetConnectionString();
        Assert.NotNull(connectionString);

        for (int i = 0; i < 10; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                await using var conn = new NpgsqlConnection(connectionString);
                await conn.OpenAsync();
                await using var cmd = conn.CreateCommand();

                cmd.CommandText = @"
                    INSERT INTO toggle_lockout_state (account_id, failed_count, locked_until, updated_at)
                    VALUES (@accountId, 1, NULL, NOW())
                    ON CONFLICT (account_id) DO UPDATE
                    SET failed_count = toggle_lockout_state.failed_count + 1,
                        locked_until = CASE WHEN toggle_lockout_state.failed_count + 1 >= 5 THEN NOW() + interval '15 minutes' ELSE NULL END,
                        updated_at = NOW()
                    RETURNING failed_count, locked_until;";
                
                cmd.Parameters.AddWithValue("@accountId", devUser.Id);

                await using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    var count = reader.GetInt32(0);
                    var lockedTime = reader.IsDBNull(1) ? (DateTimeOffset?)null : reader.GetFieldValue<DateTimeOffset>(1);
                    return (count, lockedTime);
                }
                return (0, null);
            }));
        }

        var results = await Task.WhenAll(tasks);

        // 3. Verify final state in the database
        await using var checkConn = new NpgsqlConnection(connectionString);
        await checkConn.OpenAsync();
        await using var checkCmd = checkConn.CreateCommand();
        checkCmd.CommandText = "SELECT failed_count, locked_until FROM toggle_lockout_state WHERE account_id = @accountId";
        checkCmd.Parameters.AddWithValue("@accountId", devUser.Id);

        await using var checkReader = await checkCmd.ExecuteReaderAsync();
        Assert.True(await checkReader.ReadAsync());
        var finalCount = checkReader.GetInt32(0);
        var finalLock = checkReader.IsDBNull(1) ? (DateTimeOffset?)null : checkReader.GetFieldValue<DateTimeOffset>(1);

        Assert.Equal(10, finalCount);
        Assert.NotNull(finalLock);
        Assert.True(finalLock.Value > DateTimeOffset.UtcNow);

        var lockedResults = results.Where(r => r.LockedUntil.HasValue).ToList();
        Assert.NotEmpty(lockedResults);
    }

    [Fact]
    public async Task LockoutTest_CorrectPasswordDuringLockout_ShouldBeRejected()
    {
        // 1. Seed Developer user with known password hash
        var devRole = new Role
        {
            Id = Guid.NewGuid(),
            Name = "Developer",
            Description = "Developer Role for lockout test"
        };
        _context.Roles.Add(devRole);

        var correctPassword = "Developer@123!";
        var correctPasswordHash = BCrypt.Net.BCrypt.HashPassword(correctPassword);

        var devUser = new User
        {
            Id = Guid.NewGuid(),
            Username = "lockout-dev@supermarket.local",
            PasswordHash = correctPasswordHash,
            FullName = "Lockout Developer",
            RoleId = devRole.Id,
            IsActive = true
        };
        _context.Users.Add(devUser);
        await _context.SaveChangesAsync();

        // Setup controller dependencies
        var host = GetActiveHost();
        var inMemorySettings = new Dictionary<string, string?> {
            {"SystemConfig:DeploymentMode", "SelfHosted"},
            {"ConnectionStrings:DefaultConnection", $"Host={host};Port=5432;Database=posdb_integration_tests;Username=posadmin;Password=pospassword;"}
        };

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var tenantProvider = new TestTenantProvider { TenantId = Guid.Empty };
        var memoryCache = new TestMemoryCache();
        var connProvider = new ConnectionStringProvider(config, tenantProvider, memoryCache);
        var lifetime = new TestHostApplicationLifetime();

        var controller = new EnvironmentToggleController(
            _context,
            connProvider,
            lifetime,
            config,
            tenantProvider,
            memoryCache
        );

        var userClaims = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(new[]
        {
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, devUser.Id.ToString()),
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, devUser.Username)
        }, "TestAuth"));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext { User = userClaims }
        };

        // 2. Trigger lockout (5 failures)
        for (int i = 0; i < 5; i++)
        {
            var res = await controller.ToggleEnvironment(new ToggleEnvironmentRequest
            {
                DeveloperPassword = "WrongPassword!",
                TargetMode = "UAT"
            });
            if (i < 4)
            {
                Assert.IsType<UnauthorizedObjectResult>(res);
            }
            else
            {
                var objectResult = Assert.IsType<ObjectResult>(res);
                Assert.Equal(429, objectResult.StatusCode);
            }
        }

        // 3. Verify lockout is active in DB
        await using (var conn = new NpgsqlConnection(_context.Database.GetConnectionString()))
        {
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT locked_until FROM toggle_lockout_state WHERE account_id = @accountId";
            cmd.Parameters.AddWithValue("@accountId", devUser.Id);
            var lockedUntilValue = await cmd.ExecuteScalarAsync();
            Assert.NotNull(lockedUntilValue);
            Assert.True((DateTime)lockedUntilValue > DateTime.UtcNow);
        }

        // 4. Attempt toggle with CORRECT password while lockout is active -> Should be rejected with 429
        var lockoutRes = await controller.ToggleEnvironment(new ToggleEnvironmentRequest
        {
            DeveloperPassword = correctPassword,
            TargetMode = "UAT"
        });

        var lockoutObjResult = Assert.IsType<ObjectResult>(lockoutRes);
        Assert.Equal(429, lockoutObjResult.StatusCode);
    }

    [Fact]
    public async Task ToggleEnvironment_SaaS_ShouldInvalidateTokensForTargetTenantOnly()
    {
        var host = GetActiveHost();
        var adminConnStr = $"Host={host};Port=5432;Database=postgres;Username=posadmin;Password=pospassword;";
        
        var tenantAId = Guid.NewGuid();
        var tenantBId = Guid.NewGuid();

        // 1. Recreate databases representing Tenant A and Tenant B
        await using (var conn = new NpgsqlConnection(adminConnStr))
        {
            await conn.OpenAsync();
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "DROP DATABASE IF EXISTS tenant_a_live; DROP DATABASE IF EXISTS tenant_b_live;";
                await cmd.ExecuteNonQueryAsync();
            }
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "CREATE DATABASE tenant_a_live; CREATE DATABASE tenant_b_live;";
                await cmd.ExecuteNonQueryAsync();
            }
        }

        // 2. Initialize schemas and seed roles/users/tokens using EF Core
        var devRoleId = Guid.NewGuid();
        var devUserId = Guid.NewGuid();
        var hashedDevPassword = BCrypt.Net.BCrypt.HashPassword("DevPassword!");

        // Populate Database A
        var optionsA = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql($"Host={host};Port=5432;Database=tenant_a_live;Username=posadmin;Password=pospassword;")
            .Options;
        
        using (var dbContextA = new ApplicationDbContext(optionsA))
        {
            await dbContextA.Database.EnsureCreatedAsync();

            var devRole = new Role { Id = devRoleId, Name = "Developer" };
            var devUser = new User
            {
                Id = devUserId,
                Username = "developer@supermarket.local",
                PasswordHash = hashedDevPassword,
                FullName = "Dev A",
                RoleId = devRoleId,
                IsActive = true
            };
            var token = new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = devUserId,
                Token = "token-a",
                TokenFamily = "fam-a",
                ExpiresAt = DateTime.UtcNow.AddDays(1),
                DeviceId = "dev-a"
            };

            dbContextA.Roles.Add(devRole);
            dbContextA.Users.Add(devUser);
            dbContextA.RefreshTokens.Add(token);
            await dbContextA.SaveChangesAsync();
        }

        // Populate Database B
        var optionsB = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql($"Host={host};Port=5432;Database=tenant_b_live;Username=posadmin;Password=pospassword;")
            .Options;

        using (var dbContextB = new ApplicationDbContext(optionsB))
        {
            await dbContextB.Database.EnsureCreatedAsync();

            var devRoleB = new Role { Id = devRoleId, Name = "Developer" };
            var devUserB = new User
            {
                Id = devUserId,
                Username = "developer@supermarket.local",
                PasswordHash = hashedDevPassword,
                FullName = "Dev B",
                RoleId = devRoleId,
                IsActive = true
            };
            var tokenB = new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = devUserId,
                Token = "token-b",
                TokenFamily = "fam-b",
                ExpiresAt = DateTime.UtcNow.AddDays(1),
                DeviceId = "dev-b"
            };

            dbContextB.Roles.Add(devRoleB);
            dbContextB.Users.Add(devUserB);
            dbContextB.RefreshTokens.Add(tokenB);
            await dbContextB.SaveChangesAsync();
        }

        // Setup platform-level tenant_environments
        await using (var conn = new NpgsqlConnection(adminConnStr))
        {
            await conn.OpenAsync();
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS tenant_environments (
                        tenant_id UUID PRIMARY KEY,
                        active_mode VARCHAR(50) NOT NULL DEFAULT 'LIVE',
                        live_connection_string TEXT NOT NULL,
                        uat_connection_string TEXT NOT NULL,
                        token_version INT NOT NULL DEFAULT 1,
                        updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
                    );
                    ALTER TABLE tenant_environments ADD COLUMN IF NOT EXISTS token_version INT NOT NULL DEFAULT 1;
                    INSERT INTO tenant_environments (tenant_id, active_mode, live_connection_string, uat_connection_string)
                    VALUES (@tenantAId, 'LIVE', @connA, @connA) ON CONFLICT (tenant_id) DO NOTHING;
                    INSERT INTO tenant_environments (tenant_id, active_mode, live_connection_string, uat_connection_string)
                    VALUES (@tenantBId, 'LIVE', @connB, @connB) ON CONFLICT (tenant_id) DO NOTHING;";
                cmd.Parameters.AddWithValue("@tenantAId", tenantAId);
                cmd.Parameters.AddWithValue("@tenantBId", tenantBId);
                cmd.Parameters.AddWithValue("@connA", $"Host={host};Port=5432;Database=tenant_a_live;Username=posadmin;Password=pospassword;");
                cmd.Parameters.AddWithValue("@connB", $"Host={host};Port=5432;Database=tenant_b_live;Username=posadmin;Password=pospassword;");
                await cmd.ExecuteNonQueryAsync();
            }
        }

        // Create controller context for testing target switch
        var dbContextForController = new ApplicationDbContext(optionsA);

        var inMemorySettings = new Dictionary<string, string?> {
            {"SystemConfig:DeploymentMode", "SaaS"},
            {"ConnectionStrings:DefaultConnection", adminConnStr}
        };

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var tenantProvider = new TestTenantProvider { TenantId = tenantAId };
        var memoryCache = new TestMemoryCache();
        
        // Seed connection string cache for Tenant A
        memoryCache.Cache[$"conn_{tenantAId}"] = $"Host={host};Port=5432;Database=tenant_a_live;Username=posadmin;Password=pospassword;";
        
        var connProvider = new ConnectionStringProvider(config, tenantProvider, memoryCache);
        var lifetime = new TestHostApplicationLifetime();

        var controller = new EnvironmentToggleController(
            dbContextForController,
            connProvider,
            lifetime,
            config,
            tenantProvider,
            memoryCache
        );

        // Claims principal
        var userClaims = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(new[]
        {
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, devUserId.ToString()),
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, "developer@supermarket.local")
        }, "TestAuth"));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext { User = userClaims }
        };

        // 3. Trigger switch to UAT for Tenant A
        var toggleRes = await controller.ToggleEnvironment(new ToggleEnvironmentRequest
        {
            DeveloperPassword = "DevPassword!",
            TargetMode = "UAT"
        });

        Assert.IsType<OkObjectResult>(toggleRes);

        // 4. Assert: Tenant A's connection is evicted from cache
        Assert.False(memoryCache.Cache.ContainsKey($"conn_{tenantAId}"));

        // 5. Assert: Tenant A's refresh tokens table is empty
        await using (var conn = new NpgsqlConnection($"Host={host};Port=5432;Database=tenant_a_live;Username=posadmin;Password=pospassword;"))
        {
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM refresh_tokens";
            var count = (long)(await cmd.ExecuteScalarAsync() ?? 0L);
            Assert.Equal(0, count);
        }

        // 6. Assert: Tenant B's refresh tokens are completely untouched
        await using (var conn = new NpgsqlConnection($"Host={host};Port=5432;Database=tenant_b_live;Username=posadmin;Password=pospassword;"))
        {
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM refresh_tokens";
            var count = (long)(await cmd.ExecuteScalarAsync() ?? 0L);
            Assert.Equal(1, count); // Still has 1 token
        }
    }

    [Fact]
    public async Task TokenVersion_Validation_ShouldRejectOldTokensAfterToggle()
    {
        var host = GetActiveHost();
        var adminConnStr = $"Host={host};Port=5432;Database=postgres;Username=posadmin;Password=pospassword;";
        
        var tenantId = Guid.NewGuid();

        // 1. Set up databases
        await using (var conn = new NpgsqlConnection(adminConnStr))
        {
            await conn.OpenAsync();
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "DROP DATABASE IF EXISTS tenant_validation_live; CREATE DATABASE tenant_validation_live;";
                await cmd.ExecuteNonQueryAsync();
            }
        }

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql($"Host={host};Port=5432;Database=tenant_validation_live;Username=posadmin;Password=pospassword;")
            .Options;
        
        using (var dbContext = new ApplicationDbContext(options))
        {
            await dbContext.Database.EnsureCreatedAsync();
        }

        // Setup platform-level tenant_environments
        await using (var conn = new NpgsqlConnection(adminConnStr))
        {
            await conn.OpenAsync();
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS tenant_environments (
                        tenant_id UUID PRIMARY KEY,
                        active_mode VARCHAR(50) NOT NULL DEFAULT 'LIVE',
                        live_connection_string TEXT NOT NULL,
                        uat_connection_string TEXT NOT NULL,
                        token_version INT NOT NULL DEFAULT 1,
                        updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
                    );
                    ALTER TABLE tenant_environments ADD COLUMN IF NOT EXISTS token_version INT NOT NULL DEFAULT 1;
                    INSERT INTO tenant_environments (tenant_id, active_mode, live_connection_string, uat_connection_string, token_version)
                    VALUES (@tenantId, 'LIVE', @conn, @conn, 1)
                    ON CONFLICT (tenant_id) DO UPDATE SET token_version = 1;";
                cmd.Parameters.AddWithValue("@tenantId", tenantId);
                cmd.Parameters.AddWithValue("@conn", $"Host={host};Port=5432;Database=tenant_validation_live;Username=posadmin;Password=pospassword;");
                await cmd.ExecuteNonQueryAsync();
            }
        }

        // 2. Generate token for Tenant A when token_version is 1
        var inMemorySettings = new Dictionary<string, string?> {
            {"SystemConfig:DeploymentMode", "SaaS"},
            {"ConnectionStrings:DefaultConnection", adminConnStr},
            {"JWT__Secret", "TestSecretKey_ReplaceWithEnvVarInProduction_MinLength64Chars1234567890ABCD"}
        };

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var tenantProvider = new TestTenantProvider { TenantId = tenantId };
        var memoryCache = new TestMemoryCache();
        var connProvider = new ConnectionStringProvider(config, tenantProvider, memoryCache);

        var tokenGenerator = new JwtTokenGenerator(config, connProvider, tenantProvider);

        var devUser = new User
        {
            Id = Guid.NewGuid(),
            Username = "dev@supermarket.local",
            FullName = "Dev User"
        };
        var token = tokenGenerator.GenerateToken(devUser, "Developer");

        // 3. Verify that the generated token contains the claim token_version = 1
        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);
        var versionClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "token_version")?.Value;
        Assert.Equal("1", versionClaim);

        // 4. Simulate a toggle: Increment token version in platform DB to 2
        await using (var conn = new NpgsqlConnection(adminConnStr))
        {
            await conn.OpenAsync();
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "UPDATE tenant_environments SET token_version = token_version + 1 WHERE tenant_id = @tenantId";
                cmd.Parameters.AddWithValue("@tenantId", tenantId);
                await cmd.ExecuteNonQueryAsync();
            }
        }

        // 5. Run the validation logic (directly simulating OnTokenValidated middleware execution)
        var principal = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(new[]
        {
            new System.Security.Claims.Claim("token_version", versionClaim!)
        }));

        var httpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        
        var serviceProviderMock = new Microsoft.Extensions.DependencyInjection.ServiceCollection()
            .AddSingleton<IConfiguration>(config)
            .AddSingleton<IConnectionStringProvider>(connProvider)
            .AddSingleton<ITenantProvider>(tenantProvider)
            .BuildServiceProvider();
            
        httpContext.RequestServices = serviceProviderMock;

        var authScheme = new Microsoft.AspNetCore.Authentication.AuthenticationScheme("Bearer", "Bearer", typeof(Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerHandler));
        var jwtBearerOptions = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerOptions();
        
        var validatedContext = new Microsoft.AspNetCore.Authentication.JwtBearer.TokenValidatedContext(
            httpContext,
            authScheme,
            jwtBearerOptions)
        {
            Principal = principal
        };

        var validationEvent = new Func<Microsoft.AspNetCore.Authentication.JwtBearer.TokenValidatedContext, Task>(async ctx =>
        {
            var cfg = ctx.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
            var cp = ctx.HttpContext.RequestServices.GetRequiredService<IConnectionStringProvider>();
            var tp = ctx.HttpContext.RequestServices.GetRequiredService<ITenantProvider>();

            var tvClaim = ctx.Principal?.FindFirst("token_version")?.Value;
            if (string.IsNullOrEmpty(tvClaim))
            {
                ctx.Fail("Missing token version claim.");
                return;
            }

            var depMode = cfg["SystemConfig:DeploymentMode"] ?? "SelfHosted";
            int currentVer = 1;

            if (string.Equals(depMode, "SaaS", StringComparison.OrdinalIgnoreCase))
            {
                var platConn = cfg.GetConnectionString("DefaultConnection") 
                    ?? "Host=localhost;Database=poserp;Username=postgres;Password=postgres";
                
                await using var c = new Npgsql.NpgsqlConnection(platConn);
                await c.OpenAsync();
                await using var cmd = c.CreateCommand();
                cmd.CommandText = "SELECT token_version FROM tenant_environments WHERE tenant_id = @p0";
                var p = cmd.CreateParameter();
                p.ParameterName = "@p0";
                p.Value = tp.TenantId;
                cmd.Parameters.Add(p);
                var res = await cmd.ExecuteScalarAsync();
                if (res != null && res != DBNull.Value)
                {
                    currentVer = Convert.ToInt32(res);
                }
            }
            else
            {
                var cpImpl = cp as PosErp.Infrastructure.Services.ConnectionStringProvider;
                if (cpImpl != null)
                {
                    currentVer = cpImpl.GetSelfHostedTokenVersion();
                }
            }

            if (tvClaim != currentVer.ToString())
            {
                ctx.Fail("Token version mismatch. Session invalidated due to environment toggle.");
            }
        });

        await validationEvent(validatedContext);

        // 6. Assert: Validation failed
        Assert.True(validatedContext.Result?.Failure != null);
        Assert.Contains("Token version mismatch", validatedContext.Result?.Failure?.Message);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    // ─── Test stubs ──────────────────────────────────────────────────────────

    private class TestTenantProvider : ITenantProvider
    {
        public Guid TenantId { get; set; } = Guid.Empty;
        public void SetTenantId(Guid tenantId) { TenantId = tenantId; }
    }

    private class TestHostApplicationLifetime : IHostApplicationLifetime
    {
        public CancellationToken ApplicationStarted => default;
        public CancellationToken ApplicationStopping => default;
        public CancellationToken ApplicationStopped => default;
        public void StopApplication() { }
    }

    private class TestMemoryCache : IMemoryCache
    {
        public Dictionary<object, object> Cache = new();
        public ICacheEntry CreateEntry(object key) => new TestCacheEntry(key, this);
        public void Dispose() { }
        public void Remove(object key) => Cache.Remove(key);
        public bool TryGetValue(object key, out object? value) => Cache.TryGetValue(key, out value);
    }

    private class TestCacheEntry : ICacheEntry
    {
        private readonly object _key;
        private readonly TestMemoryCache _cache;
        public TestCacheEntry(object key, TestMemoryCache cache) { _key = key; _cache = cache; }
        public object Key => _key;
        public object? Value { get; set; }
        public DateTimeOffset? AbsoluteExpiration { get; set; }
        public TimeSpan? AbsoluteExpirationRelativeToNow { get; set; }
        public TimeSpan? SlidingExpiration { get; set; }
        public IList<IChangeToken> ExpirationTokens => new List<IChangeToken>();
        public IList<PostEvictionCallbackRegistration> PostEvictionCallbacks => new List<PostEvictionCallbackRegistration>();
        public long? Size { get; set; }
        public CacheItemPriority Priority { get; set; } = CacheItemPriority.Normal;
        public void Dispose() { _cache.Cache[_key] = Value!; }
    }
}
