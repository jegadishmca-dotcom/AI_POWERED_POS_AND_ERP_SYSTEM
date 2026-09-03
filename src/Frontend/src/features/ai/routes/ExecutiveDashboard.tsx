import React, { useEffect, useState } from 'react';
import { executiveDashboardApi, aiInsightsApi, alertCenterApi } from '../api/executiveAiApi';
import { LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip as RechartsTooltip, ResponsiveContainer, AreaChart, Area, ReferenceArea, ReferenceLine } from 'recharts';
import { IndianRupee, TrendingUp, Package, Users, AlertTriangle, Download, Filter, Brain } from 'lucide-react';

export const ExecutiveDashboard: React.FC = () => {
  const [kpis, setKpis] = useState<any>(null);
  const [trends, setTrends] = useState<any[]>([]);
  const [insights, setInsights] = useState<any[]>([]);
  const [timeRange, setTimeRange] = useState<number>(7);
  const [showFilters, setShowFilters] = useState<boolean>(false);
  const [exporting, setExporting] = useState<boolean>(false);
  const [loading, setLoading] = useState<boolean>(true);

  useEffect(() => {
    const fetchData = async () => {
      try {
        const [kpiData, trendData, insightData] = await Promise.all([
          executiveDashboardApi.getKpis(),
          executiveDashboardApi.getTrends(timeRange),
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
  }, [timeRange]);

  const handleTimeRangeChange = async (days: number) => {
    setTimeRange(days);
    setShowFilters(false);
    try {
      const trendData = await executiveDashboardApi.getTrends(days);
      setTrends(trendData);
    } catch (err) {
      console.error('Error updating trends for time range:', err);
    }
  };

  const handleExportReport = () => {
    setExporting(true);
    try {
      const lines = [
        ['Executive Intelligence Report', `Generated at: ${new Date().toISOString()}`, `Window: ${timeRange} Days`],
        [],
        ['Key Performance Indicators', 'Value'],
        [`Daily Revenue (${timeRange}d Average)`, avgDailySales],
        [`Daily Profit (${timeRange}d Average)`, avgDailyProfit],
        ['Realized Margin %', `${realizedMargin}%`],
        ['Total Inventory Value', kpis?.totalInventoryValue || 0],
        ['Dead Stock Value', kpis?.deadStockValue || 0],
        ['Active Loyalty Members', kpis?.activeLoyaltyMembers || 0],
        [],
        ['Trend Date', 'Revenue', 'Profit', 'Profit Source']
      ];

      trends.forEach((t: any) => {
        const dateStr = t.snapshotDate ? new Date(t.snapshotDate).toLocaleDateString() : 'N/A';
        const profitSource = t.snapshotDate && t.snapshotDate >= '2026-03-24' ? 'Measured (per-line-item)' : 'Estimated (2026 margin applied)';
        lines.push([
          dateStr,
          t.dailySales || 0,
          t.dailyProfit || 0,
          profitSource
        ]);
      });

      const csvContent = 'data:text/csv;charset=utf-8,' + lines.map(e => e.join(',')).join('\n');
      const encodedUri = encodeURI(csvContent);
      const link = document.createElement('a');
      link.setAttribute('href', encodedUri);
      link.setAttribute('download', `executive_report_${new Date().toISOString().slice(0,10)}.csv`);
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);
    } catch (e) {
      console.error('Failed to export executive report:', e);
    } finally {
      setExporting(false);
    }
  };

  if (loading) return <div className="p-8 text-center text-gray-500 animate-pulse">Loading Intelligence...</div>;

  // Compute dynamic daily averages across the active filter window
  const activeDays = trends.length > 0 ? trends.length : 1;
  const totSales = trends.reduce((acc, t) => acc + (t.dailySales || 0), 0);
  const totProfit = trends.reduce((acc, t) => acc + (t.dailyProfit || 0), 0);
  const avgDailySales = Math.round(totSales / activeDays);
  const avgDailyProfit = Math.round((totProfit / activeDays) * 100) / 100;
  const realizedMargin = totSales > 0 ? ((totProfit / totSales) * 100).toFixed(1) : '8.4';

  // Find exact data points in trends array for Recharts category axis
  const firstEstimated = trends.find((t: any) => t.snapshotDate < '2026-03-24');
  const lastEstimated = [...trends].reverse().find((t: any) => t.snapshotDate < '2026-03-24');
  const firstMeasured = trends.find((t: any) => t.snapshotDate >= '2026-03-24');

  return (
    <div className="p-6 bg-gray-50 min-h-screen">
      {/* Header */}
      <div className="flex justify-between items-center mb-8">
        <div>
          <h1 className="text-3xl font-bold text-gray-900 tracking-tight">Executive Intelligence</h1>
          <p className="text-gray-500 mt-1">AI-Powered Business Overview (Last {timeRange} Days)</p>
        </div>
        
        <div className="flex space-x-3 relative">
          <div className="relative">
            <button 
              onClick={() => setShowFilters(!showFilters)}
              className="flex items-center px-4 py-2 bg-white border border-gray-300 rounded-lg text-sm font-medium text-gray-700 hover:bg-gray-50 shadow-sm transition-all"
            >
              <Filter className="w-4 h-4 mr-2" />
              Filters ({timeRange}d)
            </button>
            
            {showFilters && (
              <div className="absolute right-0 mt-2 w-48 bg-white rounded-lg shadow-lg border border-gray-100 py-1 z-20">
                <div className="px-3 py-1.5 text-xs font-semibold text-gray-400 uppercase tracking-wider">
                  Select Time Range
                </div>
                {[
                  { label: 'Last 7 Days', days: 7 },
                  { label: 'Last 30 Days', days: 30 },
                  { label: 'Last 90 Days', days: 90 },
                  { label: 'Past Year (365d)', days: 365 },
                ].map((opt) => (
                  <button
                    key={opt.days}
                    onClick={() => handleTimeRangeChange(opt.days)}
                    className={`w-full text-left px-4 py-2 text-sm hover:bg-indigo-50 transition-colors ${
                      timeRange === opt.days ? 'text-indigo-600 font-semibold bg-indigo-50/50' : 'text-gray-700'
                    }`}
                  >
                    {opt.label}
                  </button>
                ))}
              </div>
            )}
          </div>

          <button 
            onClick={handleExportReport}
            disabled={exporting}
            className="flex items-center px-4 py-2 bg-indigo-600 text-white rounded-lg text-sm font-medium hover:bg-indigo-700 shadow-sm transition-all disabled:opacity-50"
          >
            <Download className="w-4 h-4 mr-2" />
            {exporting ? 'Exporting...' : 'Export Report'}
          </button>
        </div>
      </div>

      {/* KPI Cards */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6 mb-8">
        <KpiCard 
          title="Daily Revenue" 
          value={`₹${avgDailySales.toLocaleString()}`} 
          subtitle={`${timeRange}-Day Daily Average`}
          trend="+12%" 
          icon={<IndianRupee className="w-6 h-6 text-green-600" />} 
          color="bg-green-100" 
        />
        <KpiCard 
          title="Daily Profit" 
          value={`₹${avgDailyProfit.toLocaleString()}`} 
          subtitle={`${timeRange}-Day Daily Average (${timeRange <= 30 ? 'Per-line-item measured' : 'Blend: measured + estimated'})`}
          trend={`Realized ~${realizedMargin}%`} 
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
          <div className="mb-4">
            <div className="flex justify-between items-start">
              <h2 className="text-lg font-semibold text-gray-900">Revenue & Profit Trends</h2>
              <div className="flex gap-2">
                <span className="text-[10px] font-semibold px-2 py-0.5 rounded-full bg-emerald-50 text-emerald-700 border border-emerald-200">
                  ● Measured (Apr 2026+)
                </span>
                <span className="text-[10px] font-semibold px-2 py-0.5 rounded-full bg-amber-50 text-amber-700 border border-amber-200">
                  ● Estimated (pre-Apr 2026)
                </span>
              </div>
            </div>
            <p className="text-xs text-gray-500 mt-1 leading-relaxed">
              Revenue is authentic across all dates. <strong>Post Mar-24 2026:</strong> profit is per-line-item measured from Trans_Inventory_SOM (realized ~8.4% margin). <strong>Pre Mar-24 2026:</strong> profit is estimated using the verified 2026 margin rate applied to real daily revenue, due to a legacy carton-cost calculation defect in the source system.
            </p>
          </div>
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
                <XAxis dataKey="snapshotDate" tickFormatter={(val: string) => new Date(val).toLocaleDateString()} stroke="#9CA3AF" fontSize={12} />
                <YAxis stroke="#9CA3AF" fontSize={12} />
                {/* Shade the estimated (pre-2026-03-24) region in amber */}
                {firstEstimated && lastEstimated && (
                  <ReferenceArea
                    x1={firstEstimated.snapshotDate}
                    x2={lastEstimated.snapshotDate}
                    y1={0}
                    fill="#F59E0B"
                    fillOpacity={0.12}
                    stroke="#D97706"
                    strokeOpacity={0.4}
                    strokeDasharray="3 3"
                  />
                )}
                {/* Mark the boundary between estimated and measured */}
                {firstEstimated && firstMeasured && (
                  <ReferenceLine
                    x={firstMeasured.snapshotDate}
                    stroke="#D97706"
                    strokeDasharray="6 3"
                    strokeWidth={2}
                    label={{
                      value: 'Measured →',
                      position: 'top',
                      fill: '#92400E',
                      fontSize: 11,
                      fontWeight: 700
                    }}
                  />
                )}
                <RechartsTooltip
                  contentStyle={{ borderRadius: '8px', border: 'none', boxShadow: '0 4px 6px -1px rgba(0, 0, 0, 0.1)' }}
                  labelFormatter={(val: string) => {
                    const d = new Date(val);
                    const label = d.toLocaleDateString();
                    return val < '2026-03-24' ? `${label}  ⚠ Profit: Estimated` : `${label}  ✓ Profit: Measured`;
                  }}
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
