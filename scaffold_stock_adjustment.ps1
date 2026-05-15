$backendDir = "d:\JEGADISH\AI_POWERED_POS_AND_ERP_SYSTEM\src\Backend"
$frontendDir = "d:\JEGADISH\AI_POWERED_POS_AND_ERP_SYSTEM\src\Frontend"

New-Item -ItemType Directory -Force -Path "$backendDir\PosErp.Application\Features\Inventory\Commands\CreateStockAdjustment"
New-Item -ItemType Directory -Force -Path "$backendDir\PosErp.Application\Features\Inventory\Commands\ApproveStockAdjustment"

# 1. Create Stock Adjustment Command
@"
using MediatR;
using PosErp.Application.Interfaces;
using PosErp.Domain.Entities.Inventory;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Inventory.Commands.CreateStockAdjustment;

public record CreateStockAdjustmentCommand(
    Guid? StoreId,
    string Reason, // DAMAGE, THEFT, FOUND
    List<StockAdjustmentItemDto> Items,
    Guid? UserId
) : IRequest<Guid>;

public record StockAdjustmentItemDto(
    Guid ProductId,
    Guid? BatchId,
    decimal AdjustedQuantity, // Can be negative (damage) or positive (found)
    decimal UnitCost
);

public class CreateStockAdjustmentCommandHandler : IRequestHandler<CreateStockAdjustmentCommand, Guid>
{
    private readonly IApplicationDbContext _context;

    public CreateStockAdjustmentCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Guid> Handle(CreateStockAdjustmentCommand request, CancellationToken cancellationToken)
    {
        var adjustment = new StockAdjustment
        {
            StoreId = request.StoreId,
            AdjustmentNumber = $"ADJ-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString().Substring(0,4).ToUpper()}",
            Reason = request.Reason,
            Status = "PENDING"
        };

        foreach (var dto in request.Items)
        {
            adjustment.Items.Add(new StockAdjustmentItem
            {
                ProductId = dto.ProductId,
                BatchId = dto.BatchId,
                AdjustedQuantity = dto.AdjustedQuantity,
                UnitCost = dto.UnitCost
            });
        }

        _context.StockAdjustments.Add(adjustment);
        await _context.SaveChangesAsync(cancellationToken);

        return adjustment.Id;
    }
}
"@ | Out-File -FilePath "$backendDir\PosErp.Application\Features\Inventory\Commands\CreateStockAdjustment\CreateStockAdjustmentCommand.cs" -Encoding utf8

# 2. Approve Stock Adjustment Command (Hits Ledger)
@"
using MediatR;
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using PosErp.Application.Features.Inventory.Services;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Inventory.Commands.ApproveStockAdjustment;

public record ApproveStockAdjustmentCommand(Guid AdjustmentId, Guid? ApproverId) : IRequest<bool>;

