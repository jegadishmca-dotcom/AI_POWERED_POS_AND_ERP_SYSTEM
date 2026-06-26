import React, { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { getFinanceDashboard } from '../services/finance.service';
import { 
  PieChart, 
  TrendingUp, 
  TrendingDown, 
  Landmark, 
  Wallet, 
  ArrowRight, 
  AlertTriangle, 
  Lightbulb, 
  Banknote, 
  FileText, 
  CheckCircle2, 
  Package, 
  ShoppingBag, 
  ShoppingCart, 
  Coins, 
  Percent 
} from 'lucide-react';
import { formatCurrency } from '../../../utils/formatters';
import { Link } from 'react-router-dom';

export const FinanceDashboard: React.FC = () => {
  const [storeId, setStoreId] = useState('');

  const { data: dashboardData, isLoading, error } = useQuery({
    queryKey: ['financeDashboard', storeId],
    queryFn: () => getFinanceDashboard(storeId || undefined)
  });

  const aiAlerts = [
    { id: 1, title: 'Duplicate Invoice Check', type: 'anomaly', message: 'Supplier DELTA_001 submitted invoice INV-8899 which matches an existing entry.' },
    { id: 2, title: 'Cash Flow Projection', type: 'forecast', message: 'Projected cash balance may fall below minimum threshold next Thursday.' },
    { id: 3, title: 'Optimized Payment Strategy', type: 'recommendation', message: 'Pay ALPHA_CORP today to capture 2% early payment discount.' }
  ];

  return (
    <div className="p-6 max-w-7xl mx-auto space-y-8">
      {/* Header Section */}
      <div className="flex flex-col md:flex-row md:items-center justify-between gap-4">
        <div>
          <h2 className="text-2xl font-extrabold text-slate-800 dark:text-white flex items-center gap-3">
            <PieChart className="w-7 h-7 text-indigo-600" />
            Finance Dashboard
          </h2>
          <p className="text-slate-500 dark:text-slate-400 mt-1">Real-time overview of financial performance and cash positions</p>
        </div>
        <div className="w-[220px]">
          <select
            value={storeId}
            onChange={(e) => setStoreId(e.target.value)}
            className="w-full px-3 py-2.5 bg-white dark:bg-slate-900 border border-slate-300 dark:border-slate-700 rounded-lg text-sm font-bold text-slate-700 dark:text-slate-200 focus:ring-2 focus:ring-indigo-500 outline-none shadow-sm"
          >
            <option value="">All Stores (Consolidated)</option>
          </select>
        </div>
      </div>

      {isLoading ? (
        <div className="flex justify-center items-center h-64 text-slate-400 font-bold">
          <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-indigo-600 mr-3"></div>
          Loading financial data...
        </div>
      ) : error ? (
        <div className="bg-rose-50 dark:bg-rose-950/20 text-rose-600 p-6 rounded-xl border border-rose-200 dark:border-rose-800">
          <h3 className="font-extrabold text-lg flex items-center gap-2">
            <AlertTriangle className="w-5 h-5" />
            Failed to Load Financial Dashboard
          </h3>
          <p className="text-sm mt-1">Please try again later. {(error as any)?.message}</p>
        </div>
      ) : (
        <>
          {/* Section 1: Balance Sheet Positions */}
          <div className="space-y-4">
            <h3 className="text-xs font-extrabold text-slate-400 uppercase tracking-widest">Financial Positions</h3>
            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 xl:grid-cols-6 gap-6">
              
              {/* Cash Balance */}
              <div className="bg-white dark:bg-slate-900 rounded-xl shadow-sm border border-slate-200 dark:border-slate-800 p-5 flex flex-col justify-between hover:border-emerald-300 dark:hover:border-emerald-700 transition-colors group">
                <div className="flex justify-between items-start mb-4">
                  <div className="p-2.5 bg-emerald-50 dark:bg-emerald-900/20 text-emerald-600 rounded-lg group-hover:scale-105 transition-transform">
                    <Banknote className="w-5 h-5" />
                  </div>
                  <span className="text-[9px] font-bold text-emerald-600 bg-emerald-100 dark:bg-emerald-900/40 px-2 py-0.5 rounded-full uppercase tracking-wider">Asset</span>
                </div>
                <div>
                  <p className="text-xs font-bold text-slate-500 mb-1">Cash Balance</p>
                  <h4 className="text-lg font-black text-slate-800 dark:text-white truncate">
                    {formatCurrency(dashboardData?.cashBalance ?? 0)}
                  </h4>
                </div>
              </div>

              {/* Bank Balance */}
              <div className="bg-white dark:bg-slate-900 rounded-xl shadow-sm border border-slate-200 dark:border-slate-800 p-5 flex flex-col justify-between hover:border-indigo-300 dark:hover:border-indigo-700 transition-colors group">
                <div className="flex justify-between items-start mb-4">
                  <div className="p-2.5 bg-indigo-50 dark:bg-indigo-900/20 text-indigo-600 rounded-lg group-hover:scale-105 transition-transform">
                    <Landmark className="w-5 h-5" />
                  </div>
                  <span className="text-[9px] font-bold text-indigo-600 bg-indigo-100 dark:bg-indigo-900/40 px-2 py-0.5 rounded-full uppercase tracking-wider">Asset</span>
                </div>
                <div>
                  <p className="text-xs font-bold text-slate-500 mb-1">Bank Balance</p>
                  <h4 className="text-lg font-black text-slate-800 dark:text-white truncate">
                    {formatCurrency(dashboardData?.bankBalance ?? 0)}
                  </h4>
                </div>
              </div>

              {/* Inventory Value */}
              <div className="bg-white dark:bg-slate-900 rounded-xl shadow-sm border border-slate-200 dark:border-slate-800 p-5 flex flex-col justify-between hover:border-amber-300 dark:hover:border-amber-700 transition-colors group">
                <div className="flex justify-between items-start mb-4">
                  <div className="p-2.5 bg-amber-50 dark:bg-amber-900/20 text-amber-600 rounded-lg group-hover:scale-105 transition-transform">
                    <Package className="w-5 h-5" />
                  </div>
                  <span className="text-[9px] font-bold text-amber-600 bg-amber-100 dark:bg-amber-900/40 px-2 py-0.5 rounded-full uppercase tracking-wider">Stock</span>
                </div>
                <div>
                  <p className="text-xs font-bold text-slate-500 mb-1">Inventory Value</p>
                  <h4 className="text-lg font-black text-slate-800 dark:text-white truncate">
                    {formatCurrency(dashboardData?.inventoryValue ?? 0)}
                  </h4>
                </div>
              </div>

              {/* Accounts Receivable */}
              <div className="bg-white dark:bg-slate-900 rounded-xl shadow-sm border border-slate-200 dark:border-slate-800 p-5 flex flex-col justify-between hover:border-blue-300 dark:hover:border-blue-700 transition-colors group">
                <div className="flex justify-between items-start mb-4">
                  <div className="p-2.5 bg-blue-50 dark:bg-blue-900/20 text-blue-600 rounded-lg group-hover:scale-105 transition-transform">
                    <TrendingUp className="w-5 h-5" />
                  </div>
                  <span className="text-[9px] font-bold text-blue-600 bg-blue-100 dark:bg-blue-900/40 px-2 py-0.5 rounded-full uppercase tracking-wider">AR</span>
                </div>
                <div>
                  <p className="text-xs font-bold text-slate-500 mb-1">Receivables</p>
                  <h4 className="text-lg font-black text-blue-600 truncate">
                    {formatCurrency(dashboardData?.accountsReceivable ?? 0)}
                  </h4>
                </div>
              </div>

              {/* Accounts Payable */}
              <div className="bg-white dark:bg-slate-900 rounded-xl shadow-sm border border-slate-200 dark:border-slate-800 p-5 flex flex-col justify-between hover:border-rose-300 dark:hover:border-rose-700 transition-colors group">
                <div className="flex justify-between items-start mb-4">
                  <div className="p-2.5 bg-rose-50 dark:bg-rose-900/20 text-rose-600 rounded-lg group-hover:scale-105 transition-transform">
                    <TrendingDown className="w-5 h-5" />
                  </div>
                  <span className="text-[9px] font-bold text-rose-600 bg-rose-100 dark:bg-rose-900/40 px-2 py-0.5 rounded-full uppercase tracking-wider">AP</span>
                </div>
                <div>
                  <p className="text-xs font-bold text-slate-500 mb-1">Payables</p>
                  <h4 className="text-lg font-black text-rose-600 truncate">
                    {formatCurrency(dashboardData?.accountsPayable ?? 0)}
                  </h4>
                </div>
              </div>

              {/* Working Capital */}
              <div className="bg-white dark:bg-slate-900 rounded-xl shadow-sm border border-slate-200 dark:border-slate-800 p-5 flex flex-col justify-between hover:border-purple-300 dark:hover:border-purple-700 transition-colors group">
                <div className="flex justify-between items-start mb-4">
                  <div className="p-2.5 bg-purple-50 dark:bg-purple-900/20 text-purple-600 rounded-lg group-hover:scale-105 transition-transform">
                    <Coins className="w-5 h-5" />
                  </div>
                  <span className="text-[9px] font-bold text-purple-600 bg-purple-100 dark:bg-purple-900/40 px-2 py-0.5 rounded-full uppercase tracking-wider">Net</span>
                </div>
                <div>
                  <p className="text-xs font-bold text-slate-500 mb-1">Working Capital</p>
                  <h4 className={`text-lg font-black truncate ${(dashboardData?.workingCapital ?? 0) >= 0 ? 'text-purple-600' : 'text-rose-600'}`}>
                    {formatCurrency(dashboardData?.workingCapital ?? 0)}
                  </h4>
                </div>
              </div>

            </div>
          </div>

          {/* Section 2: Daily Performance & Taxation */}
          <div className="space-y-4">
            <h3 className="text-xs font-extrabold text-slate-400 uppercase tracking-widest">Performance & Tax Liabilities</h3>
            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6">
              
              {/* Sales Today */}
              <div className="bg-white dark:bg-slate-900 rounded-xl shadow-sm border border-slate-200 dark:border-slate-800 p-5 flex items-center justify-between hover:border-teal-300 dark:hover:border-teal-700 transition-colors group">
                <div className="space-y-1">
                  <p className="text-xs font-bold text-slate-500 uppercase tracking-wider">Sales Today</p>
                  <h3 className="text-xl font-black text-teal-600">
                    {formatCurrency(dashboardData?.salesToday ?? 0)}
                  </h3>
                </div>
                <div className="p-3 bg-teal-50 dark:bg-teal-900/20 text-teal-600 rounded-xl group-hover:scale-105 transition-transform">
                  <ShoppingBag className="w-6 h-6" />
                </div>
              </div>

              {/* Purchases Today */}
              <div className="bg-white dark:bg-slate-900 rounded-xl shadow-sm border border-slate-200 dark:border-slate-800 p-5 flex items-center justify-between hover:border-orange-300 dark:hover:border-orange-700 transition-colors group">
                <div className="space-y-1">
                  <p className="text-xs font-bold text-slate-500 uppercase tracking-wider">Purchases Today</p>
                  <h3 className="text-xl font-black text-orange-600">
                    {formatCurrency(dashboardData?.purchasesToday ?? 0)}
                  </h3>
                </div>
                <div className="p-3 bg-orange-50 dark:bg-orange-900/20 text-orange-600 rounded-xl group-hover:scale-105 transition-transform">
                  <ShoppingCart className="w-6 h-6" />
                </div>
              </div>

              {/* Net Profit */}
              <div className="bg-white dark:bg-slate-900 rounded-xl shadow-sm border border-slate-200 dark:border-slate-800 p-5 flex items-center justify-between hover:border-cyan-300 dark:hover:border-cyan-700 transition-colors group">
                <div className="space-y-1">
                  <p className="text-xs font-bold text-slate-500 uppercase tracking-wider">Net Profit / Loss</p>
                  <h3 className={`text-xl font-black ${(dashboardData?.profit ?? 0) >= 0 ? 'text-cyan-600' : 'text-rose-600'}`}>
                    {formatCurrency(dashboardData?.profit ?? 0)}
                  </h3>
                </div>
                <div className={`p-3 rounded-xl group-hover:scale-105 transition-transform ${(dashboardData?.profit ?? 0) >= 0 ? 'bg-cyan-50 dark:bg-cyan-900/20 text-cyan-600' : 'bg-rose-50 dark:bg-rose-900/20 text-rose-600'}`}>
                  <TrendingUp className="w-6 h-6" />
                </div>
              </div>

              {/* GST Payable */}
              <div className="bg-white dark:bg-slate-900 rounded-xl shadow-sm border border-slate-200 dark:border-slate-800 p-5 flex items-center justify-between hover:border-violet-300 dark:hover:border-violet-700 transition-colors group">
                <div className="space-y-1">
                  <p className="text-xs font-bold text-slate-500 uppercase tracking-wider">GST Payable (Net)</p>
                  <h3 className={`text-xl font-black ${(dashboardData?.gstPayable ?? 0) >= 0 ? 'text-violet-600' : 'text-emerald-600'}`}>
                    {formatCurrency(dashboardData?.gstPayable ?? 0)}
                  </h3>
                </div>
                <div className="p-3 bg-violet-50 dark:bg-violet-900/20 text-violet-600 rounded-xl group-hover:scale-105 transition-transform">
                  <Percent className="w-6 h-6" />
                </div>
              </div>

            </div>
          </div>

          <div className="grid grid-cols-1 lg:grid-cols-3 gap-8">
            {/* AI Finance Intelligence */}
            <div className="lg:col-span-2 bg-white dark:bg-slate-900 rounded-xl shadow-sm border border-slate-200 dark:border-slate-800 overflow-hidden flex flex-col">
              <div className="p-5 border-b border-slate-200 dark:border-slate-800 flex justify-between items-center bg-gradient-to-r from-slate-50 to-indigo-50/30 dark:from-slate-900 dark:to-indigo-900/10">
                <h3 className="font-extrabold text-indigo-900 dark:text-indigo-400 uppercase tracking-wider flex items-center gap-2">
                  <Lightbulb className="w-5 h-5 animate-pulse text-indigo-500" />
                  AI Finance Intelligence
                </h3>
                <span className="text-xs font-bold bg-indigo-100 text-indigo-800 dark:bg-indigo-950/40 dark:text-indigo-400 px-2 py-1 rounded">3 Active Insights</span>
              </div>
              <div className="p-0 flex-1 divide-y divide-slate-100 dark:divide-slate-800">
                {aiAlerts.map(alert => (
                  <div key={alert.id} className="p-5 flex gap-4 hover:bg-slate-50 dark:hover:bg-slate-800/30 transition-colors">
                    <div className="mt-1">
                      {alert.type === 'anomaly' && <AlertTriangle className="w-5 h-5 text-rose-500" />}
                      {alert.type === 'forecast' && <TrendingDown className="w-5 h-5 text-amber-500" />}
                      {alert.type === 'recommendation' && <CheckCircle2 className="w-5 h-5 text-emerald-500" />}
                    </div>
                    <div>
                      <h4 className="font-bold text-slate-800 dark:text-white text-sm">{alert.title}</h4>
                      <p className="text-sm text-slate-500 dark:text-slate-400 mt-1">{alert.message}</p>
                    </div>
                  </div>
                ))}
              </div>
            </div>

            {/* Quick Actions */}
            <div className="bg-white dark:bg-slate-900 rounded-xl shadow-sm border border-slate-200 dark:border-slate-800 overflow-hidden flex flex-col">
              <div className="p-5 border-b border-slate-200 dark:border-slate-800">
                <h3 className="font-extrabold text-slate-800 dark:text-white uppercase tracking-wider">Quick Actions</h3>
              </div>
              <div className="p-3 flex-1 flex flex-col justify-start">
                <Link to="/finance/supplier-bills" className="flex items-center justify-between p-4 hover:bg-slate-50 dark:hover:bg-slate-800/50 rounded-lg transition-colors group">
                  <div className="flex items-center gap-4">
                    <div className="w-10 h-10 rounded-full bg-rose-50 dark:bg-rose-900/30 flex items-center justify-center text-rose-600">
                      <FileText className="w-5 h-5" />
                    </div>
                    <div>
                      <p className="font-bold text-slate-800 dark:text-white group-hover:text-rose-600 transition-colors text-sm">Process AP Bills</p>
                      <p className="text-xs text-slate-500">Pay supplier invoices</p>
                    </div>
                  </div>
                  <ArrowRight className="w-5 h-5 text-slate-400 group-hover:text-rose-600 transition-colors group-hover:translate-x-1" />
                </Link>
                <Link to="/finance/customer-receipts" className="flex items-center justify-between p-4 hover:bg-slate-50 dark:hover:bg-slate-800/50 rounded-lg transition-colors group">
                  <div className="flex items-center gap-4">
                    <div className="w-10 h-10 rounded-full bg-blue-50 dark:bg-blue-900/30 flex items-center justify-center text-blue-600">
                      <Banknote className="w-5 h-5" />
                    </div>
                    <div>
                      <p className="font-bold text-slate-800 dark:text-white group-hover:text-blue-600 transition-colors text-sm">Record AR Receipts</p>
                      <p className="text-xs text-slate-500">Log customer payments</p>
                    </div>
                  </div>
                  <ArrowRight className="w-5 h-5 text-slate-400 group-hover:text-blue-600 transition-colors group-hover:translate-x-1" />
                </Link>
                <Link to="/finance/journals" className="flex items-center justify-between p-4 hover:bg-slate-50 dark:hover:bg-slate-800/50 rounded-lg transition-colors group">
                  <div className="flex items-center gap-4">
                    <div className="w-10 h-10 rounded-full bg-indigo-50 dark:bg-indigo-900/30 flex items-center justify-center text-indigo-600">
                      <Landmark className="w-5 h-5" />
                    </div>
                    <div>
                      <p className="font-bold text-slate-800 dark:text-white group-hover:text-indigo-600 transition-colors text-sm">Journal Entry</p>
                      <p className="text-xs text-slate-500">Manual GL adjustment</p>
                    </div>
                  </div>
                  <ArrowRight className="w-5 h-5 text-slate-400 group-hover:text-indigo-600 transition-colors group-hover:translate-x-1" />
                </Link>
              </div>
            </div>
          </div>
        </>
      )}
    </div>
  );
};
