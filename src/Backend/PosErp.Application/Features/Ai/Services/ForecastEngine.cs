using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PosErp.Domain.Entities.Finance;
using PosErp.Application.Interfaces;

namespace PosErp.Application.Features.Ai.Services;

public class ForecastEngine : IForecastEngine
{
    private readonly IApplicationDbContext _context;

    public ForecastEngine(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<AiDemandForecast>> GenerateDemandForecastsAsync(Guid? storeId, DateTime businessDate, CancellationToken cancellationToken)
    {
        var forecasts = new List<AiDemandForecast>();

        // Heuristic: Extrapolate based on simple 30-day velocity with dummy seasonal multiplier
        var products = await _context.Products.Take(100).ToListAsync(cancellationToken);

        foreach (var product in products)
        {
            // Calculate base 30-day velocity
            var invoicesQuery = _context.InvoiceItems
                .Include(i => i.Invoice)
                .Where(i => i.ProductId == product.Id && i.Invoice.Status == "COMPLETED" && i.Invoice.BusinessDate >= businessDate.AddDays(-30));
                
            if (storeId.HasValue) invoicesQuery = invoicesQuery.Where(i => i.Invoice.StoreId == storeId);

            decimal last30DaysSold = await invoicesQuery.SumAsync(i => i.Quantity, cancellationToken);
            decimal dailyVelocity = last30DaysSold / 30m;

            // Apply dummy seasonality (e.g., 10% uplift next month)
            decimal seasonalMultiplier = 1.1m; 
            decimal forecast30Days = dailyVelocity * 30m * seasonalMultiplier;

            forecasts.Add(new AiDemandForecast
            {
                ForecastType = "PRODUCT",
                ReferenceId = product.Id,
                ForecastDate = businessDate,
                ForecastHorizonDays = 30,
                ForecastMethod = "MovingAverageWithSeasonality",
                ForecastQuantity = Math.Round(forecast30Days, 0),
                ConfidenceLevel = 80.0m, // Baseline heuristic confidence
                ModelVersion = "Heuristic-v1.0",
                CreatedAt = DateTime.UtcNow
            });
        }

        return forecasts;
    }

    public async Task<ForecastAccuracySnapshot> EvaluateForecastAccuracyAsync(DateTime businessDate, CancellationToken cancellationToken)
    {
        // Mock accuracy evaluation logic
        // Ideally, we compare past AiDemandForecast (where ForecastDate + Horizon == today) against actual sales.
        
        var snapshot = new ForecastAccuracySnapshot
        {
            SnapshotDate = businessDate,
            ModelVersion = "Heuristic-v1.0",
            MeanAbsolutePercentageError = 12.5m, // 12.5% MAPE
            MeanAbsoluteError = 15.0m, // 15 units off on average
            RootMeanSquareError = 18.2m, // Penalizes larger errors
            CreatedAt = DateTime.UtcNow
        };

        return await Task.FromResult(snapshot);
    }
}
