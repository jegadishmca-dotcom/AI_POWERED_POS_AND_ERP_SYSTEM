import React, { useState, useEffect } from 'react';
import { TrendingUp, ShoppingBag, Users, DollarSign, Download } from 'lucide-react';
import { LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer } from 'recharts';
import { getDashboardKpis, getSalesTrend, getTopProducts } from '../api/analytics.api';
import { api } from '../../../utils/api';

export const Dashboard = () => {
  const [kpis, setKpis] = useState({ sales: 0, orders: 0, avg: 0, growth: 0 });
  const [salesTrend, setSalesTrend] = useState<{ name: string; NetSales: number; GrossSales: number }[]>([]);
  const [topProducts, setTopProducts] = useState<{ name: string; qty: number; rev: number }[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const fetchDashboardData = async () => {
      try {
        setLoading(true);
        setError(null);
        
        const [kpiData, trendData, productsData] = await Promise.all([
          getDashboardKpis(),
          getSalesTrend(7),
          getTopProducts()
        ]);

        setKpis({
          sales: kpiData.todaySales,
          orders: kpiData.todayOrders,
          avg: kpiData.avgOrderValue,
          growth: kpiData.salesGrowthPercentage
        });

        setSalesTrend(
          trendData.map(item => ({
            name: item.date,
            NetSales: item.netSales,
            GrossSales: item.grossSales
          }))
        );

        setTopProducts(
          productsData.map(item => ({
            name: item.productName,
            qty: item.totalQuantitySold,
            rev: item.totalRevenue
          }))
        );
      } catch (err: any) {
        console.error('Failed to fetch dashboard analytics:', err);
        setError('Failed to load store analytics data. Please try again.');
      } finally {
        setLoading(false);
      }
    };

    fetchDashboardData();
  }, []);

  const handleExport = async (type: 'pdf' | 'excel') => {
    try {
      const response = await api.get(`/api/analytics/export/${type}`, { responseType: 'blob' });
      const blob = new Blob([response.data], { 
        type: type === 'pdf' 
          ? 'application/pdf' 
          : 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' 
      });
      const link = document.createElement('a');
      link.href = URL.createObjectURL(blob);
      link.setAttribute('download', type === 'pdf' ? 'DailySalesReport.pdf' : 'SalesAnalytics.xlsx');
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);
    } catch (err) {
      console.error(`Failed to export ${type} report:`, err);
      alert(`Failed to export ${type} report. Please check your permissions and try again.`);
    }
  };

  if (loading) {
    return (
      <div className="p-8 bg-slate-50 min-h-screen flex flex-col justify-center items-center">
        <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-indigo-600 mb-4"></div>
        <p className="text-gray-500 font-medium animate-pulse">Loading store analytics...</p>
      </div>
    );
  }

  if (error) {
    return (
      <div className="p-8 bg-slate-50 min-h-screen flex flex-col justify-center items-center">
        <div className="bg-red-50 border border-red-200 text-red-700 px-6 py-5 rounded-xl shadow-sm max-w-md text-center">
          <h3 className="font-bold text-lg mb-2">Error Loading Analytics</h3>
          <p className="text-sm text-red-600 mb-4">{error}</p>
          <button 
            onClick={() => window.location.reload()} 
            className="px-6 py-2 bg-red-600 hover:bg-red-700 text-white rounded-lg font-bold text-sm transition shadow-sm"
          >
            Retry
          </button>
        </div>
      </div>
    );
  }

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
      <div className="grid grid-cols-1 md:grid-cols-3 gap-6 mb-8">
        <div className="bg-white p-6 rounded-xl shadow-sm border border-slate-100 flex items-center justify-between">
          <div>
            <p className="text-sm font-bold text-gray-500 mb-1">Today's Net Sales</p>
            <h2 className="text-3xl font-black text-slate-800">₹{kpis.sales.toLocaleString()}</h2>
            <p className={`text-sm font-bold mt-2 ${kpis.growth >= 0 ? 'text-emerald-500' : 'text-rose-500'}`}>
              {kpis.growth >= 0 ? '↑' : '↓'} {Math.abs(kpis.growth).toFixed(1)}% vs Yesterday
            </p>
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
          {topProducts.length === 0 ? (
            <div className="h-60 flex items-center justify-center text-gray-400 font-medium">
              No sales recorded yet.
            </div>
          ) : (
            <div className="space-y-4">
              {topProducts.map((prod: any, idx) => (
                <div key={idx} className="flex justify-between items-center border-b pb-3 last:border-0">
                  <div className="max-w-[70%]">
                    <p className="font-bold text-slate-800 text-sm truncate" title={prod.name}>{prod.name}</p>
                    <p className="text-xs text-gray-500">{prod.qty} Units Sold</p>
                  </div>
                  <p className="font-black text-indigo-600">₹{prod.rev.toLocaleString()}</p>
                </div>
              ))}
            </div>
          )}
        </div>
      </div>
    </div>
  );
};
