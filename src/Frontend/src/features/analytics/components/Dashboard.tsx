import React, { useState, useEffect } from 'react';
import { TrendingUp, ShoppingBag, Users, DollarSign, Download } from 'lucide-react';
import { LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer, BarChart, Bar } from 'recharts';

export const Dashboard = () => {
  const [kpis, setKpis] = useState({ sales: 0, orders: 0, avg: 0, growth: 0 });
  const [salesTrend, setSalesTrend] = useState([]);
  const [topProducts, setTopProducts] = useState([]);

  useEffect(() => {
    // Mock Fetching from GetTodayDashboardQuery & GetSalesAnalyticsQuery
    setKpis({ sales: 124500.50, orders: 342, avg: 364.03, growth: 12.5 });
    
    setSalesTrend([
      { name: 'Mon', NetSales: 85000, GrossSales: 92000 },
      { name: 'Tue', NetSales: 78000, GrossSales: 85000 },
      { name: 'Wed', NetSales: 91000, GrossSales: 99000 },
      { name: 'Thu', NetSales: 110000, GrossSales: 120000 },
      { name: 'Fri', NetSales: 124500, GrossSales: 135000 },
      { name: 'Sat', NetSales: 155000, GrossSales: 168000 },
      { name: 'Sun', NetSales: 172000, GrossSales: 185000 }
    ]);

    setTopProducts([
      { name: 'Aashirvaad Atta 5kg', qty: 150, rev: 45000 },
      { name: 'Tata Salt 1kg', qty: 320, rev: 6400 },
      { name: 'Fortune Sunflower Oil 1L', qty: 110, rev: 22000 },
      { name: 'Amul Taaza Milk 500ml', qty: 450, rev: 13500 },
      { name: 'Britannia Good Day', qty: 200, rev: 5000 }
    ]);
  }, []);

  const handleExport = (type: 'pdf' | 'excel') => {
    alert(`Downloading ${type} report from /api/analytics/export/${type}`);
  };

  return (
    <div className="p-8 bg-slate-50 min-h-screen">
      <div className="flex justify-between items-center mb-8">
        <div>
          <h1 className="text-3xl font-black text-slate-800">Manager Dashboard</h1>
          <p className="text-gray-500">Real-time overview of store operations</p>
        </div>
        <div className="flex gap-4">
          <button onClick={() => handleExport('pdf')} className="px-4 py-2 bg-white border border-gray-300 rounded font-bold text-gray-700 shadow-sm hover:bg-gray-50 flex items-center">
            <Download className="w-4 h-4 mr-2" /> PDF Report
          </button>
          <button onClick={() => handleExport('excel')} className="px-4 py-2 bg-emerald-600 text-white rounded font-bold shadow-sm hover:bg-emerald-700 flex items-center">
            <Download className="w-4 h-4 mr-2" /> Excel Export
          </button>
        </div>
      </div>

      {/* KPI Cards */}
      <div className="grid grid-cols-1 md:grid-cols-4 gap-6 mb-8">
        <div className="bg-white p-6 rounded-xl shadow-sm border border-slate-100 flex items-center justify-between">
          <div>
            <p className="text-sm font-bold text-gray-500 mb-1">Today's Net Sales</p>
            <h2 className="text-3xl font-black text-slate-800">₹{kpis.sales.toLocaleString()}</h2>
            <p className="text-sm font-bold mt-2 text-emerald-500">↑ {kpis.growth}% vs Yesterday</p>
          </div>
          <div className="w-12 h-12 rounded-full bg-emerald-100 flex items-center justify-center text-emerald-600">
            <DollarSign className="w-6 h-6" />
          </div>
        </div>
        
        <div className="bg-white p-6 rounded-xl shadow-sm border border-slate-100 flex items-center justify-between">
          <div>
            <p className="text-sm font-bold text-gray-500 mb-1">Total Invoices</p>
            <h2 className="text-3xl font-black text-slate-800">{kpis.orders}</h2>
          </div>
          <div className="w-12 h-12 rounded-full bg-blue-100 flex items-center justify-center text-blue-600">
            <ShoppingBag className="w-6 h-6" />
          </div>
        </div>

        <div className="bg-white p-6 rounded-xl shadow-sm border border-slate-100 flex items-center justify-between">
          <div>
            <p className="text-sm font-bold text-gray-500 mb-1">Avg Basket Value</p>
            <h2 className="text-3xl font-black text-slate-800">₹{kpis.avg.toFixed(2)}</h2>
          </div>
          <div className="w-12 h-12 rounded-full bg-purple-100 flex items-center justify-center text-purple-600">
            <TrendingUp className="w-6 h-6" />
          </div>
        </div>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-8">
        {/* Recharts Area */}
        <div className="lg:col-span-2 bg-white p-6 rounded-xl shadow-sm border border-slate-100">
          <h3 className="text-lg font-bold text-slate-800 mb-6">7-Day Sales Trend</h3>
          <div className="h-80 w-full">
            <ResponsiveContainer width="100%" height="100%">
              <LineChart data={salesTrend}>
                <CartesianGrid strokeDasharray="3 3" vertical={false} />
                <XAxis dataKey="name" axisLine={false} tickLine={false} />
                <YAxis axisLine={false} tickLine={false} tickFormatter={(value) => `₹${(value/1000).toFixed(0)}k`} />
                <Tooltip formatter={(value) => [`₹${Number(value).toLocaleString()}`, 'Sales']} />
                <Line type="monotone" dataKey="NetSales" stroke="#4f46e5" strokeWidth={3} dot={{ r: 4 }} activeDot={{ r: 8 }} />
              </LineChart>
            </ResponsiveContainer>
          </div>
        </div>

        {/* Top Products Table */}
        <div className="bg-white p-6 rounded-xl shadow-sm border border-slate-100">
          <h3 className="text-lg font-bold text-slate-800 mb-6">Top Moving Products</h3>
          <div className="space-y-4">
            {topProducts.map((prod: any, idx) => (
              <div key={idx} className="flex justify-between items-center border-b pb-3 last:border-0">
                <div>
                  <p className="font-bold text-slate-800 text-sm">{prod.name}</p>
                  <p className="text-xs text-gray-500">{prod.qty} Units Sold</p>
                </div>
                <p className="font-black text-indigo-600">₹{prod.rev.toLocaleString()}</p>
              </div>
            ))}
          </div>
        </div>
      </div>
    </div>
  );
};
