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
                <td className={p-3 text-center font-bold \}>
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
