using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PosErp.Application.Interfaces;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Infrastructure.Jobs;

public class RefreshTokenCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;

    public RefreshTokenCleanupService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Console.WriteLine("[RefreshTokenCleanupService] Background worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

                var expiryLimit = DateTime.UtcNow.AddDays(-30);
                
                // Delete revoked tokens or tokens expired more than 30 days ago
                int deletedCount = await context.RefreshTokens
                    .Where(t => t.IsRevoked || t.ExpiresAt < expiryLimit)
                    .ExecuteDeleteAsync(stoppingToken);

                if (deletedCount > 0)
                {
                    Console.WriteLine($"[RefreshTokenCleanupService] Cleaned up {deletedCount} expired/revoked refresh tokens.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RefreshTokenCleanupService] [ERROR] Failed to clean up refresh tokens: {ex.Message}");
            }

            // Run once every 24 hours
            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
        }
    }
}
