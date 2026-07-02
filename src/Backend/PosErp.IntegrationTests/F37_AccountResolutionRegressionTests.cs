using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PosErp.Infrastructure.Services;
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

    public void Dispose()
    {
        _context.Dispose();
    }
}
