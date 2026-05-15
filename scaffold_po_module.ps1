$backendDir = "d:\JEGADISH\AI_POWERED_POS_AND_ERP_SYSTEM\src\Backend"
$frontendDir = "d:\JEGADISH\AI_POWERED_POS_AND_ERP_SYSTEM\src\Frontend"

New-Item -ItemType Directory -Force -Path "$backendDir\PosErp.Application\Features\Purchasing\Commands\CreatePurchaseOrder"
New-Item -ItemType Directory -Force -Path "$backendDir\PosErp.Application\Features\Purchasing\Commands\ApprovePurchaseOrder"
New-Item -ItemType Directory -Force -Path "$frontendDir\src\features\purchasing\components"

# 1. Create Purchase Order Command
@"
using MediatR;
using PosErp.Application.Interfaces;
using PosErp.Domain.Entities.Purchasing;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

namespace PosErp.Application.Features.Purchasing.Commands.CreatePurchaseOrder;

public record CreatePurchaseOrderCommand(
    Guid? StoreId,
    Guid SupplierId,
    DateTime ExpectedDeliveryDate,
    List<PurchaseOrderItemDto> Items,
    Guid? UserId
) : IRequest<Guid>;

public record PurchaseOrderItemDto(
    Guid ProductId,
    decimal OrderedQuantity,
    decimal UnitCost
);

public class CreatePurchaseOrderCommandHandler : IRequestHandler<CreatePurchaseOrderCommand, Guid>
{
    private readonly IApplicationDbContext _context;

    public CreatePurchaseOrderCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Guid> Handle(CreatePurchaseOrderCommand request, CancellationToken cancellationToken)
    {
        var po = new PurchaseOrderHeader
        {
            StoreId = request.StoreId,
            SupplierId = request.SupplierId,
            PoNumber = $"PO-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString().Substring(0, 4).ToUpper()}",
            PoDate = DateTime.UtcNow,
            ExpectedDeliveryDate = request.ExpectedDeliveryDate,
            Status = "DRAFT",
            CreatedBy = request.UserId
        };

        foreach (var itemDto in request.Items)
        {
            var itemTotal = itemDto.OrderedQuantity * itemDto.UnitCost;
            po.Items.Add(new PurchaseOrderItem
            {
                ProductId = itemDto.ProductId,
                OrderedQuantity = itemDto.OrderedQuantity,
                ReceivedQuantity = 0,
                UnitCost = itemDto.UnitCost,
                TotalCost = itemTotal
            });
            po.TotalAmount += itemTotal;
        }

        _context.PurchaseOrders.Add(po);
        await _context.SaveChangesAsync(cancellationToken);

        return po.Id;
    }
}
"@ | Out-File -FilePath "$backendDir\PosErp.Application\Features\Purchasing\Commands\CreatePurchaseOrder\CreatePurchaseOrderCommand.cs" -Encoding utf8

# 2. Approve Purchase Order Command
@"
using MediatR;
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Purchasing.Commands.ApprovePurchaseOrder;

public record ApprovePurchaseOrderCommand(Guid PurchaseOrderId, Guid? UserId) : IRequest<bool>;

