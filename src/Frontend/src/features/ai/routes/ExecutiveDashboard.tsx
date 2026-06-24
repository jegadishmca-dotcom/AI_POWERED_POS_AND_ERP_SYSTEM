import React, { useEffect, useState } from 'react';
import { executiveDashboardApi, aiInsightsApi, alertCenterApi } from '../api/executiveAiApi';
import { LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip as RechartsTooltip, ResponsiveContainer, AreaChart, Area } from 'recharts';
import { DollarSign, TrendingUp, Package, Users, AlertTriangle, Download, Filter, Brain } from 'lucide-react';

export const ExecutiveDashboard: React.FC = () => {
  const [kpis, setKpis] = useState<any>(null);
  const [trends, setTrends] = useState<any[]>([]);
  const [insights, setInsights] = useState<any[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const fetchData = async () => {
      try {
        const [kpiData, trendData, insightData] = await Promise.all([
          executiveDashboardApi.getKpis(),
          executiveDashboardApi.getTrends(7),
          aiInsightsApi.getInsights('New')
        ]);
        setKpis(kpiData);
        setTrends(trendData);
        setInsights(insightData.slice(0, 5));
      } catch (error) {
        console.error('Error fetching dashboard data:', error);
      } finally {
        setLoading(false);
      }
    };
    fetchData();
  }, []);

  if (loading) return <div className="p-8 text-center text-gray-500 animate-pulse">Loading Intelligence...</div>;

  return (
    <div className="p-6 bg-gray-50 min-h-screen">
      <div className="flex justify-between items-center mb-8">
        <div>
          <h1 className="text-3xl font-bold text-gray-900 tracking-tight">Executive Intelligence</h1>
          <p className="text-gray-500 mt-1">AI-Powered Business Overview</p>
        </div>
        <div className="flex space-x-3">
          <button className="flex items-center px-4 py-2 bg-white border border-gray-300 rounded-lg text-sm font-medium text-gray-700 hover:bg-gray-50 shadow-sm transition-all">
            <Filter className="w-4 h-4 mr-2" />
            Filters
          </button>
          <button className="flex items-center px-4 py-2 bg-indigo-600 text-white rounded-lg text-sm font-medium hover:bg-indigo-700 shadow-sm shadow-indigo-200 transition-all">
            <Download className="w-4 h-4 mr-2" />
            Export Report
          </button>
        </div>
      </div>

      {/* KPI Cards */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6 mb-8">
        <KpiCard 
          title="Daily Revenue" 
          value={`₹${kpis?.dailySales?.toLocaleString() || '0'}`} 
          trend="+12%" 
          icon={<DollarSign className="w-6 h-6 text-green-600" />} 
          color="bg-green-100" 
        />
        <KpiCard 
          title="Daily Profit" 
          value={`₹${kpis?.dailyProfit?.toLocaleString() || '0'}`} 
          trend="+8%" 
          icon={<TrendingUp className="w-6 h-6 text-blue-600" />} 
          color="bg-blue-100" 
        />
        <KpiCard 
          title="Inventory Health" 
          value={kpis?.totalInventoryValue ? '85/100' : 'N/A'} 
          subtitle={`₹${kpis?.totalInventoryValue?.toLocaleString() || '0'} Value`}
          trend="Stable" 
          icon={<Package className="w-6 h-6 text-indigo-600" />} 
          color="bg-indigo-100" 
        />
        <KpiCard 
          title="Loyalty Engagement" 
          value={kpis?.activeLoyaltyMembers?.toLocaleString() || '0'} 
          subtitle="Active VIP Members"
          trend="+5%" 
          icon={<Users className="w-6 h-6 text-purple-600" />} 
          color="bg-purple-100" 
        />
      </div>

      {/* Charts & AI Summary */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <div className="col-span-2 bg-white p-6 rounded-xl shadow-sm border border-gray-100">
          <h2 className="text-lg font-semibold text-gray-900 mb-6">Revenue & Profit Trends</h2>
          <div className="h-80">
            <ResponsiveContainer width="100%" height="100%">
              <AreaChart data={trends} margin={{ top: 10, right: 30, left: 0, bottom: 0 }}>
                <defs>
                  <linearGradient id="colorSales" x1="0" y1="0" x2="0" y2="1">
                    <stop offset="5%" stopColor="#4F46E5" stopOpacity={0.1}/>
                    <stop offset="95%" stopColor="#4F46E5" stopOpacity={0}/>
                  </linearGradient>
                </defs>
                <CartesianGrid strokeDasharray="3 3" vertical={false} stroke="#E5E7EB" />
                <XAxis dataKey="snapshotDate" tickFormatter={(val) => new Date(val).toLocaleDateString()} stroke="#9CA3AF" fontSize={12} />
                <YAxis stroke="#9CA3AF" fontSize={12} />
                <RechartsTooltip 
                  contentStyle={{ borderRadius: '8px', border: 'none', boxShadow: '0 4px 6px -1px rgba(0, 0, 0, 0.1)' }}
                  labelFormatter={(val) => new Date(val).toLocaleDateString()}
                />
                <Area type="monotone" dataKey="dailySales" stroke="#4F46E5" strokeWidth={3} fillOpacity={1} fill="url(#colorSales)" />
              </AreaChart>
            </ResponsiveContainer>
          </div>
        </div>

        <div className="col-span-1 bg-white p-6 rounded-xl shadow-sm border border-gray-100">
          <div className="flex items-center justify-between mb-6">
            <h2 className="text-lg font-semibold text-gray-900 flex items-center">
              <Brain className="w-5 h-5 text-indigo-500 mr-2" />
              AI Summary
            </h2>
            <span className="bg-indigo-50 text-indigo-700 text-xs px-2 py-1 rounded-full font-medium">Top 5</span>
          </div>
          <div className="space-y-4">
            {insights.length > 0 ? insights.map((insight, idx) => (
              <div key={idx} className="p-4 rounded-lg bg-gray-50 border border-gray-100 hover:shadow-md transition-shadow cursor-pointer">
                <div className="flex justify-between items-start mb-2">
                  <span className={`text-xs font-bold px-2 py-1 rounded ${insight.insightCategory === 'Risk' ? 'bg-red-100 text-red-700' : 'bg-green-100 text-green-700'}`}>
                    {insight.insightCategory}
                  </span>
                  <span className="text-xs text-gray-500 font-medium">Impact: {insight.impactScore}/100</span>
                </div>
                <h3 className="text-sm font-semibold text-gray-900 mb-1">{insight.title}</h3>
                <p className="text-xs text-gray-600 line-clamp-2">{insight.description}</p>
              </div>
            )) : (
              <p className="text-sm text-gray-500 text-center py-8">No critical AI insights pending.</p>
            )}
          </div>
        </div>
      </div>
    </div>
  );
};

const KpiCard = ({ title, value, subtitle, trend, icon, color }: any) => (
  <div className="bg-white p-6 rounded-xl shadow-sm border border-gray-100 hover:shadow-md transition-shadow">
    <div className="flex justify-between items-start">
      <div>
        <p className="text-sm font-medium text-gray-500 mb-1">{title}</p>
        <h3 className="text-3xl font-bold text-gray-900 tracking-tight">{value}</h3>
        {subtitle && <p className="text-xs text-gray-400 mt-1">{subtitle}</p>}
      </div>
      <div className={`p-3 rounded-lg ${color}`}>
        {icon}
      </div>
    </div>
    <div className="mt-4 flex items-center">
      <span className={`text-sm font-medium ${trend.startsWith('+') ? 'text-green-600' : 'text-gray-500'}`}>
        {trend}
      </span>
      <span className="text-sm text-gray-400 ml-2">vs last period</span>
    </div>
  </div>
);