public class ApproveStockAdjustmentCommandHandler : IRequestHandler<ApproveStockAdjustmentCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly IStockLedgerService _stockLedgerService;

    public ApproveStockAdjustmentCommandHandler(IApplicationDbContext context, IStockLedgerService stockLedgerService)
    {
        _context = context;
        _stockLedgerService = stockLedgerService;
    }

    public async Task<bool> Handle(ApproveStockAdjustmentCommand request, CancellationToken cancellationToken)
    {
        using var transaction = await ((DbContext)_context).Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var adj = await _context.StockAdjustments
                .Include(a => a.Items)
                .FirstOrDefaultAsync(a => a.Id == request.AdjustmentId, cancellationToken);
                
            if (adj == null || adj.Status != "PENDING") throw new Exception("Invalid or already processed Adjustment.");

            adj.Status = "APPROVED";
            adj.ApprovedBy = request.ApproverId;

            foreach(var item in adj.Items)
            {
                if (item.AdjustedQuantity == 0) continue;

                await _stockLedgerService.RecordMovementAsync(
                    storeId: adj.StoreId ?? Guid.Empty,
                    warehouseId: null,
                    terminalId: null,
                    businessDate: DateTime.UtcNow,
                    productId: item.ProductId,
                    batchId: item.BatchId,
                    movementType: "ADJ", // Key movement type
                    quantity: item.AdjustedQuantity, // Can be -ve or +ve
                    unitCost: item.UnitCost,
                    expiryDate: null,
                    referenceDocId: adj.Id,
                    referenceNumber: adj.AdjustmentNumber,
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
"@ | Out-File -FilePath "$backendDir\PosErp.Application\Features\Inventory\Commands\ApproveStockAdjustment\ApproveStockAdjustmentCommand.cs" -Encoding utf8

# 3. Frontend: Stock Adjustment Form
@"
import React, { useState } from 'react';
import { Save, ShieldAlert, Plus, Search } from 'lucide-react';

export const StockAdjustmentForm = () => {
  const [items, setItems] = useState([{ productId: '', name: '', qty: -1, reason: 'DAMAGE' }]);

  const handleAddItem = () => setItems([...items, { productId: '', name: '', qty: -1, reason: 'DAMAGE' }]);

  return (
    <div className="bg-white shadow rounded-lg p-6 max-w-5xl mx-auto">
      <div className="flex justify-between items-center mb-6 border-b pb-4">
        <h2 className="text-2xl font-bold text-slate-800">Record Stock Adjustment</h2>
        <button className="px-6 py-2 bg-orange-600 text-white rounded shadow flex items-center font-bold hover:bg-orange-700">
          <Save className="w-5 h-5 mr-2" /> Submit for Approval
        </button>
      </div>

      <div className="bg-orange-50 border-l-4 border-orange-500 p-4 mb-6 text-orange-800 text-sm flex items-start">
        <ShieldAlert className="w-5 h-5 mr-2 mt-0.5 flex-shrink-0" />
        <p>
          <strong>Manager Approval Required:</strong> All adjustments impacting inventory value will be placed in a PENDING state and will not reflect on the Stock Ledger until explicitly approved by a Manager.
        </p>
      </div>

      <div className="mb-4 flex justify-between items-center">
        <h3 className="text-lg font-bold text-slate-800">Adjustment Lines</h3>
        <button onClick={handleAddItem} className="text-blue-600 font-bold flex items-center hover:text-blue-800">
          <Plus className="w-4 h-4 mr-1" /> Add Row
        </button>
      </div>

      <table className="w-full text-left border-collapse">
        <thead className="bg-slate-100 text-sm">
          <tr>
            <th className="p-3 border w-1/2">Product (Search)</th>
            <th className="p-3 border">Adjusted Qty (-/+)</th>
            <th className="p-3 border">Reason Code</th>
          </tr>
        </thead>
        <tbody>
          {items.map((item, idx) => (
            <tr key={idx} className="border-b">
              <td className="p-3">
                <div className="relative">
                  <Search className="absolute left-2 top-2 text-gray-400 w-4 h-4" />
                  <input type="text" placeholder="Scan barcode or search name..." className="w-full pl-8 p-1 border rounded text-sm" />
                </div>
              </td>
              <td className="p-3">
                <input 
                  type="number" 
                  className={`w-full p-1 border rounded text-right \${item.qty < 0 ? 'text-red-600 bg-red-50' : 'text-emerald-600 bg-emerald-50'}`}
                  value={item.qty} 
                  onChange={(e) => {
                    const newItems = [...items];
                    newItems[idx].qty = parseInt(e.target.value) || 0;
                    setItems(newItems);
                  }}
                />
              </td>
              <td className="p-3">
                <select 
                  className="w-full p-1 border rounded text-sm"
                  value={item.reason}
                  onChange={(e) => {
                    const newItems = [...items];
                    newItems[idx].reason = e.target.value;
                    setItems(newItems);
                  }}
                >
                  <option value="DAMAGE">Damaged / Broken</option>
                  <option value="EXPIRED">Expired Write-off</option>
                  <option value="THEFT">Shrinkage / Theft</option>
                  <option value="FOUND">Found Stock (+)</option>
                </select>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
};
"@ | Out-File -FilePath "$frontendDir\src\features\inventory\components\StockAdjustmentForm.tsx" -Encoding utf8

Write-Host "Stock Adjustment Module Scaffolded"
