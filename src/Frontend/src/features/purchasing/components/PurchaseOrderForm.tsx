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
                â‚¹{(item.orderedQty * item.unitCost).toFixed(2)}
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
          <p className="text-4xl font-black text-slate-800">â‚¹{totalAmount.toFixed(2)}</p>
        </div>
      </div>
    </div>
  );
};
