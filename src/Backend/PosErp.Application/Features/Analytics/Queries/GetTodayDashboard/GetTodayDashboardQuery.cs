using MediatR;
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Analytics.Queries.GetTodayDashboard;

public record GetTodayDashboardQuery(Guid? TerminalId) : IRequest<DashboardKpiDto>;

public class DashboardKpiDto
{
    public decimal TodaySales { get; set; }
    public int TodayOrders { get; set; }
    public decimal AvgOrderValue { get; set; }
    public decimal SalesGrowthPercentage { get; set; } 
}

public class GetTodayDashboardQueryHandler : IRequestHandler<GetTodayDashboardQuery, DashboardKpiDto>
{
    private readonly IApplicationDbContext _context;

    public GetTodayDashboardQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<DashboardKpiDto> Handle(GetTodayDashboardQuery request, CancellationToken cancellationToken)
    {
        var today = DateTime.UtcNow.Date;
        var yesterday = today.AddDays(-1);

        // Fetch today's sales and order count from real invoices
        var todayStats = await _context.Invoices
            .Where(i => i.BusinessDate == today && i.Status == "COMPLETED")
            .GroupBy(i => 1)
            .Select(g => new {
                Sales = g.Sum(i => i.TotalAmount),
                Orders = g.Count()
            })
            .FirstOrDefaultAsync(cancellationToken);

        // Fetch yesterday's sales to calculate growth
        var yesterdaySales = await _context.Invoices
            .Where(i => i.BusinessDate == yesterday && i.Status == "COMPLETED")
            .Select(i => i.TotalAmount)
            .SumAsync(cancellationToken);

        decimal todaySales = todayStats?.Sales ?? 0m;
        int todayOrders = todayStats?.Orders ?? 0;

        return new DashboardKpiDto
        {
            TodaySales = todaySales,
            TodayOrders = todayOrders,
            AvgOrderValue = todayOrders > 0 ? todaySales / todayOrders : 0,
            SalesGrowthPercentage = yesterdaySales > 0 ? ((todaySales - yesterdaySales) / yesterdaySales) * 100 : 0
        };
    }
}
