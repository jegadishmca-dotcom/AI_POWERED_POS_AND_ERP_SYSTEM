import React, { useState, useEffect } from 'react';
import { CalendarClock, Download } from 'lucide-react';
import { api } from '../../../utils/api';
import { formatCurrency } from '../../../utils/formatters';

interface AgingDto {
  customerId: string;
  customerName: string;
  totalOutstanding: number;
  current: number;
  overdue1To30: number;
  overdue31To60: number;
  overdue61To90: number;
  overdue90Plus: number;
}

export const ARAging: React.FC = () => {
  const [asOfDate, setAsOfDate] = useState<string>(new Date().toISOString().split('T')[0]);
  const [agingData, setAgingData] = useState<AgingDto[]>([]);
  const [loading, setLoading] = useState<boolean>(false);
  const [error, setError] = useState<string>('');

  useEffect(() => {
    setLoading(true);
    setError('');
    const storeId = '00000000-0000-0000-0000-000000000000';
    api.get(`/api/AccountsReceivable/aging?storeId=${storeId}&asOfDate=${asOfDate}`)
      .then(res => {
        setAgingData(res.data || []);
      })
      .catch(err => {
        console.error('Failed to fetch AR aging', err);
        setError('Failed to fetch AR aging report.');
      })
      .finally(() => {
        setLoading(false);
      });
  }, [asOfDate]);

  const handleExport = () => {
    if (agingData.length === 0) return;
    const headers = ['Customer Name', 'Total Outstanding', 'Current', '1-30 Days', '31-60 Days', '61-90 Days', '90+ Days'];
    const rows = agingData.map(e => [
      e.customerName,
      e.totalOutstanding,
      e.current,
      e.overdue1To30,
      e.overdue31To60,
      e.overdue61To90,
      e.overdue90Plus
    ]);
    const csvContent = "data:text/csv;charset=utf-8," 
      + [headers.join(','), ...rows.map(r => r.join(','))].join('\n');
    const encodedUri = encodeURI(csvContent);
    const link = document.createElement("a");
    link.setAttribute("href", encodedUri);
    link.setAttribute("download", `ARAgingReport_${asOfDate}.csv`);
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
  };

  return (
    <div className="p-6 max-w-7xl mx-auto">
      <div className="flex flex-col md:flex-row md:items-center justify-between mb-8 gap-4 border-b pb-4">
        <div>
          <h2 className="text-2xl font-extrabold text-slate-800 dark:text-white flex items-center gap-3">
            <CalendarClock className="w-7 h-7 text-blue-600" />
            Accounts Receivable (AR) Aging
          </h2>
          <p className="text-slate-500 dark:text-slate-400 mt-1 font-semibold">Receivables aging analysis (0-30, 31-60, 61-90, 90+ days)</p>
        </div>
        {agingData.length > 0 && (
          <button 
            onClick={handleExport}
            className="bg-white hover:bg-slate-50 text-slate-700 border border-slate-300 px-4 py-2.5 rounded-lg font-bold flex items-center gap-2 shadow-sm transition-all"
          >
            <Download className="w-5 h-5" />
            Export CSV
          </button>
        )}
      </div>

      <div className="bg-white dark:bg-slate-900 rounded-xl shadow-sm border border-slate-200 dark:border-slate-800 p-6 mb-6">
        <div className="max-w-md">
          <label className="block text-sm font-bold text-slate-700 dark:text-slate-300 mb-2">As Of Date</label>
          <input
            type="date"
            value={asOfDate}
            onChange={(e) => setAsOfDate(e.target.value)}
            className="w-full px-3 py-2 bg-white dark:bg-slate-950 border border-slate-300 dark:border-slate-700 rounded-lg text-sm focus:ring-2 focus:ring-blue-500 outline-none text-slate-900 dark:text-white font-semibold"
          />
        </div>
      </div>

      {error && <div className="bg-rose-50 border border-rose-200 text-rose-700 p-4 rounded-lg font-semibold mb-6">{error}</div>}

      <div className="bg-white dark:bg-slate-900 rounded-xl shadow-sm border border-slate-200 dark:border-slate-800 overflow-hidden">
        {loading ? (
          <div className="p-12 text-center text-slate-500 font-semibold">Loading aging analysis...</div>
        ) : agingData.length === 0 ? (
          <div className="p-12 text-center text-slate-500 font-semibold">No outstanding receivables found as of this date.</div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-left border-collapse">
              <thead>
                <tr className="bg-slate-100 dark:bg-slate-950 border-b border-slate-200 dark:border-slate-800 text-xs font-extrabold text-slate-500 uppercase tracking-wider">
                  <th className="p-4">Customer Name</th>
                  <th className="p-4 text-right font-extrabold">Total Outstanding</th>
                  <th className="p-4 text-right bg-emerald-50/50 dark:bg-emerald-950/20 font-bold">Current</th>
                  <th className="p-4 text-right bg-amber-50/50 dark:bg-amber-950/20 font-bold">1 - 30 Days</th>
                  <th className="p-4 text-right bg-orange-50/50 dark:bg-orange-950/20 font-bold">31 - 60 Days</th>
                  <th className="p-4 text-right bg-red-50/50 dark:bg-red-950/20 font-bold">61 - 90 Days</th>
                  <th className="p-4 text-right bg-rose-50/50 dark:bg-rose-950/20 font-bold">90+ Days</th>
                </tr>
              </thead>
              <tbody className="text-sm font-semibold">
                {agingData.map((e, idx) => (
                  <tr key={e.customerId || idx} className="border-b border-slate-100 dark:border-slate-800/50 hover:bg-slate-50/50 dark:hover:bg-slate-800/30">
                    <td className="p-4 font-bold text-slate-850 dark:text-slate-200">{e.customerName}</td>
                    <td className="p-4 text-right text-slate-900 dark:text-white font-extrabold">{formatCurrency(e.totalOutstanding)}</td>
                    <td className="p-4 text-right text-emerald-600 bg-emerald-50/10 dark:bg-emerald-950/5">{formatCurrency(e.current)}</td>
                    <td className="p-4 text-right text-amber-600 bg-amber-50/10 dark:bg-amber-950/5">{formatCurrency(e.overdue1To30)}</td>
                    <td className="p-4 text-right text-orange-600 bg-orange-50/10 dark:bg-orange-950/5">{formatCurrency(e.overdue31To60)}</td>
                    <td className="p-4 text-right text-red-650 bg-red-50/10 dark:bg-red-950/5">{formatCurrency(e.overdue61To90)}</td>
                    <td className="p-4 text-right text-rose-700 bg-rose-50/10 dark:bg-rose-950/5">{formatCurrency(e.overdue90Plus)}</td>
                  </tr>
                ))}
                <tr className="bg-slate-50 dark:bg-slate-950 border-t-2 border-slate-200 dark:border-slate-800 text-slate-900 dark:text-white font-extrabold">
                  <td className="p-4">Total</td>
                  <td className="p-4 text-right">{formatCurrency(agingData.reduce((sum, e) => sum + e.totalOutstanding, 0))}</td>
                  <td className="p-4 text-right text-emerald-650">{formatCurrency(agingData.reduce((sum, e) => sum + e.current, 0))}</td>
                  <td className="p-4 text-right text-amber-650">{formatCurrency(agingData.reduce((sum, e) => sum + e.overdue1To30, 0))}</td>
                  <td className="p-4 text-right text-orange-650">{formatCurrency(agingData.reduce((sum, e) => sum + e.overdue31To60, 0))}</td>
                  <td className="p-4 text-right text-red-700">{formatCurrency(agingData.reduce((sum, e) => sum + e.overdue61To90, 0))}</td>
                  <td className="p-4 text-right text-rose-800">{formatCurrency(agingData.reduce((sum, e) => sum + e.overdue90Plus, 0))}</td>
                </tr>
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  );
};
