import React, { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { getJournalEntries } from '../services/finance.service';
import { FileText, Plus, Search } from 'lucide-react';
import { formatCurrency } from '../../../utils/formatters';

export const JournalEntries: React.FC = () => {
  const [startDate, setStartDate] = useState(() => {
    const d = new Date();
    d.setMonth(d.getMonth() - 1);
    return d.toISOString().split('T')[0];
  });
  const [endDate, setEndDate] = useState(new Date().toISOString().split('T')[0]);
  const [storeId, setStoreId] = useState('');
  const [searchTerm, setSearchTerm] = useState('');

  const { data: entries, isLoading } = useQuery({
    queryKey: ['journalEntries', storeId, startDate, endDate],
    queryFn: () => getJournalEntries(storeId || undefined, startDate, endDate)
  });

  const filteredEntries = entries?.filter((entry: any) => {
    const entryNum = entry.entryNumber || '';
    const desc = entry.description || '';
    return entryNum.toLowerCase().includes(searchTerm.toLowerCase()) || 
           desc.toLowerCase().includes(searchTerm.toLowerCase());
  }) || [];

  return (
    <div className="p-6 max-w-7xl mx-auto">
      <div className="flex flex-col md:flex-row md:items-center justify-between mb-8 gap-4">
        <div>
          <h2 className="text-2xl font-extrabold text-slate-800 dark:text-white flex items-center gap-3">
            <FileText className="w-7 h-7 text-indigo-600" />
            Journal Entries
          </h2>
          <p className="text-slate-500 dark:text-slate-400 mt-1">General ledger transactions and manual journals</p>
        </div>
        <button className="bg-indigo-600 hover:bg-indigo-700 text-white px-4 py-2.5 rounded-lg font-bold flex items-center gap-2 shadow-lg shadow-indigo-600/30 transition-all">
          <Plus className="w-5 h-5" />
          New Journal Entry
        </button>
      </div>

      <div className="bg-white dark:bg-slate-900 rounded-xl shadow-sm border border-slate-200 dark:border-slate-800 overflow-hidden mb-6">
        <div className="p-4 bg-slate-50 dark:bg-slate-950 flex flex-wrap gap-4 items-end">
          <div className="flex-1 min-w-[250px]">
            <label className="block text-xs font-bold text-slate-500 uppercase tracking-wider mb-1.5">Search</label>
            <div className="relative">
              <Search className="w-5 h-5 absolute left-3 top-2.5 text-slate-400" />
              <input 
                type="text" 
                placeholder="Entry Number or Description..."
                value={searchTerm}
                onChange={(e) => setSearchTerm(e.target.value)}
                className="w-full pl-10 pr-3 py-2 bg-white dark:bg-slate-900 border border-slate-300 dark:border-slate-700 rounded-lg text-sm text-slate-900 dark:text-white focus:ring-2 focus:ring-indigo-500 outline-none"
              />
            </div>
          </div>
          <div className="w-[160px]">
            <label className="block text-xs font-bold text-slate-500 uppercase tracking-wider mb-1.5">From Date</label>
            <input 
              type="date" 
              value={startDate}
              onChange={(e) => setStartDate(e.target.value)}
              className="w-full px-3 py-2 bg-white dark:bg-slate-900 border border-slate-300 dark:border-slate-700 rounded-lg text-sm text-slate-900 dark:text-white focus:ring-2 focus:ring-indigo-500 outline-none"
            />
          </div>
          <div className="w-[160px]">
            <label className="block text-xs font-bold text-slate-500 uppercase tracking-wider mb-1.5">To Date</label>
            <input 
              type="date" 
              value={endDate}
              onChange={(e) => setEndDate(e.target.value)}
              className="w-full px-3 py-2 bg-white dark:bg-slate-900 border border-slate-300 dark:border-slate-700 rounded-lg text-sm text-slate-900 dark:text-white focus:ring-2 focus:ring-indigo-500 outline-none"
            />
          </div>
          <div className="w-[200px]">
            <label className="block text-xs font-bold text-slate-500 uppercase tracking-wider mb-1.5">Store</label>
            <select
              value={storeId}
              onChange={(e) => setStoreId(e.target.value)}
              className="w-full px-3 py-2 bg-white dark:bg-slate-900 border border-slate-300 dark:border-slate-700 rounded-lg text-sm text-slate-900 dark:text-white focus:ring-2 focus:ring-indigo-500 outline-none"
            >
              <option value="">All Stores</option>
            </select>
          </div>
        </div>
      </div>

      <div className="space-y-4">
        {isLoading ? (
          <div className="flex justify-center items-center h-48 text-slate-400">Loading Journal Entries...</div>
        ) : filteredEntries.length > 0 ? (
          filteredEntries.map((entry: any) => (
            <div key={entry.id} className="bg-white dark:bg-slate-900 rounded-xl shadow-sm border border-slate-200 dark:border-slate-800 overflow-hidden">
              <div className="p-4 bg-slate-50 dark:bg-slate-950 border-b border-slate-200 dark:border-slate-800 flex justify-between items-center">
                <div>
                  <h3 className="font-bold text-indigo-600 dark:text-indigo-400 text-lg flex items-center gap-2">
                    {entry.entryNumber}
                    {entry.isPosted ? (
                      <span className="text-[10px] bg-emerald-100 text-emerald-800 px-2 py-0.5 rounded-full uppercase tracking-widest border border-emerald-200">Posted</span>
                    ) : (
                      <span className="text-[10px] bg-amber-100 text-amber-800 px-2 py-0.5 rounded-full uppercase tracking-widest border border-amber-200">Draft</span>
                    )}
                  </h3>
                  <div className="text-sm text-slate-500 mt-1 flex items-center gap-3">
                    <span>{new Date(entry.entryDate).toLocaleDateString('en-IN')}</span>
                    <span className="w-1 h-1 rounded-full bg-slate-300"></span>
                    <span>Ref: {entry.referenceDocument || 'N/A'}</span>
                  </div>
                </div>
                <div className="text-right">
                  <p className="text-sm font-semibold text-slate-700 dark:text-slate-200">{entry.description}</p>
                </div>
              </div>
              <div className="p-0">
                <table className="w-full text-left text-sm">
                  <thead className="bg-slate-50/50 dark:bg-slate-900/50 border-b border-slate-100 dark:border-slate-800 text-xs font-bold text-slate-500 uppercase tracking-wider">
                    <tr>
                      <th className="p-3 pl-4 w-1/3">Account</th>
                      <th className="p-3 w-1/3">Line Description</th>
                      <th className="p-3 text-right">Debit</th>
                      <th className="p-3 pr-4 text-right">Credit</th>
                    </tr>
                  </thead>
                  <tbody>
                    {entry.lines?.map((line: any) => (
                      <tr key={line.id} className="border-b border-slate-50 dark:border-slate-800/50 last:border-0 hover:bg-slate-50/50 dark:hover:bg-slate-800/30">
                        <td className="p-3 pl-4">
                          <span className="font-mono text-indigo-500 mr-2">{line.account?.accountCode}</span>
                          <span className="text-slate-700 dark:text-slate-300">{line.account?.name}</span>
                        </td>
                        <td className="p-3 text-slate-600 dark:text-slate-400">{line.description}</td>
                        <td className="p-3 text-right text-slate-700 dark:text-slate-300">{line.debitAmount > 0 ? formatCurrency(line.debitAmount) : '-'}</td>
                        <td className="p-3 pr-4 text-right text-slate-700 dark:text-slate-300">{line.creditAmount > 0 ? formatCurrency(line.creditAmount) : '-'}</td>
                      </tr>
                    ))}
                    <tr className="bg-slate-50 dark:bg-slate-800/30 font-bold border-t border-slate-200 dark:border-slate-700">
                      <td colSpan={2} className="p-3 text-right text-slate-500 uppercase tracking-wider text-xs">Total</td>
                      <td className="p-3 text-right text-indigo-600">{formatCurrency(entry.lines?.reduce((sum: number, l: any) => sum + l.debitAmount, 0) || 0)}</td>
                      <td className="p-3 pr-4 text-right text-indigo-600">{formatCurrency(entry.lines?.reduce((sum: number, l: any) => sum + l.creditAmount, 0) || 0)}</td>
                    </tr>
                  </tbody>
                </table>
              </div>
            </div>
          ))
        ) : (
          <div className="text-center p-12 text-slate-500 bg-white dark:bg-slate-900 rounded-xl border border-slate-200 dark:border-slate-800">
            No journal entries found for the selected criteria.
          </div>
        )}
      </div>
    </div>
  );
};
