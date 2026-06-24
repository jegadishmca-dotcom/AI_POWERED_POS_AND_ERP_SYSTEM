import React, { useState, useEffect } from 'react';
import { AreaChart, Area, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer, BarChart, Bar, Legend, PieChart, Pie, Cell } from 'recharts';

export function InventoryIntelligenceDashboard() {
  const [healthData, setHealthData] = useState<any>(null);
  const [fastMovers, setFastMovers] = useState<any[]>([]);
  const [slowMovers, setSlowMovers] = useState<any[]>([]);
  const [expiryData, setExpiryData] = useState<any>(null);

  useEffect(() => {
    // In a real app, these would be fetched from our new APIs
    // Mocking data for the wireframe based on the new API responses
    setHealthData({
      InventoryValue: 1250000,
      InventoryHealthScore: 82,
      DeadStockValue: 45000,
      ExpiryRiskValue: 12000,
      ReorderValue: 85000
    });

    setFastMovers([
      { ProductName: 'Apple Premium', TotalSold: 1200, TotalRevenue: 240000 },
      { ProductName: 'Milk 1L', TotalSold: 950, TotalRevenue: 57000 },
      { ProductName: 'Bread Large', TotalSold: 820, TotalRevenue: 32800 },
      { ProductName: 'Eggs 12pk', TotalSold: 780, TotalRevenue: 62400 },
      { ProductName: 'Rice 5kg', TotalSold: 450, TotalRevenue: 225000 }
    ]);

    setExpiryData([
      { name: '0-7 Days', value: 3500 },
      { name: '8-30 Days', value: 8500 },
      { name: '31-60 Days', value: 15000 },
      { name: '61-90 Days', value: 45000 }
    ]);
  }, []);

  const COLORS = ['#ef4444', '#f97316', '#eab308', '#22c55e'];

  if (!healthData) return <div className="p-8 flex justify-center"><div className="animate-spin h-8 w-8 border-4 border-blue-500 rounded-full border-t-transparent"></div></div>;

  const getHealthColor = (score: number) => {
    if (score >= 90) return 'text-green-500';
    if (score >= 75) return 'text-blue-500';
    if (score >= 60) return 'text-yellow-500';
    return 'text-red-500';
  };

  return (
    <div className="p-6 space-y-6 bg-slate-900 min-h-screen text-slate-200">
      <div className="flex justify-between items-center">
        <h1 className="text-2xl font-bold text-white">Inventory Intelligence</h1>
        <select className="bg-slate-800 border border-slate-700 text-sm rounded-lg focus:ring-blue-500 focus:border-blue-500 block p-2.5">
          <option>All Stores</option>
          <option>Main Warehouse</option>
          <option>Store #1</option>
        </select>
      </div>

      {/* KPI Cards */}
      <div className="grid grid-cols-1 md:grid-cols-5 gap-4">
        <div className="bg-slate-800 p-4 rounded-xl border border-slate-700 shadow-sm relative overflow-hidden">
          <div className="absolute top-0 left-0 w-full h-1 bg-blue-500"></div>
          <p className="text-sm text-slate-400 mb-1">Health Score</p>
          <p className={`text-3xl font-bold ${getHealthColor(healthData.InventoryHealthScore)}`}>{healthData.InventoryHealthScore}/100</p>
          <p className="text-xs text-slate-500 mt-2">Weighted across 4 dimensions</p>
        </div>
        <div className="bg-slate-800 p-4 rounded-xl border border-slate-700 shadow-sm">
          <p className="text-sm text-slate-400 mb-1">Total Value</p>
          <p className="text-2xl font-bold text-white">₹{(healthData.InventoryValue / 100000).toFixed(2)}L</p>
        </div>
        <div className="bg-slate-800 p-4 rounded-xl border border-slate-700 shadow-sm relative overflow-hidden">
          <div className="absolute top-0 left-0 w-full h-1 bg-red-500"></div>
          <p className="text-sm text-slate-400 mb-1">Dead Stock</p>
          <p className="text-2xl font-bold text-red-400">₹{(healthData.DeadStockValue / 1000).toFixed(1)}K</p>
        </div>
        <div className="bg-slate-800 p-4 rounded-xl border border-slate-700 shadow-sm relative overflow-hidden">
          <div className="absolute top-0 left-0 w-full h-1 bg-orange-500"></div>
          <p className="text-sm text-slate-400 mb-1">Expiry Risk (90d)</p>
          <p className="text-2xl font-bold text-orange-400">₹{(healthData.ExpiryRiskValue / 1000).toFixed(1)}K</p>
        </div>
        <div className="bg-slate-800 p-4 rounded-xl border border-slate-700 shadow-sm relative overflow-hidden">
          <div className="absolute top-0 left-0 w-full h-1 bg-emerald-500"></div>
          <p className="text-sm text-slate-400 mb-1">Reorder Value</p>
          <p className="text-2xl font-bold text-emerald-400">₹{(healthData.ReorderValue / 1000).toFixed(1)}K</p>
        </div>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        {/* Fast Movers Chart */}
        <div className="bg-slate-800 p-6 rounded-xl border border-slate-700">
          <h3 className="text-lg font-semibold text-white mb-4">Fast Moving Items (Last 30 Days)</h3>
          <div className="h-72">
            <ResponsiveContainer width="100%" height="100%">
              <BarChart data={fastMovers} layout="vertical" margin={{ top: 5, right: 30, left: 20, bottom: 5 }}>
                <CartesianGrid strokeDasharray="3 3" stroke="#334155" horizontal={true} vertical={false} />
                <XAxis type="number" stroke="#94a3b8" />
                <YAxis dataKey="ProductName" type="category" width={100} stroke="#94a3b8" fontSize={12} />
                <Tooltip 
                  contentStyle={{ backgroundColor: '#1e293b', borderColor: '#334155', color: '#f8fafc' }}
                  itemStyle={{ color: '#60a5fa' }}
                />
                <Bar dataKey="TotalSold" fill="#3b82f6" radius={[0, 4, 4, 0]} />
              </BarChart>
            </ResponsiveContainer>
          </div>
        </div>

        {/* Expiry Intelligence */}
        <div className="bg-slate-800 p-6 rounded-xl border border-slate-700">
          <h3 className="text-lg font-semibold text-white mb-4">Expiry Intelligence</h3>
          <div className="flex items-center h-72">
            <div className="w-1/2 h-full">
              <ResponsiveContainer width="100%" height="100%">
                <PieChart>
                  <Pie
                    data={expiryData}
                    cx="50%"
                    cy="50%"
                    innerRadius={60}
                    outerRadius={90}
                    paddingAngle={5}
                    dataKey="value"
                  >
                    {expiryData?.map((entry: any, index: number) => (
                      <Cell key={`cell-${index}`} fill={COLORS[index % COLORS.length]} />
                    ))}
                  </Pie>
                  <Tooltip contentStyle={{ backgroundColor: '#1e293b', borderColor: '#334155', color: '#f8fafc' }} />
                </PieChart>
              </ResponsiveContainer>
            </div>
            <div className="w-1/2 space-y-4">
              {expiryData?.map((item: any, idx: number) => (
                <div key={idx} className="flex justify-between items-center">
                  <div className="flex items-center">
                    <div className="w-3 h-3 rounded-full mr-2" style={{ backgroundColor: COLORS[idx] }}></div>
                    <span className="text-sm text-slate-300">{item.name}</span>
                  </div>
                  <span className="font-semibold text-white">₹{item.value.toLocaleString()}</span>
                </div>
              ))}
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
