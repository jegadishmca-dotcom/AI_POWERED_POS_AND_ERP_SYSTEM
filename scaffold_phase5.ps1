$backendDir = "d:\JEGADISH\AI_POWERED_POS_AND_ERP_SYSTEM\src\Backend"
$frontendDir = "d:\JEGADISH\AI_POWERED_POS_AND_ERP_SYSTEM\src\Frontend"

New-Item -ItemType Directory -Force -Path "$backendDir\PosErp.Application\Features\Analytics\Queries\GetTodayDashboard"
New-Item -ItemType Directory -Force -Path "$backendDir\PosErp.Application\Features\Analytics\Queries\GetSalesAnalytics"
New-Item -ItemType Directory -Force -Path "$backendDir\PosErp.Application\Features\Analytics\Queries\GetTopProducts"

New-Item -ItemType Directory -Force -Path "$frontendDir\src\features\analytics\components"

# 1. Database Materialized Views
@"
-- ==============================================================================
-- PHASE 5: ANALYTICS MATERIALIZED VIEWS
-- ==============================================================================

-- Daily Sales Summary (Fast line charts)
CREATE MATERIALIZED VIEW mv_daily_sales_summary AS
SELECT 
    terminal_id,
    business_date,
    COUNT(id) as total_invoices,
    SUM(sub_total) as gross_sales,
    SUM(total_discount) as total_discounts,
    SUM(tax_total) as total_tax,
    SUM(total_amount) as net_sales
FROM invoices
WHERE status = 'COMPLETED'
GROUP BY terminal_id, business_date;

CREATE UNIQUE INDEX idx_mv_daily_sales_summary ON mv_daily_sales_summary(terminal_id, business_date);

-- Top Products Stats (Fast tables)
CREATE MATERIALIZED VIEW mv_product_sales_stats AS
SELECT 
    i.business_date,
    ii.product_id,
    SUM(ii.quantity) as total_quantity_sold,
    SUM(ii.final_total) as total_revenue
FROM invoices i
JOIN invoice_items ii ON i.id = ii.invoice_id
WHERE i.status = 'COMPLETED'
GROUP BY i.business_date, ii.product_id;

CREATE UNIQUE INDEX idx_mv_product_sales_stats ON mv_product_sales_stats(business_date, product_id);

-- Hangfire will call: REFRESH MATERIALIZED VIEW CONCURRENTLY mv_daily_sales_summary;
"@ | Out-File -FilePath "$backendDir\PosErp.Infrastructure\Persistence\Migrations\13_AnalyticsViewsSchema.sql" -Encoding utf8

# 2. GetTodayDashboardQuery
@"
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
    public decimal SalesGrowthPercentage { get; set; } // Compared to yesterday
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

        // Fetch Today's metrics directly from invoices for real-time accuracy (since MVs might be slightly delayed)
        var todayQuery = _context.Invoices.Where(i => i.BusinessDate == today && i.Status == "COMPLETED");
        if (request.TerminalId.HasValue) todayQuery = todayQuery.Where(i => i.TerminalId == request.TerminalId.Value);

        var todayData = await todayQuery
            .GroupBy(i => 1)
            .Select(g => new { Sales = g.Sum(i => i.TotalAmount), Orders = g.Count() })
            .FirstOrDefaultAsync(cancellationToken);

        // Fetch Yesterday from MV for speed
        // This is pseudo-code representation of querying the MV (would usually map to a DbQuery/Entity in EF)
        // For simplicity in this scaffold, we'll mock the yesterday comparison.
        decimal yesterdaySales = 50000m; 

        decimal todaySales = todayData?.Sales ?? 0;
        int todayOrders = todayData?.Orders ?? 0;

        return new DashboardKpiDto
        {
            TodaySales = todaySales,
            TodayOrders = todayOrders,
            AvgOrderValue = todayOrders > 0 ? todaySales / todayOrders : 0,
            SalesGrowthPercentage = yesterdaySales > 0 ? ((todaySales - yesterdaySales) / yesterdaySales) * 100 : 0
        };
    }
}
"@ | Out-File -FilePath "$backendDir\PosErp.Application\Features\Analytics\Queries\GetTodayDashboard\GetTodayDashboardQuery.cs" -Encoding utf8

# 3. GetTopProductsQuery
@"
using MediatR;
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Analytics.Queries.GetTopProducts;

public record GetTopProductsQuery(int Days = 30) : IRequest<List<TopProductDto>>;

public class TopProductDto
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal TotalQuantitySold { get; set; }
    public decimal TotalRevenue { get; set; }
}

public class GetTopProductsQueryHandler : IRequestHandler<GetTopProductsQuery, List<TopProductDto>>
{
    private readonly IApplicationDbContext _context;

    public GetTopProductsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<TopProductDto>> Handle(GetTopProductsQuery request, CancellationToken cancellationToken)
    {
        // Querying the live tables. In production, this would hit the `mv_product_sales_stats` View.
        var fromDate = DateTime.UtcNow.Date.AddDays(-request.Days);

        return await _context.InvoiceItems
            .Include(ii => ii.Product)
            .Where(ii => ii.Invoice.BusinessDate >= fromDate && ii.Invoice.Status == "COMPLETED")
            .GroupBy(ii => new { ii.ProductId, ii.Product.Name })
            .Select(g => new TopProductDto
            {
                ProductId = g.Key.ProductId,
                ProductName = g.Key.Name,
                TotalQuantitySold = g.Sum(x => x.Quantity),
                TotalRevenue = g.Sum(x => x.FinalTotal)
            })
            .OrderByDescending(x => x.TotalRevenue)
            .Take(10)
            .ToListAsync(cancellationToken);
    }
}
"@ | Out-File -FilePath "$backendDir\PosErp.Application\Features\Analytics\Queries\GetTopProducts\GetTopProductsQuery.cs" -Encoding utf8

