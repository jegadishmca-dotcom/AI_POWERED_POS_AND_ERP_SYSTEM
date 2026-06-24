using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PosErp.Application.Features.Ai.Jobs;
using PosErp.Application.Features.Ai.Services;
using PosErp.Application.Interfaces;
using PosErp.Infrastructure.Persistence;
using PosErp.Domain.Entities.Catalog;
using PosErp.Domain.Entities.Crm;
using PosErp.Domain.Entities.Auth;
using PosErp.Domain.Entities.Finance;

namespace AiJobPerfTest;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Setting up AI Job Perf Test...");
        var services = new ServiceCollection();

        services.AddLogging(c => c.AddConsole());

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase("PerfTestDb"));

        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<IInsightEngine, InsightEngine>();
        services.AddScoped<IForecastEngine, ForecastEngine>();
        services.AddScoped<IRecommendationEngine, RecommendationEngine>();
        services.AddScoped<IAiBackgroundJobs, AiBackgroundJobs>();

        var provider = services.BuildServiceProvider();

        using (var scope = provider.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await context.Database.EnsureCreatedAsync();

            Console.WriteLine("Seeding data (50k products, 10k customers, 100 stores)...");
            
            // To prevent massive memory spikes and slow seeding, we'll do smaller batches 
            // but enough to show the job runs quickly. We'll do exactly what the user asked for testing scaling.
            for (int i = 0; i < 100; i++)
            {
                context.Stores.Add(new Store { Id = Guid.NewGuid(), StoreName = $"Store {i}" });
            }
            await context.SaveChangesAsync();
            
            var storeIds = await context.Stores.Select(s => s.Id).ToListAsync();

            // Seed 10k customers
            var customers = new List<Customer>();
            for(int i=0; i<10000; i++)
            {
                customers.Add(new Customer { Id = Guid.NewGuid(), Name = $"Customer {i}", RunningLoyaltyPoints = i % 1000 });
            }
            context.Customers.AddRange(customers);
            await context.SaveChangesAsync();

            // Seed 50k products
            for(int batch = 0; batch < 50; batch++)
            {
                var products = new List<Product>();
                for(int i=0; i<1000; i++)
                {
                    products.Add(new Product { Id = Guid.NewGuid(), Name = $"Product {batch}_{i}", ProductCode = $"SKU_{batch}_{i}" });
                }
                context.Products.AddRange(products);
                await context.SaveChangesAsync();
            }

            Console.WriteLine("Data seeding complete.");
            
            var jobs = scope.ServiceProvider.GetRequiredService<IAiBackgroundJobs>();

            async Task MeasureJob(string name, Func<Task> jobAction)
            {
                Console.WriteLine($"\nRunning {name}...");
                var sw = Stopwatch.StartNew();
                var memBefore = GC.GetTotalMemory(true);
                
                await jobAction();
                
                sw.Stop();
                var memAfter = GC.GetTotalMemory(true);
                var mbUsed = (memAfter - memBefore) / (1024.0 * 1024.0);
                Console.WriteLine($"[RESULTS] {name}: {sw.ElapsedMilliseconds} ms, {mbUsed:F2} MB memory diff");
            }

            await MeasureJob("InsightGenerationJob", () => jobs.ExecuteInsightGenerationJobAsync(CancellationToken.None));
            await MeasureJob("ForecastGenerationJob", () => jobs.ExecuteForecastGenerationJobAsync(CancellationToken.None));
            await MeasureJob("CustomerIntelligenceJob", () => jobs.ExecuteCustomerIntelligenceJobAsync(CancellationToken.None));
            await MeasureJob("ExecutiveSnapshotJob", () => jobs.ExecuteExecutiveSnapshotJobAsync(CancellationToken.None));
            await MeasureJob("ForecastAccuracyJob", () => jobs.ExecuteForecastAccuracyJobAsync(CancellationToken.None));
            await MeasureJob("AlertGenerationJob", () => jobs.ExecuteAlertGenerationJobAsync(CancellationToken.None));
            
            Console.WriteLine("\nAll jobs completed. Exiting.");
        }
    }
}
