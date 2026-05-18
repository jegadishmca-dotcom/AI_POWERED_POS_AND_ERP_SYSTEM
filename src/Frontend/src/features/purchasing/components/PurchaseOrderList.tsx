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
                  <span className={`px-2 py-1 rounded text-xs font-bold ${po.status === 'APPROVED' ? 'bg-green-100 text-green-800' : po.status === 'DRAFT' ? 'bg-gray-100 text-gray-700' : 'bg-blue-100 text-blue-800'}`}>
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
