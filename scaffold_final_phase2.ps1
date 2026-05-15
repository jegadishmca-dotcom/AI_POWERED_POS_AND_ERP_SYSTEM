$backendDir = "d:\JEGADISH\AI_POWERED_POS_AND_ERP_SYSTEM\src\Backend"
$frontendDir = "d:\JEGADISH\AI_POWERED_POS_AND_ERP_SYSTEM\src\Frontend"

New-Item -ItemType Directory -Force -Path "$backendDir\PosErp.Application\Features\Inventory\Commands\ApproveStockTake"
New-Item -ItemType Directory -Force -Path "$backendDir\PosErp.Infrastructure\Jobs"

# 1. Warehouse & StockTake Entities
@"
using System;
using System.Collections.Generic;

namespace PosErp.Domain.Entities.Inventory;

public class Warehouse
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? StoreId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public ICollection<Bin> Bins { get; set; } = new List<Bin>();
}

public class Bin
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WarehouseId { get; set; }
    public string Code { get; set; } = string.Empty; // e.g. A1-01
    public string Description { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public Warehouse Warehouse { get; set; } = null!;
}

public class StockTakeHeader
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? StoreId { get; set; }
    public string TakeNumber { get; set; } = string.Empty;
    public DateTime ScheduledDate { get; set; }
    public string Status { get; set; } = "DRAFT"; // DRAFT, IN_PROGRESS, REVIEW, APPROVED
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? ApprovedBy { get; set; }
    public ICollection<StockTakeItem> Items { get; set; } = new List<StockTakeItem>();
}

public class StockTakeItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StockTakeHeaderId { get; set; }
    public Guid ProductId { get; set; }
    public Guid? BatchId { get; set; }
    
    public decimal SystemQuantity { get; set; }
    public decimal PhysicalQuantity { get; set; }
    public decimal VarianceQuantity => PhysicalQuantity - SystemQuantity;
    
    public StockTakeHeader StockTakeHeader { get; set; } = null!;
}
"@ | Out-File -FilePath "$backendDir\PosErp.Domain\Entities\Inventory\WarehouseEntities.cs" -Encoding utf8

# 2. Supplier Entity
@"
using System;

namespace PosErp.Domain.Entities.Purchasing;

