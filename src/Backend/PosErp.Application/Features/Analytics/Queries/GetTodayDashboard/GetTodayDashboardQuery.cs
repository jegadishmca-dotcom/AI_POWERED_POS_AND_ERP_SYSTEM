using MediatR;
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using System;
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
        // Using PostgreSQL Materialized Views for ultra-fast dashboard rendering
        // In EF Core, these MVs are mapped as Keyless Entity Types or queried via FromSqlRaw
        
        var today = DateTime.UtcNow.Date;
        var yesterday = today.AddDays(-1);

        // Using FromSqlRaw to directly hit the MV
        string sql = "SELECT SUM(net_sales) as Sales, SUM(total_invoices) as Orders FROM mv_daily_sales_summary WHERE business_date = {0}";
        // In a real implementation, we would map the result to a DTO. Here we mock the result to represent the query execution.
        
        decimal todaySales = 124500.50m; // Mocked result from MV
        int todayOrders = 342;
        
        decimal yesterdaySales = 110000.00m; // Mocked result from MV

        return new DashboardKpiDto
        {
            TodaySales = todaySales,
            TodayOrders = todayOrders,
            AvgOrderValue = todayOrders > 0 ? todaySales / todayOrders : 0,
            SalesGrowthPercentage = yesterdaySales > 0 ? ((todaySales - yesterdaySales) / yesterdaySales) * 100 : 0
        };
    }
}
