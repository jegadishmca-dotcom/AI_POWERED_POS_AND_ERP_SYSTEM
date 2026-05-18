import React, { useState } from 'react';
import { PackageCheck, Save, AlertCircle } from 'lucide-react';
import { useQuery } from '@tanstack/react-query';
import { api } from '@/utils/api';

export const GrnForm = () => {
  const [selectedPo, setSelectedPo] = useState<string | null>(null);
  const [grnItems, setGrnItems] = useState<any[]>([]);

  // Mock fetching PO lines for the selected PO
  const fetchPoLines = (poId: string) => {
    // In real app, fetch from API. We mock it for the UI structure.
    setGrnItems([
      { id: '1', productId: 'p1', name: 'Aashirvaad Atta 5kg', ordered: 100, received: 0, pending: 100, accepted: 0, rejected: 0, rejectionReason: '', batch: '', expiry: '', hasExpiry: true, unitCost: 200 },
      { id: '2', productId: 'p2', name: 'Tata Salt 1kg', ordered: 50, received: 0, pending: 50, accepted: 0, rejected: 0, rejectionReason: '', batch: '', expiry: '', hasExpiry: false, unitCost: 20 },
    ]);
  };

  const handleQuantityChange = (idx: number, field: string, value: any) => {
    const updated = [...grnItems];
    updated[idx][field] = value;
    setGrnItems(updated);
  };

  const handleConfirmGrn = async () => {
    // Validate Expiry for hasExpiry items
    const invalidItem = grnItems.find(i => i.hasExpiry && i.accepted > 0 && !i.expiry);
    if (invalidItem) {
      alert(`Expiry Date is mandatory for ${invalidItem.name}`);
      return;
    }

    try {
      // await api.post('/api/purchasing/confirm-grn', { poId: selectedPo, items: grnItems });
      alert('GRN Confirmed and Stock Ledger updated successfully!');
    } catch (e) {
      console.error(e);
    }
  };

  return (
    <div className="bg-white shadow rounded-lg p-6 max-w-7xl mx-auto">
      <div className="flex justify-between items-center mb-6 border-b pb-4">
        <h2 className="text-2xl font-bold text-slate-800 flex items-center">
          <PackageCheck className="mr-3 text-emerald-600" /> Goods Receipt Note (GRN)
        </h2>
        <button 
          onClick={handleConfirmGrn}
          className="px-6 py-2 bg-emerald-600 text-white rounded shadow flex items-center font-bold hover:bg-emerald-700"
        >
          <Save className="w-5 h-5 mr-2" /> Confirm GRN
        </button>
      </div>

      <div className="mb-6 grid grid-cols-3 gap-6">
        <div>
          <label className="block text-sm font-bold text-gray-700 mb-2">Select Purchase Order</label>
          <select 
            className="w-full p-2 border rounded"
            onChange={(e) => {
              setSelectedPo(e.target.value);
              fetchPoLines(e.target.value);
            }}
          >
            <option value="">-- Select PO --</option>
            <option value="PO-001">PO-001 (ITC Limited)</option>
            <option value="PO-002">PO-002 (Unilever)</option>
          </select>
        </div>
        <div>
          <label className="block text-sm font-bold text-gray-700 mb-2">Supplier Invoice No.</label>
          <input type="text" className="w-full p-2 border rounded" placeholder="Enter Invoice No." />
        </div>
        <div>
          <label className="block text-sm font-bold text-gray-700 mb-2">Received Date</label>
          <input type="date" className="w-full p-2 border rounded" defaultValue={new Date().toISOString().slice(0,10)} />
        </div>
      </div>

      {grnItems.length > 0 && (
        <div className="overflow-x-auto">
          <table className="w-full text-left border-collapse">
            <thead className="bg-slate-100 text-sm">
              <tr>
                <th className="p-3 border">Product</th>
                <th className="p-3 border text-center">Pending Qty</th>
                <th className="p-3 border bg-blue-50 text-center text-blue-800">Accepted Qty</th>
                <th className="p-3 border bg-red-50 text-center text-red-800">Rejected Qty</th>
                <th className="p-3 border w-48">Batch / Expiry</th>
              </tr>
            </thead>
            <tbody>
              {grnItems.map((item, idx) => (
                <tr key={item.id} className="border-b">
                  <td className="p-3">
                    <p className="font-bold">{item.name}</p>
                    <p className="text-xs text-gray-500">Cost: â‚¹{item.unitCost.toFixed(2)}</p>
                  </td>
                  <td className="p-3 text-center text-lg font-bold text-gray-600">{item.pending}</td>
                  <td className="p-3 bg-blue-50/30">
                    <input 
                      type="number" 
                      className="w-full p-2 border border-blue-200 rounded text-center" 
                      value={item.accepted} 
                      onChange={(e) => handleQuantityChange(idx, 'accepted', parseFloat(e.target.value) || 0)}
                    />
                  </td>
                  <td className="p-3 bg-red-50/30">
                    <input 
                      type="number" 
                      className="w-full p-2 border border-red-200 rounded text-center mb-1" 
                      value={item.rejected} 
                      onChange={(e) => handleQuantityChange(idx, 'rejected', parseFloat(e.target.value) || 0)}
                    />
                    {item.rejected > 0 && (
                      <input 
                        type="text" 
                        placeholder="Reason" 
                        className="w-full p-1 text-xs border border-red-200 rounded"
                        onChange={(e) => handleQuantityChange(idx, 'rejectionReason', e.target.value)}
                      />
                    )}
                  </td>
                  <td className="p-3">
                    <input 
                      type="text" 
                      placeholder="Batch No (Optional)" 
                      className="w-full p-2 border rounded text-sm mb-2"
                      value={item.batch}
                      onChange={(e) => handleQuantityChange(idx, 'batch', e.target.value)}
                    />
                    <div className="flex items-center">
                      <input 
                        type="date" 
                        className={`w-full p-2 border rounded text-sm ${item.hasExpiry ? 'border-orange-300' : ''}`}
                        value={item.expiry}
                        onChange={(e) => handleQuantityChange(idx, 'expiry', e.target.value)}
                      />
                      {item.hasExpiry && !item.expiry && <AlertCircle className="w-4 h-4 text-orange-500 ml-1" title="Expiry Date is mandatory" />}
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
};
