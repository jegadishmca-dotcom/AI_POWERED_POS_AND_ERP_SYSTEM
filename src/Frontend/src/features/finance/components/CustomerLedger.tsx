import React, { useState, useEffect } from 'react';
import { BookOpen, Download, Search, Calendar } from 'lucide-react';
import { api } from '../../../utils/api';
import { formatCurrency } from '../../../utils/formatters';

interface Customer {
  id: string;
  name: string;
  phone: string;
  runningLoyaltyPoints?: number;
  runningWalletBalance?: number;
  tierName?: string;
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

interface LoyaltyLedgerItem {
  id: string;
  createdAt: string;
  transactionType: string;
  referenceDocument: string;
  pointsEarned: number;
  pointsRedeemed: number;
  runningPoints: number;
  balanceAfterTransaction: number;
  remarks: string;
}

export const CustomerLedger: React.FC = () => {
  const [searchQuery, setSearchQuery] = useState<string>('');
  const [customers, setCustomers] = useState<Customer[]>([]);
  const [selectedCustomerId, setSelectedCustomerId] = useState<string>('');
  const [selectedCustomerObj, setSelectedCustomerObj] = useState<Customer | null>(null);
  
  const [activeTab, setActiveTab] = useState<'AR' | 'LOYALTY'>('AR');
  const [ledgerEntries, setLedgerEntries] = useState<LedgerEntry[]>([]);
  const [loyaltyEntries, setLoyaltyEntries] = useState<LoyaltyLedgerItem[]>([]);
  
  const [loading, setLoading] = useState<boolean>(false);
  const [error, setError] = useState<string>('');

  // Initial load of customers and load on search query change
  useEffect(() => {
    const delayDebounceFn = setTimeout(() => {
      api.get(`/api/customers/search?q=${searchQuery}`)
        .then(res => {
          setCustomers(res.data || []);
        })
        .catch(err => {
          console.error('Failed to search customers', err);
        });
    }, 300);

    return () => clearTimeout(delayDebounceFn);
  }, [searchQuery]);

  useEffect(() => {
    if (!selectedCustomerId) {
      setLedgerEntries([]);
      setLoyaltyEntries([]);
      setSelectedCustomerObj(null);
      return;
    }

    const foundCust = customers.find(c => c.id === selectedCustomerId) || null;
    setSelectedCustomerObj(foundCust);

    setLoading(true);
    setError('');
    const storeId = '00000000-0000-0000-0000-000000000000';

    Promise.all([
      api.get(`/api/AccountsReceivable/ledger?customerId=${selectedCustomerId}&storeId=${storeId}`),
      api.get(`/api/loyalty/customer/${selectedCustomerId}/ledger`)
    ])
      .then(([arRes, loyaltyRes]) => {
        setLedgerEntries(arRes.data || []);
        setLoyaltyEntries(loyaltyRes.data || []);
      })
      .catch(err => {
        console.error('Failed to fetch customer ledger details', err);
        setError('Failed to load ledger entries.');
      })
      .finally(() => {
        setLoading(false);
      });
  }, [selectedCustomerId]);

  const handleExport = () => {
    if (activeTab === 'AR') {
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
      const csvContent = "data:text/csv;charset=utf-8," + [headers.join(','), ...rows.map(r => r.join(','))].join('\n');
      const encodedUri = encodeURI(csvContent);
      const link = document.createElement("a");
      link.setAttribute("href", encodedUri);
      link.setAttribute("download", `Customer_Financial_Ledger_${selectedCustomerId}.csv`);
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);
    } else {
      if (loyaltyEntries.length === 0) return;
      const headers = ['Date', 'Type', 'Ref Document', 'Earned (+)', 'Redeemed (-)', 'Running Points', 'Remarks'];
      const rows = loyaltyEntries.map(e => [
        new Date(e.createdAt).toLocaleDateString('en-IN'),
        e.transactionType,
        e.referenceDocument || '-',
        e.pointsEarned,
        e.pointsRedeemed,
        e.runningPoints || e.balanceAfterTransaction,
        e.remarks || '-'
      ]);
      const csvContent = "data:text/csv;charset=utf-8," + [headers.join(','), ...rows.map(r => r.join(','))].join('\n');
      const encodedUri = encodeURI(csvContent);
      const link = document.createElement("a");
      link.setAttribute("href", encodedUri);
      link.setAttribute("download", `Customer_Loyalty_Ledger_${selectedCustomerId}.csv`);
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);
    }
  };

  return (
    <div className="p-6 max-w-7xl mx-auto">
      <div className="flex flex-col md:flex-row md:items-center justify-between mb-8 gap-4 border-b pb-4">
        <div>
          <h2 className="text-2xl font-extrabold text-slate-800 dark:text-white flex items-center gap-3">
            <BookOpen className="w-7 h-7 text-blue-600" />
            Customer Ledger (AR & Loyalty)
          </h2>
          <p className="text-slate-500 dark:text-slate-400 mt-1 font-semibold">Detailed financial and loyalty points transaction history by customer</p>
        </div>
        {((activeTab === 'AR' && ledgerEntries.length > 0) || (activeTab === 'LOYALTY' && loyaltyEntries.length > 0)) && (
          <button 
            onClick={handleExport}
            className="bg-white hover:bg-slate-50 text-slate-700 border border-slate-300 px-4 py-2.5 rounded-lg font-bold flex items-center gap-2 shadow-sm transition-all"
          >
            <Download className="w-5 h-5" />
            Export CSV
          </button>
        )}
      </div>

      <div className="bg-white dark:bg-slate-900 rounded-xl shadow-sm border border-slate-200 dark:border-slate-800 p-6 mb-6 grid grid-cols-1 md:grid-cols-2 gap-6">
        <div>
          <label className="block text-sm font-bold text-slate-700 dark:text-slate-300 mb-2">Search Customer Name / Phone</label>
          <div className="relative">
            <Search className="w-5 h-5 absolute left-3 top-2.5 text-slate-400" />
            <input
              type="text"
              placeholder="Type name or phone..."
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              className="w-full pl-10 pr-3 py-2 bg-white dark:bg-slate-950 border border-slate-300 dark:border-slate-700 rounded-lg text-sm focus:ring-2 focus:ring-blue-500 outline-none text-slate-900 dark:text-white font-bold"
            />
          </div>
        </div>

        <div>
          <label className="block text-sm font-bold text-slate-700 dark:text-slate-300 mb-2">Select Customer Result</label>
          <select
            value={selectedCustomerId}
            onChange={(e) => setSelectedCustomerId(e.target.value)}
            className="w-full px-3 py-2 bg-white dark:bg-slate-950 border border-slate-300 dark:border-slate-700 rounded-lg text-sm focus:ring-2 focus:ring-blue-500 outline-none text-slate-900 dark:text-white font-bold"
          >
            <option value="">-- Select Customer --</option>
            {customers.map(c => (
              <option key={c.id} value={c.id}>{c.name} ({c.phone})</option>
            ))}
          </select>
        </div>
      </div>

      {/* Selected Customer Summary Card */}
      {selectedCustomerObj && (
        <div className="bg-gradient-to-r from-indigo-900 via-slate-900 to-indigo-950 text-white rounded-xl p-5 mb-6 shadow-md border border-indigo-700 flex flex-wrap items-center justify-between gap-4">
          <div>
            <h3 className="text-xl font-extrabold flex items-center gap-2">
              {selectedCustomerObj.name}
              {selectedCustomerObj.tierName && (
                <span className="bg-amber-400 text-slate-950 text-xs px-2 py-0.5 rounded font-black">
                  {selectedCustomerObj.tierName}
                </span>
              )}
            </h3>
            <p className="text-xs text-indigo-200 mt-1 font-semibold">Phone: {selectedCustomerObj.phone || 'N/A'}</p>
          </div>

          <div className="flex items-center gap-6">
            <div className="bg-indigo-950/80 border border-amber-500/40 rounded-lg px-4 py-2 text-right">
              <span className="text-xs text-amber-300 font-bold uppercase tracking-wider block">Loyalty Points Balance</span>
              <span className="text-xl font-black text-amber-400">⭐ {selectedCustomerObj.runningLoyaltyPoints ?? 0} Pts</span>
            </div>

            <div className="bg-indigo-950/80 border border-indigo-700/60 rounded-lg px-4 py-2 text-right">
              <span className="text-xs text-indigo-300 font-bold uppercase tracking-wider block">Accounts Receivable (AR)</span>
              <span className="text-xl font-black text-emerald-400">{formatCurrency(selectedCustomerObj.runningWalletBalance ?? 0)}</span>
            </div>
          </div>
        </div>
      )}

      {/* Tab Switcher */}
      {selectedCustomerId && (
        <div className="flex border-b border-slate-200 dark:border-slate-800 mb-6 gap-2">
          <button
            onClick={() => setActiveTab('AR')}
            className={`py-2.5 px-5 font-black text-sm rounded-t-lg transition-all ${
              activeTab === 'AR'
                ? 'bg-blue-600 text-white shadow-sm'
                : 'bg-slate-100 dark:bg-slate-800 text-slate-600 dark:text-slate-400 hover:bg-slate-200'
            }`}
          >
            💵 Accounts Receivable (Financial Ledger)
          </button>
          <button
            onClick={() => setActiveTab('LOYALTY')}
            className={`py-2.5 px-5 font-black text-sm rounded-t-lg transition-all ${
              activeTab === 'LOYALTY'
                ? 'bg-amber-600 text-white shadow-sm'
                : 'bg-slate-100 dark:bg-slate-800 text-slate-600 dark:text-slate-400 hover:bg-slate-200'
            }`}
          >
            ⭐ Loyalty Points Ledger ({loyaltyEntries.length} Records)
          </button>
        </div>
      )}

      {error && <div className="bg-rose-50 border border-rose-200 text-rose-700 p-4 rounded-lg font-semibold mb-6">{error}</div>}

      <div className="bg-white dark:bg-slate-900 rounded-xl shadow-sm border border-slate-200 dark:border-slate-800 overflow-hidden">
        {loading ? (
          <div className="p-12 text-center text-slate-500 font-semibold">Loading ledger entries...</div>
        ) : !selectedCustomerId ? (
          <div className="p-12 text-center text-slate-400 font-semibold">Please search and select a customer to view the ledger.</div>
        ) : activeTab === 'AR' ? (
          ledgerEntries.length === 0 ? (
            <div className="p-12 text-center text-slate-500 font-semibold">No financial AR ledger transactions found for this customer.</div>
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
                          e.transactionType === 'INVOICE' ? 'bg-blue-100 text-blue-800' :
                          e.transactionType === 'RECEIPT' ? 'bg-emerald-100 text-emerald-800' :
                          'bg-slate-100 text-slate-800'
                        }`}>
                          {e.transactionType}
                        </span>
                      </td>
                      <td className="p-4 font-mono font-bold text-slate-800 dark:text-slate-200">{e.referenceNumber}</td>
                      <td className="p-4 text-slate-600 dark:text-slate-400 font-medium">{e.description}</td>
                      <td className="p-4 text-right text-blue-600 font-bold">{e.debitAmount > 0 ? formatCurrency(e.debitAmount) : '-'}</td>
                      <td className="p-4 text-right text-emerald-600 font-bold">{e.creditAmount > 0 ? formatCurrency(e.creditAmount) : '-'}</td>
                      <td className="p-4 text-right font-bold text-slate-900 dark:text-white">{formatCurrency(e.runningBalance)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )
        ) : (
          loyaltyEntries.length === 0 ? (
            <div className="p-12 text-center text-slate-500 font-semibold">No loyalty points transactions found for this customer.</div>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full text-left border-collapse">
                <thead>
                  <tr className="bg-amber-50 dark:bg-slate-950 border-b border-amber-200 dark:border-slate-800 text-xs font-extrabold text-amber-900 uppercase tracking-wider">
                    <th className="p-4">Date</th>
                    <th className="p-4">Transaction Type</th>
                    <th className="p-4">Ref Document</th>
                    <th className="p-4 text-right">Points Earned (+)</th>
                    <th className="p-4 text-right">Points Redeemed (-)</th>
                    <th className="p-4 text-right">Running Points Balance</th>
                    <th className="p-4">Remarks</th>
                  </tr>
                </thead>
                <tbody className="text-sm">
                  {loyaltyEntries.map((l, idx) => (
                    <tr key={l.id || idx} className="border-b border-slate-100 dark:border-slate-800/50 hover:bg-amber-50/30">
                      <td className="p-4 font-semibold text-slate-700 dark:text-slate-300">
                        <span className="flex items-center gap-2">
                          <Calendar className="w-4 h-4 text-amber-500" />
                          {new Date(l.createdAt).toLocaleDateString('en-IN')}
                        </span>
                      </td>
                      <td className="p-4">
                        <span className={`px-2 py-0.5 rounded-full text-xs font-black ${
                          l.transactionType === 'OPENING_BALANCE' ? 'bg-indigo-100 text-indigo-800 border border-indigo-200' :
                          l.transactionType.includes('Earn') ? 'bg-emerald-100 text-emerald-800' :
                          'bg-rose-100 text-rose-800'
                        }`}>
                          {l.transactionType}
                        </span>
                      </td>
                      <td className="p-4 font-mono font-extrabold text-slate-800 dark:text-slate-200">{l.referenceDocument || '-'}</td>
                      <td className="p-4 text-right text-emerald-600 font-black">{l.pointsEarned > 0 ? `+${l.pointsEarned}` : '-'}</td>
                      <td className="p-4 text-right text-rose-600 font-black">{l.pointsRedeemed > 0 ? `-${l.pointsRedeemed}` : '-'}</td>
                      <td className="p-4 text-right font-black text-amber-600 text-base">⭐ {l.runningPoints || l.balanceAfterTransaction} Pts</td>
                      <td className="p-4 text-slate-600 dark:text-slate-400 text-xs font-semibold">{l.remarks || '-'}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )
        )}
      </div>
    </div>
  );
};
