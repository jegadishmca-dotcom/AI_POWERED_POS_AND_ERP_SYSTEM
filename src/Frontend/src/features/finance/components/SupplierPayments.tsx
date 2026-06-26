import React, { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { getSupplierPayments, SupplierPayment } from '../services/finance.service';
import { 
  Banknote, 
  Search, 
  Plus, 
  Download, 
  ArrowUpDown, 
  ChevronLeft, 
  ChevronRight, 
  Calendar,
  AlertCircle,
  Tag
} from 'lucide-react';
import { formatCurrency } from '../../../utils/formatters';

export const SupplierPayments: React.FC = () => {
  const [search, setSearch] = useState('');
  const [sortBy, setSortBy] = useState<keyof SupplierPayment>('paymentDate');
  const [sortOrder, setSortOrder] = useState<'asc' | 'desc'>('desc');
  const [page, setPage] = useState(1);
  const pageSize = 10;

  const { data: payments = [], isLoading, error } = useQuery({
    queryKey: ['supplierPayments'],
    queryFn: () => getSupplierPayments()
  });

  const handleSort = (field: keyof SupplierPayment) => {
    if (sortBy === field) {
      setSortOrder(sortOrder === 'asc' ? 'desc' : 'asc');
    } else {
      setSortBy(field);
      setSortOrder('desc');
    }
  };

  // Filter payments
  const filteredPayments = payments.filter(pmt => {
    return (
      pmt.paymentNumber.toLowerCase().includes(search.toLowerCase()) ||
      pmt.supplierName.toLowerCase().includes(search.toLowerCase()) ||
      (pmt.referenceNumber && pmt.referenceNumber.toLowerCase().includes(search.toLowerCase()))
    );
  });

  // Sort payments
  const sortedPayments = [...filteredPayments].sort((a, b) => {
    let aVal = a[sortBy] ?? '';
    let bVal = b[sortBy] ?? '';
    
    if (typeof aVal === 'string') {
      return sortOrder === 'asc' 
        ? aVal.localeCompare(bVal as string) 
        : (bVal as string).localeCompare(aVal);
    } else {
      return sortOrder === 'asc'
        ? (aVal as number) - (bVal as number)
        : (bVal as number) - (aVal as number);
    }
  });

  // Paginate payments
  const totalItems = sortedPayments.length;
  const totalPages = Math.ceil(totalItems / pageSize);
  const paginatedPayments = sortedPayments.slice((page - 1) * pageSize, page * pageSize);

  const getModeBadgeClass = (mode: string) => {
    switch (mode.toUpperCase()) {
      case 'CASH':
        return 'bg-emerald-100 text-emerald-800 dark:bg-emerald-950/40 dark:text-emerald-400 border-emerald-200 dark:border-emerald-800/40';
      case 'BANK_TRANSFER':
      case 'BANK':
        return 'bg-indigo-100 text-indigo-800 dark:bg-indigo-950/40 dark:text-indigo-400 border-indigo-200 dark:border-indigo-800/40';
      case 'UPI':
        return 'bg-violet-100 text-violet-800 dark:bg-violet-950/40 dark:text-violet-400 border-violet-200 dark:border-violet-800/40';
      default:
        return 'bg-slate-100 text-slate-800 dark:bg-slate-950/40 dark:text-slate-400 border-slate-200 dark:border-slate-800/40';
    }
  };

  return (
    <div className="p-6 max-w-7xl mx-auto space-y-6">
      {/* Header */}
      <div className="flex flex-col md:flex-row md:items-center justify-between gap-4">
        <div>
          <h2 className="text-2xl font-extrabold text-slate-800 dark:text-white flex items-center gap-3">
            <Banknote className="w-7 h-7 text-rose-600" />
            Supplier Payments
          </h2>
          <p className="text-slate-500 dark:text-slate-400 mt-1">Record and allocate vendor payments</p>
        </div>
        <div className="flex gap-3">
          <button className="bg-white dark:bg-slate-900 border border-slate-300 dark:border-slate-700 hover:bg-slate-50 dark:hover:bg-slate-800 text-slate-700 dark:text-slate-200 px-4 py-2.5 rounded-lg font-bold flex items-center gap-2 shadow-sm transition-all text-sm">
            <Download className="w-4 h-4" />
            Export
          </button>
          <button className="bg-rose-600 hover:bg-rose-700 text-white px-4 py-2.5 rounded-lg font-bold flex items-center gap-2 shadow-lg shadow-rose-600/30 transition-all text-sm">
            <Plus className="w-4 h-4" />
            Record Payment
          </button>
        </div>
      </div>

      {/* Search Input */}
      <div className="bg-white dark:bg-slate-900 p-4 rounded-xl border border-slate-200 dark:border-slate-800 flex shadow-sm">
        <div className="relative w-full">
          <Search className="absolute left-3.5 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-400" />
          <input
            type="text"
            placeholder="Search by payment number, vendor name, or reference transaction ID..."
            value={search}
            onChange={(e) => { setSearch(e.target.value); setPage(1); }}
            className="w-full pl-10 pr-4 py-2.5 bg-slate-50 dark:bg-slate-950 border border-slate-200 dark:border-slate-800 rounded-lg text-sm text-slate-800 dark:text-white placeholder-slate-400 focus:bg-white dark:focus:bg-slate-900 focus:ring-2 focus:ring-rose-500 outline-none transition-all"
          />
        </div>
      </div>

      {/* Main Table */}
      {isLoading ? (
        <div className="bg-white dark:bg-slate-900 rounded-xl border border-slate-200 dark:border-slate-800 p-12 text-center text-slate-400 font-bold">
          <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-rose-600 mx-auto mb-4"></div>
          Loading payments...
        </div>
      ) : error ? (
        <div className="bg-rose-50 dark:bg-rose-950/20 text-rose-600 p-6 rounded-xl border border-rose-200 dark:border-rose-800">
          <h3 className="font-extrabold text-lg flex items-center gap-2">
            <AlertCircle className="w-5 h-5" />
            Error loading payments
          </h3>
          <p className="text-sm mt-1">{(error as any)?.message}</p>
        </div>
      ) : paginatedPayments.length === 0 ? (
        <div className="bg-white dark:bg-slate-900 rounded-xl border border-slate-200 dark:border-slate-800 p-12 text-center text-slate-400">
          No supplier payments found.
        </div>
      ) : (
        <div className="bg-white dark:bg-slate-900 rounded-xl border border-slate-200 dark:border-slate-800 shadow-sm overflow-hidden">
          <div className="overflow-x-auto">
            <table className="w-full text-left border-collapse text-sm">
              <thead>
                <tr className="border-b border-slate-200 dark:border-slate-800 bg-slate-50/50 dark:bg-slate-900/50 text-slate-500 dark:text-slate-400 font-bold">
                  <th className="p-4 cursor-pointer hover:bg-slate-100 dark:hover:bg-slate-800" onClick={() => handleSort('paymentNumber')}>
                    <span className="flex items-center gap-1.5">
                      Payment ID
                      <ArrowUpDown className="w-3.5 h-3.5" />
                    </span>
                  </th>
                  <th className="p-4 cursor-pointer hover:bg-slate-100 dark:hover:bg-slate-800" onClick={() => handleSort('supplierName')}>
                    <span className="flex items-center gap-1.5">
                      Supplier
                      <ArrowUpDown className="w-3.5 h-3.5" />
                    </span>
                  </th>
                  <th className="p-4 cursor-pointer hover:bg-slate-100 dark:hover:bg-slate-800" onClick={() => handleSort('paymentDate')}>
                    <span className="flex items-center gap-1.5">
                      Payment Date
                      <ArrowUpDown className="w-3.5 h-3.5" />
                    </span>
                  </th>
                  <th className="p-4 cursor-pointer hover:bg-slate-100 dark:hover:bg-slate-800" onClick={() => handleSort('paymentMode')}>
                    <span className="flex items-center justify-center gap-1.5">
                      Mode
                      <ArrowUpDown className="w-3.5 h-3.5" />
                    </span>
                  </th>
                  <th className="p-4">Reference No</th>
                  <th className="p-4 text-right cursor-pointer hover:bg-slate-100 dark:hover:bg-slate-800" onClick={() => handleSort('amount')}>
                    <span className="flex items-center justify-end gap-1.5">
                      Paid Amount
                      <ArrowUpDown className="w-3.5 h-3.5" />
                    </span>
                  </th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-150 dark:divide-slate-800/80">
                {paginatedPayments.map(pmt => (
                  <tr key={pmt.id} className="hover:bg-slate-50 dark:hover:bg-slate-800/30 transition-colors">
                    <td className="p-4 font-bold text-slate-800 dark:text-slate-100">{pmt.paymentNumber}</td>
                    <td className="p-4 font-bold text-slate-700 dark:text-slate-300">{pmt.supplierName}</td>
                    <td className="p-4 text-slate-600 dark:text-slate-400">
                      <span className="flex items-center gap-2">
                        <Calendar className="w-4 h-4 text-slate-400" />
                        {new Date(pmt.paymentDate).toLocaleDateString()}
                      </span>
                    </td>
                    <td className="p-4 text-center">
                      <span className={`inline-block px-2.5 py-1 rounded-full text-xs font-bold uppercase tracking-wider border ${getModeBadgeClass(pmt.paymentMode)}`}>
                        {pmt.paymentMode.replace('_', ' ')}
                      </span>
                    </td>
                    <td className="p-4 text-slate-500 font-mono text-xs">{pmt.referenceNumber || '-'}</td>
                    <td className="p-4 text-right font-black text-emerald-600">
                      {formatCurrency(pmt.amount)}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          {/* Pagination */}
          {totalPages > 1 && (
            <div className="p-4 border-t border-slate-200 dark:border-slate-800 flex justify-between items-center bg-slate-50 dark:bg-slate-900/50">
              <span className="text-xs font-bold text-slate-500">
                Showing {((page - 1) * pageSize) + 1} to {Math.min(page * pageSize, totalItems)} of {totalItems} entries
              </span>
              <div className="flex gap-2">
                <button
                  onClick={() => setPage(p => Math.max(1, p - 1))}
                  disabled={page === 1}
                  className="p-2 border border-slate-300 dark:border-slate-700 rounded-lg text-slate-600 dark:text-slate-400 disabled:opacity-50 hover:bg-slate-100 dark:hover:bg-slate-800 transition-colors"
                >
                  <ChevronLeft className="w-4 h-4" />
                </button>
                <span className="px-4 py-2 text-sm font-bold text-slate-700 dark:text-slate-300 flex items-center">
                  Page {page} of {totalPages}
                </span>
                <button
                  onClick={() => setPage(p => Math.min(totalPages, p + 1))}
                  disabled={page === totalPages}
                  className="p-2 border border-slate-300 dark:border-slate-700 rounded-lg text-slate-600 dark:text-slate-400 disabled:opacity-50 hover:bg-slate-100 dark:hover:bg-slate-800 transition-colors"
                >
                  <ChevronRight className="w-4 h-4" />
                </button>
              </div>
            </div>
          )}
        </div>
      )}
    </div>
  );
};
