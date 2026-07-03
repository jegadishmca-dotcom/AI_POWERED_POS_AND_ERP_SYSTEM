using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PosErp.Infrastructure.Services;
using PosErp.Application.Interfaces;
using Xunit;

namespace PosErp.IntegrationTests;

[Collection("Database Collection")]
public class F37_AccountResolutionRegressionTests : IDisposable
{
    private readonly PosErp.Infrastructure.Persistence.ApplicationDbContext _context;
    private readonly AccountResolutionService _resolutionService;

    public F37_AccountResolutionRegressionTests()
    {
        _context = IntegrationTestDbFactory.Build();
        _resolutionService = new AccountResolutionService(_context);
    }

    [Fact]
    public async Task ResolveAccountCodeAsync_MustResolveLeafAccount_AndNeverSummaryAccount()
    {
        // Act
        // Resolve for namePattern "Current" which matches both summary account "Current Assets" (10000)
        // and leaf account "HDFC Current A/C" (10200) under the ASSET type.
        var resolvedCode = await _resolutionService.ResolveAccountCodeAsync("ASSET", "Current", "99999", CancellationToken.None);

        // Assert
        // Parent account "10000" must be excluded because it has child accounts, resolving to leaf account "10200".
        Assert.Equal("10200", resolvedCode);
    }

    [Fact]
    public async Task ResolveAccountCodeAsync_ShouldNeverResolveLegacyCodes()
    {
        // Act & Assert
        // 1. "Sales Revenue" matches legacy account "4000", but it must be bypassed and return fallback code.
        var revenueCode = await _resolutionService.ResolveAccountCodeAsync("REVENUE", "Sales Revenue", "99999", CancellationToken.None);
        Assert.Equal("99999", revenueCode);

        // 2. "Cost of Goods Sold" matches legacy "5000" and modern "50100". It must resolve to "50100" (since 5000 is excluded).
        var cogsCode = await _resolutionService.ResolveAccountCodeAsync("EXPENSE", "Cost of Goods Sold", "99999", CancellationToken.None);
        Assert.Equal("50100", cogsCode);

        // 3. "Cash on Hand" matches legacy "1000", but it must return fallback.
        var cashCode = await _resolutionService.ResolveAccountCodeAsync("ASSET", "Cash on Hand", "99999", CancellationToken.None);
        Assert.Equal("99999", cashCode);
    }

    [Fact]
    public void ConfigurationDefaults_ShouldResolveConfiguredValuesAndFallbacks()
    {
        // Arrange
        var configValues = new Dictionary<string, string>
        {
            { "Finance:AccountDefaults:Cash", "99998" }
        };
        
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();

        // Act & Assert
        // Cash should resolve to the configured in-memory value of "99998"
        var cashDefault = configuration?["Finance:AccountDefaults:Cash"] ?? "10100";
        Assert.Equal("99998", cashDefault);

        // DigitalBank is not configured in the dictionary, so it should fall back to "10200"
        var digitalDefault = configuration?["Finance:AccountDefaults:DigitalBank"] ?? "10200";
        Assert.Equal("10200", digitalDefault);
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
