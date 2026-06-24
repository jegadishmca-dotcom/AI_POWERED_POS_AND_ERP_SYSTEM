using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PosErp.Domain.Entities.Finance;

namespace PosErp.Application.Features.Ai.Services;

/// <summary>
/// Abstraction for the AI Demand Forecasting Engine.
/// Extrapolates historical data into seasonal and product-specific future demand.
/// </summary>
public interface IForecastEngine
{
    /// <summary>
    /// Generates demand forecasts for products based on sales velocity and seasonality.
    /// </summary>
    Task<List<AiDemandForecast>> GenerateDemandForecastsAsync(Guid? storeId, DateTime businessDate, CancellationToken cancellationToken);
    
    /// <summary>
    /// Evaluates the accuracy of past forecasts against actuals.
    /// </summary>
    Task<ForecastAccuracySnapshot> EvaluateForecastAccuracyAsync(DateTime businessDate, CancellationToken cancellationToken);
}