public class Supplier
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Gstin { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string PaymentTerms { get; set; } = "NET30";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
"@ | Out-File -FilePath "$backendDir\PosErp.Domain\Entities\Purchasing\Supplier.cs" -Encoding utf8

# 3. Approve Stock Take Command (Auto-Adjust Ledger)
@"
using MediatR;
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using PosErp.Application.Features.Inventory.Services;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Inventory.Commands.ApproveStockTake;

public record ApproveStockTakeCommand(Guid StockTakeId, Guid? ApproverId) : IRequest<bool>;

public class ApproveStockTakeCommandHandler : IRequestHandler<ApproveStockTakeCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly IStockLedgerService _stockLedgerService;

    public ApproveStockTakeCommandHandler(IApplicationDbContext context, IStockLedgerService stockLedgerService)
    {
        _context = context;
        _stockLedgerService = stockLedgerService;
    }

    public async Task<bool> Handle(ApproveStockTakeCommand request, CancellationToken cancellationToken)
    {
        using var transaction = await ((DbContext)_context).Database.BeginTransactionAsync(cancellationToken);
        try
        {
            // Note: Uses DbSet mapped in DbContext
            // For scaffold, assume context.Set<StockTakeHeader>()
            var take = await _context.Set<PosErp.Domain.Entities.Inventory.StockTakeHeader>()
                .Include(t => t.Items)
                .FirstOrDefaultAsync(t => t.Id == request.StockTakeId, cancellationToken);
                
            if (take == null || take.Status != "REVIEW") throw new Exception("Stock Take not ready for approval.");

            take.Status = "APPROVED";
            take.ApprovedBy = request.ApproverId;

            foreach (var item in take.Items)
            {
                if (item.VarianceQuantity == 0) continue;

                // Create Auto-Adjustment for variance
                await _stockLedgerService.RecordMovementAsync(
                    storeId: take.StoreId ?? Guid.Empty,
                    warehouseId: null,
                    terminalId: null,
                    businessDate: DateTime.UtcNow,
                    productId: item.ProductId,
                    batchId: item.BatchId,
                    movementType: "ADJ", // Automated variance correction
                    quantity: item.VarianceQuantity, // Will be negative if missing stock
                    unitCost: 0, // Should be fetched from product/batch in real app
                    expiryDate: null,
                    referenceDocId: take.Id,
                    referenceNumber: $"TAKE-VAR-{take.TakeNumber}",
                    userId: request.ApproverId,
                    cancellationToken: cancellationToken
                );
            }

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new Exception("Concurrency conflict. Please retry.", ex);
        }
        catch (Exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
"@ | Out-File -FilePath "$backendDir\PosErp.Application\Features\Inventory\Commands\ApproveStockTake\ApproveStockTakeCommand.cs" -Encoding utf8

# 4. Hangfire Job for Expiry Alerts
@"
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace PosErp.Infrastructure.Jobs;

public class CheckExpiryJob
{
    private readonly IApplicationDbContext _context;

    public CheckExpiryJob(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task ExecuteAsync()
    {
        var thresholdDate = DateTime.UtcNow.AddDays(30);

        var expiringBatches = await _context.ProductBatches
            .Where(b => b.IsActive && b.ExpiryDate != null && b.ExpiryDate <= thresholdDate)
            .ToListAsync();

        foreach (var batch in expiringBatches)
        {
            // In a real app, integrate with INotificationService (Email/Push)
            Console.WriteLine($"[ALERT] Batch {batch.BatchNumber} expires on {batch.ExpiryDate}");
        }
    }
}
"@ | Out-File -FilePath "$backendDir\PosErp.Infrastructure\Jobs\CheckExpiryJob.cs" -Encoding utf8

# 5. Database Schema
@"
-- ==============================================================================
-- PHASE 2: WAREHOUSE & STOCK TAKE SCHEMA
-- ==============================================================================

CREATE TABLE suppliers (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    name VARCHAR(200) NOT NULL,
    gstin VARCHAR(15),
    phone VARCHAR(20),
    payment_terms VARCHAR(50) DEFAULT 'NET30',
    is_active BOOLEAN DEFAULT TRUE,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE warehouses (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    store_id UUID,
    name VARCHAR(100) NOT NULL,
    code VARCHAR(50) UNIQUE NOT NULL,
    is_active BOOLEAN DEFAULT TRUE
);

CREATE TABLE bins (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    warehouse_id UUID NOT NULL REFERENCES warehouses(id) ON DELETE CASCADE,
    code VARCHAR(50) NOT NULL,
    description VARCHAR(200),
    is_active BOOLEAN DEFAULT TRUE,
    UNIQUE(warehouse_id, code)
);

CREATE TABLE stock_take_headers (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    store_id UUID,
    take_number VARCHAR(100) UNIQUE NOT NULL,
    scheduled_date DATE NOT NULL,
    status VARCHAR(50) DEFAULT 'DRAFT',
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    approved_by UUID
);

CREATE TABLE stock_take_items (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    stock_take_header_id UUID NOT NULL REFERENCES stock_take_headers(id) ON DELETE CASCADE,
    product_id UUID NOT NULL REFERENCES products(id),
    batch_id UUID REFERENCES product_batches(id),
    system_quantity DECIMAL(18,4) NOT NULL,
    physical_quantity DECIMAL(18,4) NOT NULL
);
"@ | Out-File -FilePath "$backendDir\PosErp.Infrastructure\Persistence\Migrations\06_FinalInventorySchema.sql" -Encoding utf8

# 6. Frontend: Stock Take Form
@"
import React, { useState } from 'react';
import { ClipboardCheck, Save, CheckCircle } from 'lucide-react';

export const StockTakeForm = () => {
  const [items, setItems] = useState([
    { id: '1', product: 'Aashirvaad Atta 5kg', sysQty: 150, physQty: 150 },
    { id: '2', product: 'Tata Salt 1kg', sysQty: 49, physQty: 47 }, // Variance of -2
    { id: '3', product: 'Amul Butter 500g', sysQty: 15, physQty: 15 },
  ]);

  return (
    <div className="bg-white shadow rounded-lg p-6 max-w-5xl mx-auto">
      <div className="flex justify-between items-center mb-6 border-b pb-4">
        <h2 className="text-2xl font-bold text-slate-800 flex items-center">
          <ClipboardCheck className="mr-3 text-purple-600" /> Stock Take (Cycle Count)
        </h2>
        <div className="flex gap-2">
          <button className="px-4 py-2 bg-blue-600 text-white rounded shadow flex items-center hover:bg-blue-700">
            <Save className="w-4 h-4 mr-2" /> Save Progress
          </button>
          <button className="px-4 py-2 bg-purple-600 text-white rounded shadow flex items-center hover:bg-purple-700 font-bold">
            <CheckCircle className="w-4 h-4 mr-2" /> Submit for Review
          </button>
        </div>
      </div>

      <table className="w-full text-left border-collapse">
        <thead className="bg-slate-100 text-sm">
          <tr>
            <th className="p-3 border w-1/2">Product</th>
            <th className="p-3 border text-center">System Qty (mv_current_stock)</th>
            <th className="p-3 border text-center">Physical Count</th>
            <th className="p-3 border text-center">Variance</th>
          </tr>
        </thead>
        <tbody>
          {items.map((item, idx) => {
            const variance = item.physQty - item.sysQty;
            return (
              <tr key={item.id} className="border-b hover:bg-slate-50">
                <td className="p-3 font-bold text-slate-800">{item.product}</td>
                <td className="p-3 text-center text-gray-500 font-bold">{item.sysQty}</td>
                <td className="p-3">
                  <input 
                    type="number" 
                    className="w-full p-2 border border-purple-200 rounded text-center font-bold" 
                    value={item.physQty}
                    onChange={(e) => {
                      const newItems = [...items];
                      newItems[idx].physQty = parseFloat(e.target.value) || 0;
                      setItems(newItems);
                    }}
                  />
                </td>
                <td className={`p-3 text-center font-bold \${variance < 0 ? 'text-red-600' : variance > 0 ? 'text-emerald-600' : 'text-gray-400'}`}>
                  {variance > 0 ? '+' : ''}{variance}
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
};
"@ | Out-File -FilePath "$frontendDir\src\features\inventory\components\StockTakeForm.tsx" -Encoding utf8

# 7. Frontend: Warehouse List
@"
import React from 'react';
import { MapPin, Plus } from 'lucide-react';

export const WarehouseLocationsList = () => {
  const warehouses = [
    { id: 1, name: 'Main Store', code: 'WH-MAIN', bins: ['A1-01', 'A1-02', 'B1-01'] },
    { id: 2, name: 'Backroom Storage', code: 'WH-BACK', bins: ['C1-01', 'C1-02'] },
  ];

  return (
    <div className="bg-white shadow rounded-lg p-6 max-w-4xl mx-auto">
      <div className="flex justify-between items-center mb-6 border-b pb-4">
        <h2 className="text-2xl font-bold text-slate-800 flex items-center">
          <MapPin className="mr-3 text-red-600" /> Warehouse & Bins
        </h2>
        <button className="px-4 py-2 bg-red-600 text-white rounded flex items-center font-bold hover:bg-red-700">
          <Plus className="w-5 h-5 mr-1" /> Add Warehouse
        </button>
      </div>

      <div className="grid gap-6">
        {warehouses.map(wh => (
          <div key={wh.id} className="border rounded-lg p-4 bg-slate-50">
            <div className="flex justify-between items-center mb-4 border-b pb-2">
              <h3 className="font-bold text-lg text-slate-800">{wh.name} <span className="text-sm text-gray-500 ml-2">({wh.code})</span></h3>
              <button className="text-red-600 text-sm font-bold">+ Add Bin</button>
            </div>
            <div className="flex flex-wrap gap-2">
              {wh.bins.map(bin => (
                <span key={bin} className="bg-white border border-gray-300 px-3 py-1 rounded text-sm shadow-sm">{bin}</span>
              ))}
            </div>
          </div>
        ))}
      </div>
    </div>
  );
};
"@ | Out-File -FilePath "$frontendDir\src\features\inventory\components\WarehouseLocationsList.tsx" -Encoding utf8

Write-Host "Phase 2 Finalization Complete"
