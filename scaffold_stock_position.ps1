$backendDir = "d:\JEGADISH\AI_POWERED_POS_AND_ERP_SYSTEM\src\Backend"
$frontendDir = "d:\JEGADISH\AI_POWERED_POS_AND_ERP_SYSTEM\src\Frontend"

New-Item -ItemType Directory -Force -Path "$backendDir\PosErp.Application\Features\Inventory\Queries\GetStockPosition"
New-Item -ItemType Directory -Force -Path "$backendDir\PosErp.Application\Features\Inventory\Queries\GetNearExpiryAlerts"

# 1. Database View for Fast Stock Aggregation
@"
-- ==============================================================================
-- PHASE 2: STOCK POSITION VIEWS
-- ==============================================================================

-- 1. Current Stock Materialized View
-- We use a MATERIALIZED VIEW for near-instant reporting on massive catalogs.
-- A Hangfire job should be scheduled to run `REFRESH MATERIALIZED VIEW CONCURRENTLY mv_current_stock` every 5-10 minutes.

CREATE MATERIALIZED VIEW mv_current_stock AS
SELECT DISTINCT ON (store_id, product_id)
    id as latest_ledger_id,
    store_id,
    product_id,
    batch_id,
    running_balance as current_stock,
    unit_cost as last_unit_cost,
    created_at as last_movement_date
FROM stock_ledger
ORDER BY store_id, product_id, created_at DESC;

CREATE UNIQUE INDEX idx_mv_current_stock_unique ON mv_current_stock (store_id, product_id);
"@ | Out-File -FilePath "$backendDir\PosErp.Infrastructure\Persistence\Migrations\05_StockViewsSchema.sql" -Encoding utf8

# 2. GetStockPosition Query
@"
using MediatR;
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Inventory.Queries.GetStockPosition;

public record GetStockPositionQuery(Guid? StoreId, Guid? CategoryId, string? SearchTerm) : IRequest<List<StockPositionDto>>;

public record StockPositionDto(
    Guid ProductId,
    string ProductCode,
    string ProductName,
    string CategoryName,
    decimal CurrentStock,
    decimal LastUnitCost,
    decimal TotalValue
);

public class GetStockPositionQueryHandler : IRequestHandler<GetStockPositionQuery, List<StockPositionDto>>
{
    private readonly IApplicationDbContext _context;

    public GetStockPositionQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<StockPositionDto>> Handle(GetStockPositionQuery request, CancellationToken cancellationToken)
    {
        // In a real implementation, we would query the mv_current_stock via Dapper or EF Core Raw SQL
        // since EF Core doesn't natively map materialized views without a defined entity.
        // For scaffold purposes, we simulate the query structure.

        var sql = @"
            SELECT 
                p.id as ProductId,
                p.product_code as ProductCode,
                p.name as ProductName,
                c.name as CategoryName,
                COALESCE(mv.current_stock, 0) as CurrentStock,
                COALESCE(mv.last_unit_cost, 0) as LastUnitCost
            FROM products p
            LEFT JOIN categories c ON p.category_id = c.id
            LEFT JOIN mv_current_stock mv ON p.id = mv.product_id AND (mv.store_id = @p0 OR @p0 IS NULL)
            WHERE (@p1 IS NULL OR p.category_id = @p1)
              AND (@p2 IS NULL OR p.name ILIKE '%' || @p2 || '%' OR p.product_code ILIKE '%' || @p2 || '%')
            ORDER BY p.name";

        // Execution logic omitted for scaffold...
        
        return new List<StockPositionDto>();
    }
}
"@ | Out-File -FilePath "$backendDir\PosErp.Application\Features\Inventory\Queries\GetStockPosition\GetStockPositionQuery.cs" -Encoding utf8

# 3. GetNearExpiryAlerts Query
@"
using MediatR;
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Inventory.Queries.GetNearExpiryAlerts;

public record GetNearExpiryAlertsQuery(int DaysThreshold = 30) : IRequest<List<NearExpiryDto>>;

public record NearExpiryDto(
    Guid ProductId,
    string ProductName,
    string BatchNumber,
    DateTime ExpiryDate,
    int DaysRemaining,
    decimal AvailableStock
);

public class GetNearExpiryAlertsQueryHandler : IRequestHandler<GetNearExpiryAlertsQuery, List<NearExpiryDto>>
{
    private readonly IApplicationDbContext _context;

    public GetNearExpiryAlertsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<NearExpiryDto>> Handle(GetNearExpiryAlertsQuery request, CancellationToken cancellationToken)
    {
        var thresholdDate = DateTime.UtcNow.AddDays(request.DaysThreshold);

        // Queries active batches expiring soon.
        // In reality, we must join with StockLedger or mv_current_stock to ensure we only alert on batches that actually have positive stock.
        var alerts = await _context.ProductBatches
            .Include(b => b.Product)
            .Where(b => b.IsActive && b.ExpiryDate != null && b.ExpiryDate <= thresholdDate && b.ExpiryDate >= DateTime.UtcNow)
            .Select(b => new NearExpiryDto(
                b.ProductId,
                b.Product.Name,
                b.BatchNumber,
                b.ExpiryDate.Value,
                (b.ExpiryDate.Value - DateTime.UtcNow).Days,
                0 // Would come from mv_current_stock
            ))
            .OrderBy(a => a.DaysRemaining)
            .ToListAsync(cancellationToken);

        return alerts;
    }
}
"@ | Out-File -FilePath "$backendDir\PosErp.Application\Features\Inventory\Queries\GetNearExpiryAlerts\GetNearExpiryAlertsQuery.cs" -Encoding utf8

