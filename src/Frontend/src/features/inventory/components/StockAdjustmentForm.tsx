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
                  className={w-full p-1 border rounded text-right \}
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
