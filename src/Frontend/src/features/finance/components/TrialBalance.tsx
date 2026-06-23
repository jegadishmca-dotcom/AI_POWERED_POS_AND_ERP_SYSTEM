import React, { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { getTrialBalance } from '../services/finance.service';
import { Wallet, Search, Download, FileSpreadsheet, FileText } from 'lucide-react';
import { formatCurrency } from '../../../utils/formatters';

export const TrialBalance: React.FC = () => {
  const [asOfDate, setAsOfDate] = useState(new Date().toISOString().split('T')[0]);
  const [storeId, setStoreId] = useState('');

  const { data, isLoading } = useQuery({
    queryKey: ['trialBalance', storeId, asOfDate],
    queryFn: () => getTrialBalance(storeId || undefined, asOfDate)
  });

  const handleExport = (format: 'pdf' | 'excel' | 'csv') => {
    const url = `/api/financialreports/trial-balance?format=${format}&asOfDate=${asOfDate}${storeId ? `&storeId=${storeId}` : ''}`;
    // Open in new tab which will trigger the download
    window.open(url, '_blank');
  };

  return (
    <div className="p-6 max-w-6xl mx-auto">
      <div className="flex flex-col md:flex-row md:items-center justify-between mb-8 gap-4">
        <div>
          <h2 className="text-2xl font-extrabold text-slate-800 dark:text-white flex items-center gap-3">
            <Wallet className="w-7 h-7 text-indigo-600" />
            Trial Balance
          </h2>
          <p className="text-slate-500 dark:text-slate-400 mt-1">Verify that total debits equal total credits</p>
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
              {/* Normally populate from stores API */}
            </select>
          </div>
        </div>
      </div>

      <div className="bg-white dark:bg-slate-900 rounded-xl shadow-sm border border-slate-200 dark:border-slate-800 overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full text-left border-collapse">
            <thead>
              <tr className="bg-slate-100 dark:bg-slate-950 border-b border-slate-200 dark:border-slate-800 text-xs font-bold text-slate-500 uppercase tracking-wider">
                <th className="p-4">Account Code</th>
                <th className="p-4">Account Name</th>
                <th className="p-4 text-right">Debit</th>
                <th className="p-4 text-right">Credit</th>
              </tr>
            </thead>
            <tbody className="text-sm">
              {isLoading ? (
                <tr><td colSpan={4} className="p-8 text-center text-slate-400">Loading Trial Balance...</td></tr>
              ) : data?.lines?.length > 0 ? (
                <>
                  {data.lines.map((line: any, idx: number) => (
                    <tr key={idx} className="border-b border-slate-100 dark:border-slate-800/50 hover:bg-slate-50 dark:hover:bg-slate-800/30">
                      <td className="p-4 font-mono text-indigo-600 font-bold">{line.accountCode}</td>
                      <td className="p-4 font-medium text-slate-700 dark:text-slate-200">{line.accountName}</td>
                      <td className="p-4 text-right text-slate-600 dark:text-slate-300">{line.debitBalance > 0 ? formatCurrency(line.debitBalance) : '-'}</td>
                      <td className="p-4 text-right text-slate-600 dark:text-slate-300">{line.creditBalance > 0 ? formatCurrency(line.creditBalance) : '-'}</td>
                    </tr>
                  ))}
                  <tr className="bg-slate-50 dark:bg-slate-800/50 font-bold border-t-2 border-slate-200 dark:border-slate-700">
                    <td className="p-4 text-right uppercase text-slate-500" colSpan={2}>Total</td>
                    <td className="p-4 text-right text-indigo-600 dark:text-indigo-400">{formatCurrency(data.totalDebits)}</td>
                    <td className="p-4 text-right text-indigo-600 dark:text-indigo-400">{formatCurrency(data.totalCredits)}</td>
                  </tr>
                  {data.totalDebits !== data.totalCredits && (
                    <tr>
                      <td colSpan={4} className="p-4 bg-rose-50 text-rose-600 text-center font-bold">
                        Warning: Trial Balance does not match. Difference: {formatCurrency(Math.abs(data.totalDebits - data.totalCredits))}
                      </td>
                    </tr>
                  )}
                </>
              ) : (
                <tr><td colSpan={4} className="p-8 text-center text-slate-500">No data available for selected date.</td></tr>
              )}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
};
