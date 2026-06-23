import React, { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { getProfitAndLoss, getBalanceSheet } from '../services/finance.service';
import { PieChart, TrendingUp, TrendingDown, Landmark, Wallet, ArrowRight, AlertTriangle, Lightbulb, Banknote, FileText } from 'lucide-react';
import { formatCurrency } from '../../../utils/formatters';
import { Link } from 'react-router-dom';

export const FinanceDashboard: React.FC = () => {
  const [storeId, setStoreId] = useState('');
  
  const today = new Date().toISOString().split('T')[0];
  const lastMonth = new Date();
  lastMonth.setMonth(lastMonth.getMonth() - 1);
  const startDate = lastMonth.toISOString().split('T')[0];

  const { data: plData, isLoading: plLoading } = useQuery({
    queryKey: ['profitAndLoss', storeId, startDate, today],
    queryFn: () => getProfitAndLoss(storeId || undefined, startDate, today)
  });

  const { data: bsData, isLoading: bsLoading } = useQuery({
    queryKey: ['balanceSheet', storeId, today],
    queryFn: () => getBalanceSheet(storeId || undefined, today)
  });

  const isLoading = plLoading || bsLoading;

  // Mock data for F2 expanded features until API is connected
  const aiAlerts = [
    { id: 1, title: 'Duplicate Invoice Detected', type: 'anomaly', message: 'Supplier DELTA_001 submitted invoice INV-8899 which matches an existing entry.' },
    { id: 2, title: 'Cash Flow Risk', type: 'forecast', message: 'Projected cash balance may fall below minimum threshold next Thursday.' },
    { id: 3, title: 'Payment Recommendation', type: 'recommendation', message: 'Pay ALPHA_CORP today to capture 2% early payment discount.' }
  ];

  return (
    <div className="p-6 max-w-7xl mx-auto space-y-6">
      <div className="flex flex-col md:flex-row md:items-center justify-between mb-2 gap-4">
        <div>
          <h2 className="text-2xl font-extrabold text-slate-800 dark:text-white flex items-center gap-3">
            <PieChart className="w-7 h-7 text-indigo-600" />
            Finance Dashboard
          </h2>
          <p className="text-slate-500 dark:text-slate-400 mt-1">Executive overview of financial performance and cash position</p>
        </div>
        <div className="w-[200px]">
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
        <div className="flex justify-center items-center h-64 text-slate-400">Loading Dashboard Metrics...</div>
      ) : (
        <>
          {/* F2 Expanded Metrics Grid */}
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6">
            <div className="bg-white dark:bg-slate-900 rounded-xl shadow-sm border border-slate-200 dark:border-slate-800 p-6 flex flex-col justify-between hover:border-emerald-300 dark:hover:border-emerald-700 transition-colors group">
              <div className="flex justify-between items-start mb-4">
                <div className="p-3 bg-emerald-50 dark:bg-emerald-900/20 text-emerald-600 rounded-lg group-hover:scale-110 transition-transform">
                  <Banknote className="w-6 h-6" />
                </div>
                <span className="text-[10px] font-bold text-emerald-600 bg-emerald-100 px-2 py-0.5 rounded-full uppercase tracking-wider">Cash Pos.</span>
              </div>
              <div>
                <p className="text-sm font-bold text-slate-500 mb-1">Cash Balance</p>
                <h3 className="text-2xl font-black text-slate-800 dark:text-white">{formatCurrency(458000)}</h3>
              </div>
            </div>

            <div className="bg-white dark:bg-slate-900 rounded-xl shadow-sm border border-slate-200 dark:border-slate-800 p-6 flex flex-col justify-between hover:border-rose-300 dark:hover:border-rose-700 transition-colors group">
              <div className="flex justify-between items-start mb-4">
                <div className="p-3 bg-rose-50 dark:bg-rose-900/20 text-rose-600 rounded-lg group-hover:scale-110 transition-transform">
                  <TrendingDown className="w-6 h-6" />
                </div>
                <span className="text-[10px] font-bold text-rose-600 bg-rose-100 px-2 py-0.5 rounded-full uppercase tracking-wider">Payables</span>
              </div>
              <div>
                <p className="text-sm font-bold text-slate-500 mb-1">Accounts Payable</p>
                <h3 className="text-2xl font-black text-rose-600">{formatCurrency(125000)}</h3>
              </div>
            </div>

            <div className="bg-white dark:bg-slate-900 rounded-xl shadow-sm border border-slate-200 dark:border-slate-800 p-6 flex flex-col justify-between hover:border-blue-300 dark:hover:border-blue-700 transition-colors group">
              <div className="flex justify-between items-start mb-4">
                <div className="p-3 bg-blue-50 dark:bg-blue-900/20 text-blue-600 rounded-lg group-hover:scale-110 transition-transform">
                  <TrendingUp className="w-6 h-6" />
                </div>
                <span className="text-[10px] font-bold text-blue-600 bg-blue-100 px-2 py-0.5 rounded-full uppercase tracking-wider">Receivables</span>
              </div>
              <div>
                <p className="text-sm font-bold text-slate-500 mb-1">Accounts Receivable</p>
                <h3 className="text-2xl font-black text-blue-600">{formatCurrency(35000)}</h3>
              </div>
            </div>

            <div className="bg-white dark:bg-slate-900 rounded-xl shadow-sm border border-slate-200 dark:border-slate-800 p-6 flex flex-col justify-between hover:border-purple-300 dark:hover:border-purple-700 transition-colors group">
              <div className="flex justify-between items-start mb-4">
                <div className="p-3 bg-purple-50 dark:bg-purple-900/20 text-purple-600 rounded-lg group-hover:scale-110 transition-transform">
                  <Wallet className="w-6 h-6" />
                </div>
                <span className="text-[10px] font-bold text-purple-600 bg-purple-100 px-2 py-0.5 rounded-full uppercase tracking-wider">Liquidity</span>
              </div>
              <div>
                <p className="text-sm font-bold text-slate-500 mb-1">Working Capital</p>
                <h3 className="text-2xl font-black text-purple-600">{formatCurrency(368000)}</h3>
              </div>
            </div>
          </div>

          {/* Secondary Metrics Grid */}
          <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
            <div className="bg-white dark:bg-slate-900 rounded-xl shadow-sm border border-slate-200 dark:border-slate-800 p-5 flex items-center justify-between">
              <div>
                <p className="text-xs font-bold text-slate-500 uppercase tracking-wider mb-1">Net Income (30d)</p>
                <h3 className={`text-xl font-black ${(plData?.netProfit || 0) >= 0 ? 'text-indigo-600' : 'text-rose-600'}`}>
                  {formatCurrency(plData?.netProfit || 0)}
                </h3>
              </div>
              <PieChart className={`w-8 h-8 opacity-20 ${(plData?.netProfit || 0) >= 0 ? 'text-indigo-600' : 'text-rose-600'}`} />
            </div>

            <div className="bg-white dark:bg-slate-900 rounded-xl shadow-sm border border-slate-200 dark:border-slate-800 p-5 flex items-center justify-between">
              <div>
                <p className="text-xs font-bold text-slate-500 uppercase tracking-wider mb-1">Inventory Value</p>
                <h3 className="text-xl font-black text-slate-800 dark:text-white">{formatCurrency(850000)}</h3>
              </div>
              <Landmark className="w-8 h-8 opacity-20 text-slate-600" />
            </div>

            <div className="bg-white dark:bg-slate-900 rounded-xl shadow-sm border border-slate-200 dark:border-slate-800 p-5 flex items-center justify-between">
              <div>
                <p className="text-xs font-bold text-slate-500 uppercase tracking-wider mb-1">GST Payable</p>
                <h3 className="text-xl font-black text-rose-600">{formatCurrency(45200)}</h3>
              </div>
              <Wallet className="w-8 h-8 opacity-20 text-rose-600" />
            </div>
          </div>

          <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
            {/* AI Alerts & Forecasts Widget */}
            <div className="lg:col-span-2 bg-white dark:bg-slate-900 rounded-xl shadow-sm border border-slate-200 dark:border-slate-800 overflow-hidden flex flex-col">
              <div className="p-5 border-b border-slate-200 dark:border-slate-800 flex justify-between items-center bg-gradient-to-r from-slate-50 to-indigo-50/30 dark:from-slate-900 dark:to-indigo-900/10">
                <h3 className="font-extrabold text-indigo-900 dark:text-indigo-400 uppercase tracking-wider flex items-center gap-2">
                  <Lightbulb className="w-5 h-5" />
                  AI Finance Intelligence
                </h3>
                <span className="text-xs font-bold bg-indigo-100 text-indigo-800 px-2 py-1 rounded">3 Active Insights</span>
              </div>
              <div className="p-0 flex-1">
                {aiAlerts.map(alert => (
                  <div key={alert.id} className="p-4 border-b border-slate-100 dark:border-slate-800 hover:bg-slate-50 dark:hover:bg-slate-800/50 flex gap-4 transition-colors">
                    <div className="mt-1">
                      {alert.type === 'anomaly' && <AlertTriangle className="w-5 h-5 text-rose-500" />}
                      {alert.type === 'forecast' && <TrendingDown className="w-5 h-5 text-amber-500" />}
                      {alert.type === 'recommendation' && <CheckCircle2 className="w-5 h-5 text-emerald-500" />}
                    </div>
                    <div>
                      <h4 className="font-bold text-slate-800 dark:text-white text-sm">{alert.title}</h4>
                      <p className="text-sm text-slate-600 dark:text-slate-400 mt-1">{alert.message}</p>
                    </div>
                  </div>
                ))}
              </div>
            </div>

            {/* Quick Actions */}
            <div className="bg-white dark:bg-slate-900 rounded-xl shadow-sm border border-slate-200 dark:border-slate-800 overflow-hidden">
              <div className="p-5 border-b border-slate-200 dark:border-slate-800">
                <h3 className="font-extrabold text-slate-800 dark:text-white uppercase tracking-wider">Quick Actions</h3>
              </div>
              <div className="p-2">
                <Link to="/finance/supplier-bills" className="flex items-center justify-between p-4 hover:bg-slate-50 dark:hover:bg-slate-800/50 rounded-lg transition-colors group">
                  <div className="flex items-center gap-4">
                    <div className="w-10 h-10 rounded-full bg-rose-50 dark:bg-rose-900/30 flex items-center justify-center text-rose-600">
                      <FileText className="w-5 h-5" />
                    </div>
                    <div>
                      <p className="font-bold text-slate-800 dark:text-white group-hover:text-rose-600 transition-colors">Process AP Bills</p>
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
                      <p className="font-bold text-slate-800 dark:text-white group-hover:text-blue-600 transition-colors">Record AR Receipts</p>
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
                      <p className="font-bold text-slate-800 dark:text-white group-hover:text-indigo-600 transition-colors">Journal Entry</p>
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

// Add to imports at top of file for the CheckCircle2 icon:
import { CheckCircle2 } from 'lucide-react';
