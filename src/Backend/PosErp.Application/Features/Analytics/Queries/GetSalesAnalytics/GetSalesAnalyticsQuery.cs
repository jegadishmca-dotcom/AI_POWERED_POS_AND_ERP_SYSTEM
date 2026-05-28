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
        var startDate = DateTime.UtcNow.Date.AddDays(-request.Days);

        var salesData = await _context.Invoices
            .Where(i => i.BusinessDate >= startDate && i.Status == "COMPLETED")
            .GroupBy(i => i.BusinessDate)
            .Select(g => new
            {
                Date = g.Key,
                GrossSales = g.Sum(i => i.SubTotal),
                NetSales = g.Sum(i => i.TotalAmount),
                TotalInvoices = g.Count()
            })
            .OrderBy(g => g.Date)
            .ToListAsync(cancellationToken);

        var trendMap = salesData.ToDictionary(x => x.Date, x => x);

        var result = new List<SalesTrendDto>();
        for (int i = request.Days; i >= 0; i--)
        {
            var date = DateTime.UtcNow.Date.AddDays(-i);
            if (trendMap.TryGetValue(date, out var data))
            {
                result.Add(new SalesTrendDto
                {
                    Date = date.ToString("MMM dd"),
                    GrossSales = data.GrossSales,
                    NetSales = data.NetSales,
                    TotalInvoices = data.TotalInvoices
                });
            }
            else
            {
                result.Add(new SalesTrendDto
                {
                    Date = date.ToString("MMM dd"),
                    GrossSales = 0,
                    NetSales = 0,
                    TotalInvoices = 0
                });
            }
        }

        return result;
    }
}
