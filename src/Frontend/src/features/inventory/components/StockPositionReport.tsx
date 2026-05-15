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
              <th className="p-3 border text-right">Stock Value (â‚¹)</th>
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
                <td className="p-3 text-right font-bold text-gray-600">â‚¹{item.value.toLocaleString()}</td>
              </tr>
            ))}
          </tbody>
          <tfoot className="bg-slate-50 font-bold">
            <tr>
              <td colSpan={4} className="p-3 text-right">Total Inventory Value:</td>
              <td className="p-3 text-right text-indigo-700 text-lg">â‚¹34,730.00</td>
            </tr>
          </tfoot>
        </table>
      </div>
    </div>
  );
};
