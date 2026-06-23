import React, { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { getBalanceSheet } from '../services/finance.service';
import { Landmark, Download, FileSpreadsheet, FileText } from 'lucide-react';
import { formatCurrency } from '../../../utils/formatters';

export const BalanceSheet: React.FC = () => {
  const [asOfDate, setAsOfDate] = useState(new Date().toISOString().split('T')[0]);
  const [storeId, setStoreId] = useState('');

  const { data, isLoading } = useQuery({
    queryKey: ['balanceSheet', storeId, asOfDate],
    queryFn: () => getBalanceSheet(storeId || undefined, asOfDate)
  });

  const handleExport = (format: 'pdf' | 'excel' | 'csv') => {
    const url = `/api/financialreports/balance-sheet?format=${format}&asOfDate=${asOfDate}${storeId ? `&storeId=${storeId}` : ''}`;
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
            <span className="font-medium text-slate-700 dark:text-slate-200 text-sm">{formatCurrency(acc.debitBalance || acc.creditBalance || 0)}</span>
          </div>
        ))}
        <div className="flex justify-between py-3 mt-1 px-2 bg-slate-50 dark:bg-slate-800/50 rounded-lg border border-slate-200 dark:border-slate-700">
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
            <Landmark className="w-7 h-7 text-indigo-600" />
            Balance Sheet
          </h2>
          <p className="text-slate-500 dark:text-slate-400 mt-1">Snapshot of assets, liabilities, and equity</p>
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
            <label className="block text-xs font-bold text-slate-500 uppercase tracking-wider mb-1.5">As Of Date</label>
            <input 
              type="date" 
              value={asOfDate}
              onChange={(e) => setAsOfDate(e.target.value)}
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
        <div className="flex justify-center items-center h-48 text-slate-400">Loading Balance Sheet...</div>
      ) : data ? (
        <div className="grid grid-cols-1 lg:grid-cols-2 gap-8">
          {/* Left Column: Assets */}
          <div className="bg-white dark:bg-slate-900 rounded-xl shadow-sm border border-slate-200 dark:border-slate-800 p-6">
            <h2 className="text-xl font-extrabold text-indigo-900 dark:text-indigo-400 mb-6 border-b-2 border-indigo-100 dark:border-indigo-900/50 pb-3 uppercase tracking-widest">Assets</h2>
            {renderSection('Current Assets', data.assetAccounts?.filter((a: any) => a.accountCode.startsWith('10') || a.accountCode.startsWith('11')), data.totalAssets)}
            {/* If more granular classification exists, map it here. For now, showing all assets under total */}
            {data.assetAccounts && data.assetAccounts.length > 0 && (
              <div className="mt-8 pt-4 border-t-4 border-slate-200 dark:border-slate-700 flex justify-between items-center">
                <span className="font-black text-slate-800 dark:text-white uppercase tracking-wider">Total Assets</span>
                <span className="font-black text-indigo-600 dark:text-indigo-400 text-xl">{formatCurrency(data.totalAssets)}</span>
              </div>
            )}
          </div>

          {/* Right Column: Liabilities & Equity */}
          <div className="bg-white dark:bg-slate-900 rounded-xl shadow-sm border border-slate-200 dark:border-slate-800 p-6 flex flex-col">
            <h2 className="text-xl font-extrabold text-rose-900 dark:text-rose-400 mb-6 border-b-2 border-rose-100 dark:border-rose-900/50 pb-3 uppercase tracking-widest">Liabilities & Equity</h2>
            
            <div className="flex-1">
              {renderSection('Liabilities', data.liabilityAccounts, data.totalLiabilities)}
              <div className="my-8 border-t border-slate-200 dark:border-slate-800"></div>
              {renderSection('Equity', data.equityAccounts, data.totalEquity)}
            </div>

            <div className="mt-8 pt-4 border-t-4 border-slate-200 dark:border-slate-700 flex justify-between items-center">
              <span className="font-black text-slate-800 dark:text-white uppercase tracking-wider">Total Liab. & Equity</span>
              <span className="font-black text-rose-600 dark:text-rose-400 text-xl">{formatCurrency(data.totalLiabilities + data.totalEquity)}</span>
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
