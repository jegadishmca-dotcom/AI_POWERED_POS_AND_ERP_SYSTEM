import React, { useState, useEffect } from 'react';
import { AreaChart, Area, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer, BarChart, Bar, Legend, PieChart, Pie, Cell } from 'recharts';

export function ProcurementDashboard() {
  const [recommendations, setRecommendations] = useState<any[]>([]);

  useEffect(() => {
    // Mocking procurement data based on the newly designed IPurchaseRecommendationEngine
    setRecommendations([
      { ProductId: '1', ProductName: 'Apple Premium', CurrentStock: 50, RecommendedQuantity: 500, Priority: 'Critical', DaysUntilStockout: 2, SupplierName: 'Fresh Farms Ltd' },
      { ProductId: '2', ProductName: 'Milk 1L', CurrentStock: 120, RecommendedQuantity: 1000, Priority: 'Critical', DaysUntilStockout: 4, SupplierName: 'Dairy Co' },
      { ProductId: '3', ProductName: 'Whole Wheat Bread', CurrentStock: 40, RecommendedQuantity: 200, Priority: 'High', DaysUntilStockout: 9, SupplierName: 'Daily Bakes' },
      { ProductId: '4', ProductName: 'Basmati Rice 5kg', CurrentStock: 80, RecommendedQuantity: 300, Priority: 'Medium', DaysUntilStockout: 22, SupplierName: 'Agro Suppliers' },
      { ProductId: '5', ProductName: 'Olive Oil 1L', CurrentStock: 15, RecommendedQuantity: 60, Priority: 'Low', DaysUntilStockout: 45, SupplierName: 'Premium Imports' },
    ]);
  }, []);

  const getPriorityBadge = (priority: string) => {
    switch (priority) {
      case 'Critical': return <span className="px-2 py-1 bg-red-900 text-red-300 text-xs font-medium rounded-full border border-red-700">Critical (≤ 7 days)</span>;
      case 'High': return <span className="px-2 py-1 bg-orange-900 text-orange-300 text-xs font-medium rounded-full border border-orange-700">High (≤ 14 days)</span>;
      case 'Medium': return <span className="px-2 py-1 bg-yellow-900 text-yellow-300 text-xs font-medium rounded-full border border-yellow-700">Medium (≤ 30 days)</span>;
      default: return <span className="px-2 py-1 bg-slate-700 text-slate-300 text-xs font-medium rounded-full border border-slate-600">Low Monitor</span>;
    }
  };

  return (
    <div className="p-6 space-y-6 bg-slate-900 min-h-screen text-slate-200">
      <div className="flex justify-between items-center">
        <h1 className="text-2xl font-bold text-white">Procurement Intelligence</h1>
        <button className="bg-blue-600 hover:bg-blue-700 text-white px-4 py-2 rounded-lg text-sm font-medium transition-colors">
          Generate Draft POs
        </button>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
        <div className="bg-slate-800 p-4 rounded-xl border border-slate-700 shadow-sm relative overflow-hidden">
          <div className="absolute top-0 left-0 w-full h-1 bg-red-500"></div>
          <p className="text-sm text-slate-400 mb-1">Critical Reorders</p>
          <p className="text-3xl font-bold text-red-500">12</p>
          <p className="text-xs text-slate-500 mt-2">Action required today</p>
        </div>
        <div className="bg-slate-800 p-4 rounded-xl border border-slate-700 shadow-sm relative overflow-hidden">
          <div className="absolute top-0 left-0 w-full h-1 bg-orange-500"></div>
          <p className="text-sm text-slate-400 mb-1">High Priority Reorders</p>
          <p className="text-3xl font-bold text-orange-500">28</p>
          <p className="text-xs text-slate-500 mt-2">Stockout in 8-14 days</p>
        </div>
        <div className="bg-slate-800 p-4 rounded-xl border border-slate-700 shadow-sm relative overflow-hidden">
          <div className="absolute top-0 left-0 w-full h-1 bg-blue-500"></div>
          <p className="text-sm text-slate-400 mb-1">Pending Purchase Orders</p>
          <p className="text-3xl font-bold text-blue-500">5</p>
          <p className="text-xs text-slate-500 mt-2">Awaiting supplier confirmation</p>
        </div>
      </div>

      <div className="bg-slate-800 rounded-xl border border-slate-700 overflow-hidden">
        <div className="px-6 py-4 border-b border-slate-700 flex justify-between items-center bg-slate-800">
          <h3 className="text-lg font-semibold text-white">Smart Reorder Recommendations</h3>
          <span className="text-sm text-slate-400">Calculated using 30-day velocity + EOQ</span>
        </div>
        <div className="overflow-x-auto">
          <table className="w-full text-sm text-left text-slate-300">
            <thead className="text-xs text-slate-400 uppercase bg-slate-900/50">
              <tr>
                <th className="px-6 py-3 font-medium">Product</th>
                <th className="px-6 py-3 font-medium">Priority</th>
                <th className="px-6 py-3 font-medium text-right">Current Stock</th>
                <th className="px-6 py-3 font-medium text-right text-blue-400">Recommended Qty</th>
                <th className="px-6 py-3 font-medium">Est. Stockout</th>
                <th className="px-6 py-3 font-medium">Preferred Supplier</th>
                <th className="px-6 py-3 text-right">Action</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-700/50">
              {recommendations.map((item, idx) => (
                <tr key={idx} className="hover:bg-slate-700/30 transition-colors">
                  <td className="px-6 py-4 font-medium text-white">{item.ProductName}</td>
                  <td className="px-6 py-4">{getPriorityBadge(item.Priority)}</td>
                  <td className="px-6 py-4 text-right">{item.CurrentStock}</td>
                  <td className="px-6 py-4 text-right font-bold text-blue-400">{item.RecommendedQuantity}</td>
                  <td className="px-6 py-4">{item.DaysUntilStockout} days</td>
                  <td className="px-6 py-4 text-slate-400">{item.SupplierName}</td>
                  <td className="px-6 py-4 text-right">
                    <button className="text-blue-500 hover:text-blue-400 font-medium text-xs border border-blue-500/30 hover:border-blue-400 px-3 py-1 rounded">
                      Add to PO
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}