# 4. Frontend Dashboard Components
@"
import React, { useState, useEffect } from 'react';
import { TrendingUp, ShoppingBag, Users, DollarSign } from 'lucide-react';
// import { LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer } from 'recharts';

// Mock Data for Charts since Recharts might not be installed yet
const mockChartData = [
  { name: 'Mon', sales: 4000 },
  { name: 'Tue', sales: 3000 },
  { name: 'Wed', sales: 5000 },
  { name: 'Thu', sales: 2780 },
  { name: 'Fri', sales: 8900 },
  { name: 'Sat', sales: 12000 },
  { name: 'Sun', sales: 14000 },
];

export const Dashboard = () => {
  const [kpis, setKpis] = useState({ sales: 0, orders: 0, avg: 0, growth: 0 });

  useEffect(() => {
    // In reality, this fetches from GetTodayDashboardQuery
    setKpis({
      sales: 124500.50,
      orders: 342,
      avg: 364.03,
      growth: +12.5
    });
  }, []);

  return (
    <div className="p-8 bg-slate-50 min-h-screen">
      <div className="flex justify-between items-center mb-8">
        <h1 className="text-3xl font-black text-slate-800">Operational Dashboard</h1>
        <div className="flex gap-4">
          <button className="px-4 py-2 bg-white border border-gray-300 rounded font-bold text-gray-700 shadow-sm hover:bg-gray-50">Export PDF</button>
          <button className="px-4 py-2 bg-indigo-600 text-white rounded font-bold shadow-sm hover:bg-indigo-700">Export Excel</button>
        </div>
      </div>

      {/* KPI Cards */}
      <div className="grid grid-cols-1 md:grid-cols-4 gap-6 mb-8">
        <div className="bg-white p-6 rounded-xl shadow-sm border border-slate-100 flex items-center justify-between">
          <div>
            <p className="text-sm font-bold text-gray-500 mb-1">Today's Sales</p>
            <h2 className="text-3xl font-black text-slate-800">₹{kpis.sales.toLocaleString()}</h2>
            <p className={`text-sm font-bold mt-2 \${kpis.growth > 0 ? 'text-emerald-500' : 'text-red-500'}`}>
              {kpis.growth > 0 ? '↑' : '↓'} {Math.abs(kpis.growth)}% vs Yesterday
            </p>
          </div>
          <div className="w-12 h-12 rounded-full bg-emerald-100 flex items-center justify-center text-emerald-600">
            <DollarSign className="w-6 h-6" />
          </div>
        </div>
        
        <div className="bg-white p-6 rounded-xl shadow-sm border border-slate-100 flex items-center justify-between">
          <div>
            <p className="text-sm font-bold text-gray-500 mb-1">Total Orders</p>
            <h2 className="text-3xl font-black text-slate-800">{kpis.orders}</h2>
          </div>
          <div className="w-12 h-12 rounded-full bg-blue-100 flex items-center justify-center text-blue-600">
            <ShoppingBag className="w-6 h-6" />
          </div>
        </div>

        <div className="bg-white p-6 rounded-xl shadow-sm border border-slate-100 flex items-center justify-between">
          <div>
            <p className="text-sm font-bold text-gray-500 mb-1">Avg Order Value</p>
            <h2 className="text-3xl font-black text-slate-800">₹{kpis.avg.toFixed(2)}</h2>
          </div>
          <div className="w-12 h-12 rounded-full bg-purple-100 flex items-center justify-center text-purple-600">
            <TrendingUp className="w-6 h-6" />
          </div>
        </div>

        <div className="bg-white p-6 rounded-xl shadow-sm border border-slate-100 flex items-center justify-between">
          <div>
            <p className="text-sm font-bold text-gray-500 mb-1">Loyalty Signups</p>
            <h2 className="text-3xl font-black text-slate-800">45</h2>
          </div>
          <div className="w-12 h-12 rounded-full bg-orange-100 flex items-center justify-center text-orange-600">
            <Users className="w-6 h-6" />
          </div>
        </div>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-8">
        {/* Chart Area */}
        <div className="lg:col-span-2 bg-white p-6 rounded-xl shadow-sm border border-slate-100">
          <h3 className="text-lg font-bold text-slate-800 mb-6">7-Day Sales Trend</h3>
          <div className="h-80 flex items-center justify-center bg-slate-50 rounded border border-dashed border-slate-300">
            {/* Recharts placeholder */}
            <p className="text-slate-400 font-bold flex items-center"><TrendingUp className="mr-2" /> [Recharts LineChart renders here mapping mockChartData]</p>
          </div>
        </div>

        {/* Top Products Table */}
        <div className="bg-white p-6 rounded-xl shadow-sm border border-slate-100">
          <h3 className="text-lg font-bold text-slate-800 mb-6">Top Moving Products</h3>
          <div className="space-y-4">
            {['Aashirvaad Atta 5kg', 'Tata Salt 1kg', 'Fortune Sunflower Oil 1L', 'Amul Taaza Milk 500ml', 'Britannia Good Day'].map((prod, idx) => (
              <div key={idx} className="flex justify-between items-center border-b pb-3 last:border-0">
                <div>
                  <p className="font-bold text-slate-800 text-sm">{prod}</p>
                  <p className="text-xs text-gray-500">{(50 - idx * 8)} Units Sold</p>
                </div>
                <p className="font-black text-indigo-600">₹{(10000 - idx * 1200)}</p>
              </div>
            ))}
          </div>
        </div>
      </div>

    </div>
  );
};
"@ | Out-File -FilePath "$frontendDir\src\features\analytics\components\Dashboard.tsx" -Encoding utf8

Write-Host "Phase 5 Scaffolding Complete"
