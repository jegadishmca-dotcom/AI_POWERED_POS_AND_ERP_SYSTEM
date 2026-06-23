import React, { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { getProfitAndLoss } from '../services/finance.service';
import { BarChart3, Download, FileSpreadsheet, FileText } from 'lucide-react';
import { formatCurrency } from '../../../utils/formatters';

export const ProfitAndLoss: React.FC = () => {
  const [startDate, setStartDate] = useState(() => {
    const d = new Date();
    d.setMonth(d.getMonth() - 1);
    return d.toISOString().split('T')[0];
  });
  const [endDate, setEndDate] = useState(new Date().toISOString().split('T')[0]);
  const [storeId, setStoreId] = useState('');

  const { data, isLoading } = useQuery({
    queryKey: ['profitAndLoss', storeId, startDate, endDate],
    queryFn: () => getProfitAndLoss(storeId || undefined, startDate, endDate)
  });

  const handleExport = (format: 'pdf' | 'excel' | 'csv') => {
    const url = `/api/financialreports/profit-and-loss?format=${format}&startDate=${startDate}&endDate=${endDate}${storeId ? `&storeId=${storeId}` : ''}`;
    window.open(url, '_blank');
  };

  const renderSection = (title: string, accounts: any[], total: number) => {
    if (!accounts || accounts.length === 0) return null;
    return (
      <div className="mb-6">
        <h3 className="font-bold text-slate-800 dark:text-white uppercase tracking-wider text-xs mb-2 border-b border-slate-200 dark:border-slate-800 pb-2">{title}</h3>
        {accounts.map((acc, idx) => (
          <div key={idx} className="flex justify-between py-2 border-b border-slate-100 dark:border-slate-800/50 hover:bg-slate-50 dark:hover:bg-slate-800/30 px-2 rounded">
            <span className="text-slate-600 dark:text-slate-300 text-sm">
              <span className="font-mono text-indigo-500 mr-3 text-xs">{acc.accountCode}</span>
              {acc.accountName}
            </span>
            <span className="font-medium text-slate-700 dark:text-slate-200 text-sm">{formatCurrency(acc.creditBalance || acc.debitBalance || 0)}</span>
          </div>
        ))}
        <div className="flex justify-between py-3 mt-1 px-2 bg-slate-50 dark:bg-slate-800/50 rounded-lg">
          <span className="font-bold text-slate-700 dark:text-slate-200 text-sm">Total {title}</span>
          <span className="font-bold text-slate-800 dark:text-white">{formatCurrency(total)}</span>
        </div>
      </div>
    );
  };

  return (
    <div className="p-6 max-w-5xl mx-auto">
      <div className="flex flex-col md:flex-row md:items-center justify-between mb-8 gap-4">
        <div>
          <h2 className="text-2xl font-extrabold text-slate-800 dark:text-white flex items-center gap-3">
            <BarChart3 className="w-7 h-7 text-indigo-600" />
            Profit & Loss
          </h2>
          <p className="text-slate-500 dark:text-slate-400 mt-1">Income statement showing revenues and expenses</p>
        </div>
        
        <div className="flex items-center gap-3">
          <button onClick={() => handleExport('csv')} className="p-2 text-slate-500 hover:text-emerald-600 hover:bg-emerald-50 dark:hover:bg-emerald-900/30 rounded-lg transition-colors" title="Export CSV">
            <FileText className="w-5 h-5" />
          </button>
          <button onClick={() => handleExport('excel')} className="p-2 text-slate-500 hover:text-emerald-600 hover:bg-emerald-50 dark:hover:bg-emerald-900/30 rounded-lg transition-colors" title="Export Excel">
            <FileSpreadsheet className="w-5 h-5" />
          </button>
          <button onClick={() => handleExport('pdf')} className="p-2 text-slate-500 hover:text-rose-600 hover:bg-rose-50 dark:hover:bg-rose-900/30 rounded-lg transition-colors" title="Export PDF">
            <Download className="w-5 h-5" />
          </button>
        </div>
      </div>

      <div className="bg-white dark:bg-slate-900 rounded-xl shadow-sm border border-slate-200 dark:border-slate-800 overflow-hidden mb-6">
        <div className="p-4 bg-slate-50 dark:bg-slate-950 flex flex-wrap gap-4">
          <div className="flex-1 min-w-[200px]">
            <label className="block text-xs font-bold text-slate-500 uppercase tracking-wider mb-1.5">Start Date</label>
            <input 
              type="date" 
              value={startDate}
              onChange={(e) => setStartDate(e.target.value)}
              className="w-full px-3 py-2 bg-white dark:bg-slate-900 border border-slate-300 dark:border-slate-700 rounded-lg text-sm focus:ring-2 focus:ring-indigo-500 outline-none"
            />
          </div>
          <div className="flex-1 min-w-[200px]">
            <label className="block text-xs font-bold text-slate-500 uppercase tracking-wider mb-1.5">End Date</label>
            <input 
              type="date" 
              value={endDate}
              onChange={(e) => setEndDate(e.target.value)}
              className="w-full px-3 py-2 bg-white dark:bg-slate-900 border border-slate-300 dark:border-slate-700 rounded-lg text-sm focus:ring-2 focus:ring-indigo-500 outline-none"
            />
          </div>
          <div className="flex-1 min-w-[200px]">
            <label className="block text-xs font-bold text-slate-500 uppercase tracking-wider mb-1.5">Store Filter</label>
            <select
              value={storeId}
              onChange={(e) => setStoreId(e.target.value)}
              className="w-full px-3 py-2 bg-white dark:bg-slate-900 border border-slate-300 dark:border-slate-700 rounded-lg text-sm focus:ring-2 focus:ring-indigo-500 outline-none"
            >
              <option value="">All Stores (Consolidated)</option>
            </select>
          </div>
        </div>
      </div>

      {isLoading ? (
        <div className="flex justify-center items-center h-48 text-slate-400">Loading Profit & Loss Statement...</div>
      ) : data ? (
        <div className="bg-white dark:bg-slate-900 rounded-xl shadow-sm border border-slate-200 dark:border-slate-800 overflow-hidden">
          <div className="p-8">
            <div className="text-center mb-8">
              <h1 className="text-xl font-bold text-slate-800 dark:text-white uppercase tracking-wider">Profit & Loss Statement</h1>
              <p className="text-slate-500 text-sm mt-1">{startDate} to {endDate}</p>
            </div>

            {renderSection('Operating Revenue', data.revenueAccounts, data.totalRevenue)}
            {renderSection('Cost of Goods Sold (COGS)', data.cogsAccounts || [], data.totalCOGS)}
            
            <div className="flex justify-between py-4 mb-6 px-4 bg-indigo-50 dark:bg-indigo-900/20 border border-indigo-100 dark:border-indigo-800/30 rounded-xl">
              <span className="font-extrabold text-indigo-900 dark:text-indigo-200 text-base uppercase tracking-wider">Gross Profit</span>
              <span className="font-extrabold text-indigo-700 dark:text-indigo-400 text-lg">{formatCurrency(data.grossProfit)}</span>
            </div>

            {renderSection('Operating Expenses', data.expenseAccounts, data.totalOperatingExpenses)}

            <div className={`flex justify-between py-5 mt-8 px-4 rounded-xl border-2 ${data.netProfit >= 0 ? 'bg-emerald-50 border-emerald-200 dark:bg-emerald-900/20 dark:border-emerald-800/30' : 'bg-rose-50 border-rose-200 dark:bg-rose-900/20 dark:border-rose-800/30'}`}>
              <span className={`font-extrabold text-lg uppercase tracking-wider ${data.netProfit >= 0 ? 'text-emerald-900 dark:text-emerald-200' : 'text-rose-900 dark:text-rose-200'}`}>
                Net {data.netProfit >= 0 ? 'Profit' : 'Loss'}
              </span>
              <span className={`font-extrabold text-2xl ${data.netProfit >= 0 ? 'text-emerald-700 dark:text-emerald-400' : 'text-rose-700 dark:text-rose-400'}`}>
                {formatCurrency(Math.abs(data.netProfit))}
              </span>
            </div>
          </div>
        </div>
      ) : (
        <div className="text-center p-12 text-slate-500 bg-white dark:bg-slate-900 rounded-xl border border-slate-200 dark:border-slate-800">
          No data available for the selected period.
        </div>
      )}
    </div>
  );
};