# 4. Frontend: Stock Position Report
@"
import React from 'react';
import { Layers, Download, Search, Filter } from 'lucide-react';

export const StockPositionReport = () => {
  // Mock Data
  const stockData = [
    { id: '1', code: 'PRD-001', name: 'Aashirvaad Atta 5kg', category: 'Grocery', stock: 150, value: 30000 },
    { id: '2', code: 'PRD-002', name: 'Tata Salt 1kg', category: 'Grocery', stock: 49, value: 980 },
    { id: '3', code: 'PRD-003', name: 'Amul Butter 500g', category: 'Dairy', stock: 15, value: 3750 },
  ];

  return (
    <div className="bg-white shadow rounded-lg p-6 max-w-7xl mx-auto">
      <div className="flex justify-between items-center mb-6 border-b pb-4">
        <h2 className="text-2xl font-bold text-slate-800 flex items-center">
          <Layers className="mr-3 text-indigo-600" /> Current Stock Position
        </h2>
        <button className="px-4 py-2 bg-slate-800 text-white rounded shadow flex items-center hover:bg-slate-700">
          <Download className="w-4 h-4 mr-2" /> Export to CSV
        </button>
      </div>

      <div className="flex gap-4 mb-6 bg-slate-50 p-4 rounded-lg border">
        <div className="relative flex-1">
          <Search className="absolute left-3 top-3 text-gray-400 w-4 h-4" />
          <input type="text" placeholder="Search Product Code or Name..." className="w-full pl-9 p-2 border rounded text-sm" />
        </div>
        <select className="p-2 border rounded text-sm w-48">
          <option value="">All Categories</option>
          <option value="Grocery">Grocery</option>
          <option value="Dairy">Dairy</option>
        </select>
        <button className="px-4 py-2 bg-indigo-600 text-white rounded flex items-center text-sm font-bold">
          <Filter className="w-4 h-4 mr-2" /> Filter
        </button>
      </div>

      <div className="overflow-x-auto">
        <table className="w-full text-left border-collapse">
          <thead className="bg-indigo-50 text-indigo-900 text-sm">
            <tr>
              <th className="p-3 border">Product Code</th>
              <th className="p-3 border">Product Name</th>
              <th className="p-3 border">Category</th>
              <th className="p-3 border text-right">Current Stock</th>
              <th className="p-3 border text-right">Stock Value (₹)</th>
            </tr>
          </thead>
          <tbody>
            {stockData.map((item) => (
              <tr key={item.id} className="border-b hover:bg-slate-50">
                <td className="p-3 text-sm text-gray-500">{item.code}</td>
                <td className="p-3 font-bold text-slate-800">{item.name}</td>
                <td className="p-3 text-sm">{item.category}</td>
                <td className="p-3 text-right font-black text-lg text-slate-800">
                  <span className={item.stock < 20 ? 'text-red-600' : ''}>{item.stock}</span>
                </td>
                <td className="p-3 text-right font-bold text-gray-600">₹{item.value.toLocaleString()}</td>
              </tr>
            ))}
          </tbody>
          <tfoot className="bg-slate-50 font-bold">
            <tr>
              <td colSpan={4} className="p-3 text-right">Total Inventory Value:</td>
              <td className="p-3 text-right text-indigo-700 text-lg">₹34,730.00</td>
            </tr>
          </tfoot>
        </table>
      </div>
    </div>
  );
};
"@ | Out-File -FilePath "$frontendDir\src\features\inventory\components\StockPositionReport.tsx" -Encoding utf8

# 5. Frontend: Near Expiry Widget
@"
import React from 'react';
import { AlertTriangle, Clock } from 'lucide-react';

export const NearExpiryDashboardWidget = () => {
  const alerts = [
    { id: 1, product: 'Amul Butter 500g', batch: 'B-098', days: 5, stock: 15 },
    { id: 2, product: 'Britannia Bread', batch: 'B-102', days: 2, stock: 8 },
  ];

  return (
    <div className="bg-white shadow rounded-lg p-4 border-l-4 border-orange-500">
      <div className="flex items-center justify-between mb-4">
        <h3 className="font-bold text-slate-800 flex items-center">
          <AlertTriangle className="w-5 h-5 text-orange-500 mr-2" /> Near Expiry Alerts
        </h3>
        <span className="bg-orange-100 text-orange-800 text-xs px-2 py-1 rounded font-bold">30 Days</span>
      </div>

      <div className="space-y-3">
        {alerts.map(alert => (
          <div key={alert.id} className="flex justify-between items-center p-3 bg-orange-50 rounded border border-orange-100">
            <div>
              <p className="font-bold text-sm text-slate-800">{alert.product}</p>
              <p className="text-xs text-gray-500">Batch: {alert.batch} | Stock: {alert.stock}</p>
            </div>
            <div className="text-right">
              <p className={`font-bold text-sm flex items-center justify-end \${alert.days <= 3 ? 'text-red-600' : 'text-orange-600'}`}>
                <Clock className="w-3 h-3 mr-1" /> {alert.days} Days
              </p>
            </div>
          </div>
        ))}
      </div>
    </div>
  );
};
"@ | Out-File -FilePath "$frontendDir\src\features\inventory\components\NearExpiryDashboardWidget.tsx" -Encoding utf8

Write-Host "Stock Position Module Scaffolded"
