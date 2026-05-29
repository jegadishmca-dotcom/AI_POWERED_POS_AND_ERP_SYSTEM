using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PosErp.Application.Interfaces;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Infrastructure.Jobs;

public class StockPositionRefreshService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;

    public StockPositionRefreshService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Console.WriteLine("[StockPositionRefreshService] Background worker started.");
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
                var db = (DbContext)context;

                // Try concurrent refresh (non-blocking)
                try
                {
                    await db.Database.ExecuteSqlRawAsync("REFRESH MATERIALIZED VIEW CONCURRENTLY mv_current_stock", stoppingToken);
                }
                catch
                {
                    // Fallback to standard refresh if concurrent fails (e.g. index/data lock state)
                    await db.Database.ExecuteSqlRawAsync("REFRESH MATERIALIZED VIEW mv_current_stock", stoppingToken);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StockPositionRefreshService] [ERROR] Failed to refresh materialized view: {ex.Message}");
            }

            // Wait 5 minutes before refreshing again
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }
}
