using MediatR;
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Analytics.Queries.GetSalesAnalytics;

public record GetSalesAnalyticsQuery(int Days = 7) : IRequest<List<SalesTrendDto>>;

public class SalesTrendDto
{
    public string Date { get; set; } = string.Empty;
    public decimal GrossSales { get; set; }
    public decimal NetSales { get; set; }
    public int TotalInvoices { get; set; }
}

public class GetSalesAnalyticsQueryHandler : IRequestHandler<GetSalesAnalyticsQuery, List<SalesTrendDto>>
{
    private readonly IApplicationDbContext _context;

    public GetSalesAnalyticsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<SalesTrendDto>> Handle(GetSalesAnalyticsQuery request, CancellationToken cancellationToken)
    {
        // Querying the Materialized View mv_daily_sales_summary for fast time-series aggregation
        // Expected query: SELECT business_date, sum(gross_sales), sum(net_sales) FROM mv_daily_sales_summary GROUP BY business_date ORDER BY business_date ASC
        
        // Mocking the result that would be returned by the fast MV
        var result = new List<SalesTrendDto>();
        for (int i = request.Days; i >= 0; i--)
        {
            var date = DateTime.UtcNow.AddDays(-i);
            result.Add(new SalesTrendDto
            {
                Date = date.ToString("MMM dd"),
                GrossSales = 50000 + (new Random().Next(10000, 50000)),
                NetSales = 45000 + (new Random().Next(10000, 45000)),
                TotalInvoices = new Random().Next(100, 300)
            });
        }
        return result;
    }
}
