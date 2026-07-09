using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PosErp.Application.Interfaces;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Infrastructure.Jobs
{
    public class IdempotentRequestCleanupService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;

        public IdempotentRequestCleanupService(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Console.WriteLine("[IdempotentRequestCleanupService] Background worker started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

                    var expiryLimit = DateTime.UtcNow.AddHours(-24);
                    
                    // High-performance batch delete of requests older than 24 hours
                    int deletedCount = await context.IdempotentRequests
                        .Where(r => r.CreatedAt < expiryLimit)
                        .ExecuteDeleteAsync(stoppingToken);

                    if (deletedCount > 0)
                    {
                        Console.WriteLine($"[IdempotentRequestCleanupService] Cleaned up {deletedCount} expired idempotency records.");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[IdempotentRequestCleanupService] [ERROR] Failed to clean up idempotent requests: {ex.Message}");
                }

                // Run once every 24 hours
                await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
            }
        }
    }
}
