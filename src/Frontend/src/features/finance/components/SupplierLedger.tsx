import React, { useState, useEffect } from 'react';
import { BookOpen, Download, Calendar } from 'lucide-react';
import { api } from '../../../utils/api';
import { formatCurrency } from '../../../utils/formatters';

interface Supplier {
  id: string;
  name: string;
  code: string;
  supplierName?: string; // fallback
}

interface LedgerEntry {
  id: string;
  entryDate: string;
  transactionType: string;
  referenceNumber: string;
  debitAmount: number;
  creditAmount: number;
  runningBalance: number;
  description: string;
  journalEntryId?: string;
}

export const SupplierLedger: React.FC = () => {
  const [suppliers, setSuppliers] = useState<Supplier[]>([]);
  const [selectedSupplierId, setSelectedSupplierId] = useState<string>('');
  const [ledgerEntries, setLedgerEntries] = useState<LedgerEntry[]>([]);
  const [loading, setLoading] = useState<boolean>(false);
  const [error, setError] = useState<string>('');

  useEffect(() => {
    // Load suppliers list
    api.get('/api/suppliers')
      .then(res => {
        setSuppliers(res.data || []);
      })
      .catch(err => {
        console.error('Failed to fetch suppliers', err);
        setError('Failed to load suppliers.');
      });
  }, []);

  useEffect(() => {
    if (!selectedSupplierId) {
      setLedgerEntries([]);
      return;
    }

    setLoading(true);
    setError('');
    // Use default Head Office storeId
    const storeId = '00000000-0000-0000-0000-000000000000';
    api.get(`/api/AccountsPayable/ledger?supplierId=${selectedSupplierId}&storeId=${storeId}`)
      .then(res => {
        setLedgerEntries(res.data || []);
      })
      .catch(err => {
        console.error('Failed to fetch ledger', err);
        setError('Failed to fetch ledger entries.');
      })
      .finally(() => {
        setLoading(false);
      });
  }, [selectedSupplierId]);

  const handleExport = () => {
    // Basic CSV export
    if (ledgerEntries.length === 0) return;
    const headers = ['Date', 'Type', 'Ref Number', 'Description', 'Debit (Dr)', 'Credit (Cr)', 'Balance'];
    const rows = ledgerEntries.map(e => [
      new Date(e.entryDate).toLocaleDateString('en-IN'),
      e.transactionType,
      e.referenceNumber,
      e.description,
      e.debitAmount,
      e.creditAmount,
      e.runningBalance
    ]);
    const csvContent = "data:text/csv;charset=utf-8," 
      + [headers.join(','), ...rows.map(r => r.join(','))].join('\n');
    const encodedUri = encodeURI(csvContent);
    const link = document.createElement("a");
    link.setAttribute("href", encodedUri);
    link.setAttribute("download", `SupplierLedger_${selectedSupplierId}.csv`);
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
  };

  return (
    <div className="p-6 max-w-7xl mx-auto">
      <div className="flex flex-col md:flex-row md:items-center justify-between mb-8 gap-4 border-b pb-4">
        <div>
          <h2 className="text-2xl font-extrabold text-slate-800 dark:text-white flex items-center gap-3">
            <BookOpen className="w-7 h-7 text-rose-600" />
            Supplier Ledger (AP)
          </h2>
          <p className="text-slate-500 dark:text-slate-400 mt-1 font-semibold">Detailed transaction history by vendor</p>
        </div>
        {ledgerEntries.length > 0 && (
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
          <label className="block text-sm font-bold text-slate-700 dark:text-slate-300 mb-2">Select Vendor / Supplier</label>
          <select
            value={selectedSupplierId}
            onChange={(e) => setSelectedSupplierId(e.target.value)}
            className="w-full px-3 py-2 bg-white dark:bg-slate-950 border border-slate-300 dark:border-slate-700 rounded-lg text-sm focus:ring-2 focus:ring-rose-500 outline-none text-slate-900 dark:text-white font-semibold"
          >
            <option value="">-- Select Supplier --</option>
            {suppliers.map(s => (
              <option key={s.id} value={s.id}>{s.name || s.supplierName} ({s.code || 'Vendor'})</option>
            ))}
          </select>
        </div>
      </div>

      {error && <div className="bg-rose-50 border border-rose-200 text-rose-700 p-4 rounded-lg font-semibold mb-6">{error}</div>}

      <div className="bg-white dark:bg-slate-900 rounded-xl shadow-sm border border-slate-200 dark:border-slate-800 overflow-hidden">
        {loading ? (
          <div className="p-12 text-center text-slate-500 font-semibold">Loading ledger entries...</div>
        ) : !selectedSupplierId ? (
          <div className="p-12 text-center text-slate-400 font-semibold">Please select a supplier to view the ledger.</div>
        ) : ledgerEntries.length === 0 ? (
          <div className="p-12 text-center text-slate-500 font-semibold">No ledger transactions found for this supplier.</div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-left border-collapse">
              <thead>
                <tr className="bg-slate-100 dark:bg-slate-950 border-b border-slate-200 dark:border-slate-800 text-xs font-extrabold text-slate-500 uppercase tracking-wider">
                  <th className="p-4">Date</th>
                  <th className="p-4">Type</th>
                  <th className="p-4">Ref Number</th>
                  <th className="p-4">Description</th>
                  <th className="p-4 text-right">Debit (Dr)</th>
                  <th className="p-4 text-right">Credit (Cr)</th>
                  <th className="p-4 text-right">Balance</th>
                </tr>
              </thead>
              <tbody className="text-sm">
                {ledgerEntries.map((e, idx) => (
                  <tr key={e.id || idx} className="border-b border-slate-100 dark:border-slate-800/50 hover:bg-slate-50/50 dark:hover:bg-slate-800/30">
                    <td className="p-4 font-semibold text-slate-700 dark:text-slate-300">
                      <span className="flex items-center gap-2">
                        <Calendar className="w-4 h-4 text-slate-400" />
                        {new Date(e.entryDate).toLocaleDateString('en-IN')}
                      </span>
                    </td>
                    <td className="p-4">
                      <span className={`px-2 py-0.5 rounded-full text-xs font-bold ${
                        e.transactionType === 'BILL' ? 'bg-amber-100 text-amber-800' :
                        e.transactionType === 'PAYMENT' ? 'bg-emerald-100 text-emerald-800' :
                        'bg-slate-100 text-slate-800'
                      }`}>
                        {e.transactionType}
                      </span>
                    </td>
                    <td className="p-4 font-mono font-bold text-slate-800 dark:text-slate-200">{e.referenceNumber}</td>
                    <td className="p-4 text-slate-600 dark:text-slate-400 font-medium">{e.description}</td>
                    <td className="p-4 text-right text-rose-600 font-bold">{e.debitAmount > 0 ? formatCurrency(e.debitAmount) : '-'}</td>
                    <td className="p-4 text-right text-emerald-600 font-bold">{e.creditAmount > 0 ? formatCurrency(e.creditAmount) : '-'}</td>
                    <td className="p-4 text-right font-bold text-slate-900 dark:text-white">{formatCurrency(e.runningBalance)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  );
};