public class ApprovePurchaseOrderCommandHandler : IRequestHandler<ApprovePurchaseOrderCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public ApprovePurchaseOrderCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(ApprovePurchaseOrderCommand request, CancellationToken cancellationToken)
    {
        var po = await _context.PurchaseOrders.FirstOrDefaultAsync(p => p.Id == request.PurchaseOrderId, cancellationToken);
        
        if (po == null) throw new Exception("Purchase Order not found.");
        if (po.Status != "DRAFT") throw new Exception("Only DRAFT Purchase Orders can be approved.");

        po.Status = "APPROVED";
        // Optionally capture ApproverId here
        
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
"@ | Out-File -FilePath "$backendDir\PosErp.Application\Features\Purchasing\Commands\ApprovePurchaseOrder\ApprovePurchaseOrderCommand.cs" -Encoding utf8


# 3. Frontend: Purchase Order List View
@"
import React from 'react';
import { Plus, CheckCircle, Clock, Search } from 'lucide-react';

export const PurchaseOrderList = () => {
  // Mock Data
  const purchaseOrders = [
    { id: '1', poNumber: 'PO-20260515-A1B2', supplier: 'ITC Limited', date: '2026-05-15', amount: 45000, status: 'APPROVED' },
    { id: '2', poNumber: 'PO-20260514-X9Z1', supplier: 'Unilever', date: '2026-05-14', amount: 12000, status: 'DRAFT' },
    { id: '3', poNumber: 'PO-20260510-M4N5', supplier: 'Local Farms', date: '2026-05-10', amount: 8500, status: 'PARTIAL_GRN' },
  ];

  return (
    <div className="bg-white shadow rounded-lg p-6 max-w-7xl mx-auto">
      <div className="flex justify-between items-center mb-6 border-b pb-4">
        <h2 className="text-2xl font-bold text-slate-800">Purchase Orders</h2>
        <button className="px-4 py-2 bg-blue-600 text-white rounded shadow flex items-center font-bold hover:bg-blue-700">
          <Plus className="w-5 h-5 mr-2" /> New Purchase Order
        </button>
      </div>

      <div className="flex gap-4 mb-6">
        <div className="relative flex-1 max-w-md">
          <Search className="absolute left-3 top-3 text-gray-400" />
          <input type="text" placeholder="Search PO Number or Supplier..." className="w-full pl-10 p-2 border rounded" />
        </div>
        <select className="p-2 border rounded">
          <option value="">All Statuses</option>
          <option value="DRAFT">Draft</option>
          <option value="APPROVED">Approved</option>
          <option value="PARTIAL_GRN">Partial GRN</option>
        </select>
      </div>

      <div className="overflow-x-auto">
        <table className="w-full text-left border-collapse">
          <thead className="bg-slate-100 text-sm">
            <tr>
              <th className="p-3 border">PO Number</th>
              <th className="p-3 border">Supplier</th>
              <th className="p-3 border">Date</th>
              <th className="p-3 border">Total Amount</th>
              <th className="p-3 border text-center">Status</th>
              <th className="p-3 border text-center">Actions</th>
            </tr>
          </thead>
          <tbody>
            {purchaseOrders.map((po) => (
              <tr key={po.id} className="border-b hover:bg-slate-50">
                <td className="p-3 font-bold text-blue-600 cursor-pointer">{po.poNumber}</td>
                <td className="p-3 text-slate-800">{po.supplier}</td>
                <td className="p-3 text-gray-600">{po.date}</td>
                <td className="p-3 font-bold">₹{po.amount.toFixed(2)}</td>
                <td className="p-3 text-center">
                  <span className={`px-2 py-1 rounded text-xs font-bold \${
                    po.status === 'APPROVED' ? 'bg-emerald-100 text-emerald-800' :
                    po.status === 'DRAFT' ? 'bg-gray-100 text-gray-800' :
                    'bg-orange-100 text-orange-800'
                  }`}>
                    {po.status.replace('_', ' ')}
                  </span>
                </td>
                <td className="p-3 text-center">
                  {po.status === 'DRAFT' && (
                    <button className="text-emerald-600 hover:text-emerald-800 flex items-center justify-center w-full">
                      <CheckCircle className="w-4 h-4 mr-1" /> Approve
                    </button>
                  )}
                  {po.status === 'APPROVED' && (
                    <button className="text-blue-600 hover:text-blue-800 flex items-center justify-center w-full">
                      <Clock className="w-4 h-4 mr-1" /> Awaiting GRN
                    </button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
};
"@ | Out-File -FilePath "$frontendDir\src\features\purchasing\components\PurchaseOrderList.tsx" -Encoding utf8

# 4. Frontend: Purchase Order Form
@"
import React, { useState } from 'react';
import { Save, Search, Plus, Trash2 } from 'lucide-react';

export const PurchaseOrderForm = () => {
  const [items, setItems] = useState([{ productId: '', name: '', orderedQty: 1, unitCost: 0 }]);
  const [supplier, setSupplier] = useState('');

  const handleAddItem = () => setItems([...items, { productId: '', name: '', orderedQty: 1, unitCost: 0 }]);
  const handleRemoveItem = (index: number) => setItems(items.filter((_, i) => i !== index));

  const totalAmount = items.reduce((sum, item) => sum + (item.orderedQty * item.unitCost), 0);

  const handleSaveDraft = () => {
    alert("PO Saved as DRAFT");
  };

  return (
    <div className="bg-white shadow rounded-lg p-6 max-w-6xl mx-auto">
      <div className="flex justify-between items-center mb-6 border-b pb-4">
        <h2 className="text-2xl font-bold text-slate-800">Create Purchase Order</h2>
        <button onClick={handleSaveDraft} className="px-6 py-2 bg-blue-600 text-white rounded shadow flex items-center font-bold hover:bg-blue-700">
          <Save className="w-5 h-5 mr-2" /> Save Draft
        </button>
      </div>

      <div className="grid grid-cols-2 gap-6 mb-8">
        <div>
          <label className="block text-sm font-bold text-gray-700 mb-2">Supplier</label>
          <select className="w-full p-2 border rounded" value={supplier} onChange={(e) => setSupplier(e.target.value)}>
            <option value="">-- Select Supplier --</option>
            <option value="S1">ITC Limited</option>
            <option value="S2">Unilever</option>
          </select>
        </div>
        <div>
          <label className="block text-sm font-bold text-gray-700 mb-2">Expected Delivery Date</label>
          <input type="date" className="w-full p-2 border rounded" />
        </div>
      </div>

      <div className="mb-4 flex justify-between items-center">
        <h3 className="text-lg font-bold text-slate-800">Line Items</h3>
        <button onClick={handleAddItem} className="text-blue-600 font-bold flex items-center hover:text-blue-800">
          <Plus className="w-4 h-4 mr-1" /> Add Row
        </button>
      </div>

      <table className="w-full text-left border-collapse mb-6">
        <thead className="bg-slate-100 text-sm">
          <tr>
            <th className="p-3 border w-1/2">Product (Search)</th>
            <th className="p-3 border text-right w-1/6">Quantity</th>
            <th className="p-3 border text-right w-1/6">Unit Cost</th>
            <th className="p-3 border text-right w-1/6">Total</th>
            <th className="p-3 border text-center w-12"></th>
          </tr>
        </thead>
        <tbody>
          {items.map((item, idx) => (
            <tr key={idx} className="border-b">
              <td className="p-3">
                <div className="relative">
                  <Search className="absolute left-2 top-2 text-gray-400 w-4 h-4" />
                  <input type="text" placeholder="Search product..." className="w-full pl-8 p-1 border rounded text-sm" />
                </div>
              </td>
              <td className="p-3">
                <input 
                  type="number" 
                  className="w-full p-1 border rounded text-right" 
                  value={item.orderedQty} 
                  onChange={(e) => {
                    const newItems = [...items];
                    newItems[idx].orderedQty = parseFloat(e.target.value) || 0;
                    setItems(newItems);
                  }}
                />
              </td>
              <td className="p-3">
                <input 
                  type="number" 
                  className="w-full p-1 border rounded text-right" 
                  value={item.unitCost} 
                  onChange={(e) => {
                    const newItems = [...items];
                    newItems[idx].unitCost = parseFloat(e.target.value) || 0;
                    setItems(newItems);
                  }}
                />
              </td>
              <td className="p-3 text-right font-bold text-slate-700">
                ₹{(item.orderedQty * item.unitCost).toFixed(2)}
              </td>
              <td className="p-3 text-center">
                <button onClick={() => handleRemoveItem(idx)} className="text-red-500 hover:text-red-700">
                  <Trash2 className="w-5 h-5" />
                </button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>

      <div className="flex justify-end border-t pt-4">
        <div className="text-right">
          <p className="text-gray-500 font-bold mb-1">Total PO Amount</p>
          <p className="text-4xl font-black text-slate-800">₹{totalAmount.toFixed(2)}</p>
        </div>
      </div>
    </div>
  );
};
"@ | Out-File -FilePath "$frontendDir\src\features\purchasing\components\PurchaseOrderForm.tsx" -Encoding utf8

Write-Host "Purchase Order Module Scaffolded"
