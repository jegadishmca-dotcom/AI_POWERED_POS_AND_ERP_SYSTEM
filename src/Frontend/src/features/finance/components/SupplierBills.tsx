import React, { useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { getSupplierBills, PurchaseBill } from '../services/finance.service';
import { exportToCsv } from '../../../utils/exportToCsv';
import { Modal } from '../../../components/common/Modal';
import { api } from '../../../utils/api';
import { 
  FileText, 
  Search, 
  Filter, 
  Plus, 
  Download, 
  ArrowUpDown, 
  ChevronLeft, 
  ChevronRight, 
  Calendar,
  AlertCircle
} from 'lucide-react';
import { formatCurrency } from '../../../utils/formatters';

export const SupplierBills: React.FC = () => {
  const queryClient = useQueryClient();
  const [search, setSearch] = useState('');
  const [statusFilter, setStatusFilter] = useState('');
  const [sortBy, setSortBy] = useState<keyof PurchaseBill>('billDate');
  const [sortOrder, setSortOrder] = useState<'asc' | 'desc'>('desc');
  const [page, setPage] = useState(1);
  const pageSize = 10;

  // Modal State
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [billNumber, setBillNumber] = useState('');
  const [billDate, setBillDate] = useState(new Date().toISOString().split('T')[0]);
  const [grnHeaderId, setGrnHeaderId] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const { data: bills = [], isLoading, error } = useQuery({
    queryKey: ['supplierBills'],
    queryFn: () => getSupplierBills()
  });

  const handleSort = (field: keyof PurchaseBill) => {
    if (sortBy === field) {
      setSortOrder(sortOrder === 'asc' ? 'desc' : 'asc');
    } else {
      setSortBy(field);
      setSortOrder('desc');
    }
  };

  const handleExport = () => {
    exportToCsv(filteredBills, 'Supplier_Bills_Report', [
      { key: 'billNumber', label: 'Bill #' },
      { key: 'supplierName', label: 'Supplier Name' },
      { key: 'billDate', label: 'Bill Date' },
      { key: 'dueDate', label: 'Due Date' },
      { key: 'totalAmount', label: 'Total Amount (₹)' },
      { key: 'status', label: 'Status' },
    ]);
  };

  const handleCreateBill = async (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMessage(null);
    if (!billNumber.trim()) {
      setErrorMessage('Vendor Bill Number is required.');
      return;
    }

    setIsSubmitting(true);
    try {
      await api.post('/api/AccountsPayable/bills', {
        billNumber: billNumber.trim(),
        billDate,
        grnHeaderId: grnHeaderId.trim() || undefined
      });
      queryClient.invalidateQueries({ queryKey: ['supplierBills'] });
      setIsModalOpen(false);
      setBillNumber('');
      setGrnHeaderId('');
    } catch (err: any) {
      setErrorMessage(err.response?.data?.message || err.message || 'Failed to enter supplier bill.');
    } finally {
      setIsSubmitting(false);
    }
  };

  // Filter bills
  const filteredBills = bills.filter(bill => {
    const matchesSearch = 
      bill.billNumber.toLowerCase().includes(search.toLowerCase()) ||
      bill.supplierName.toLowerCase().includes(search.toLowerCase());
    
    const matchesStatus = statusFilter === '' || bill.status === statusFilter;
    
    return matchesSearch && matchesStatus;
  });

  // Sort bills
  const sortedBills = [...filteredBills].sort((a, b) => {
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

  // Paginate bills
  const totalItems = sortedBills.length;
  const totalPages = Math.ceil(totalItems / pageSize);
  const paginatedBills = sortedBills.slice((page - 1) * pageSize, page * pageSize);

  const getStatusBadgeClass = (status: string) => {
    switch (status) {
      case 'PAID':
        return 'bg-emerald-100 text-emerald-800 dark:bg-emerald-950/40 dark:text-emerald-400 border-emerald-200 dark:border-emerald-800/40';
      case 'PARTIALLY_PAID':
        return 'bg-amber-100 text-amber-800 dark:bg-amber-950/40 dark:text-amber-400 border-amber-200 dark:border-amber-800/40';
      case 'PENDING_PAYMENT':
      default:
        return 'bg-rose-100 text-rose-800 dark:bg-rose-950/40 dark:text-rose-400 border-rose-200 dark:border-rose-800/40';
    }
  };

  const getStatusLabel = (status: string) => {
    return status.replace('_', ' ');
  };

  return (
    <div className="p-6 max-w-7xl mx-auto space-y-6">
      {/* Header */}
      <div className="flex flex-col md:flex-row md:items-center justify-between gap-4">
        <div>
          <h2 className="text-2xl font-extrabold text-slate-800 dark:text-white flex items-center gap-3">
            <FileText className="w-7 h-7 text-rose-600" />
            Supplier Bills (AP)
          </h2>
          <p className="text-slate-500 dark:text-slate-400 mt-1">Manage vendor invoices and accounts payable</p>
        </div>
        <div className="flex gap-3">
          <button 
            onClick={handleExport}
            className="bg-white dark:bg-slate-900 border border-slate-300 dark:border-slate-700 hover:bg-slate-50 dark:hover:bg-slate-800 text-slate-700 dark:text-slate-200 px-4 py-2.5 rounded-lg font-bold flex items-center gap-2 shadow-sm transition-all text-sm cursor-pointer"
          >
            <Download className="w-4 h-4" />
            Export
          </button>
          <button 
            onClick={() => setIsModalOpen(true)}
            className="bg-rose-600 hover:bg-rose-700 text-white px-4 py-2.5 rounded-lg font-bold flex items-center gap-2 shadow-lg shadow-rose-600/30 transition-all text-sm cursor-pointer"
          >
            <Plus className="w-4 h-4" />
            Enter Bill
          </button>
        </div>
      </div>

      {/* Filters and Search */}
      <div className="bg-white dark:bg-slate-900 p-4 rounded-xl border border-slate-200 dark:border-slate-800 flex flex-col md:flex-row gap-4 justify-between items-center shadow-sm">
        <div className="relative w-full md:max-w-md">
          <Search className="absolute left-3.5 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-400" />
          <input
            type="text"
            placeholder="Search by bill number or supplier name..."
            value={search}
            onChange={(e) => { setSearch(e.target.value); setPage(1); }}
            className="w-full pl-10 pr-4 py-2.5 bg-slate-50 dark:bg-slate-950 border border-slate-200 dark:border-slate-800 rounded-lg text-sm text-slate-800 dark:text-white placeholder-slate-400 focus:bg-white dark:focus:bg-slate-900 focus:ring-2 focus:ring-rose-500 outline-none transition-all"
          />
        </div>
        <div className="flex gap-3 w-full md:w-auto">
          <div className="relative flex-1 md:w-[180px]">
            <Filter className="absolute left-3.5 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-400" />
            <select
              value={statusFilter}
              onChange={(e) => { setStatusFilter(e.target.value); setPage(1); }}
              className="w-full pl-10 pr-4 py-2.5 bg-slate-50 dark:bg-slate-950 border border-slate-200 dark:border-slate-800 rounded-lg text-sm text-slate-800 dark:text-white focus:bg-white dark:focus:bg-slate-900 focus:ring-2 focus:ring-rose-500 outline-none transition-all cursor-pointer"
            >
              <option value="">All Statuses</option>
              <option value="PENDING_PAYMENT">Pending Payment</option>
              <option value="PARTIALLY_PAID">Partially Paid</option>
              <option value="PAID">Paid</option>
            </select>
          </div>
        </div>
      </div>

      {/* Main Table */}
      {isLoading ? (
        <div className="bg-white dark:bg-slate-900 rounded-xl border border-slate-200 dark:border-slate-800 p-12 text-center text-slate-400 font-bold">
          <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-rose-600 mx-auto mb-4"></div>
          Loading supplier bills...
        </div>
      ) : error ? (
        <div className="bg-rose-50 dark:bg-rose-950/20 text-rose-600 p-6 rounded-xl border border-rose-200 dark:border-rose-800">
          <h3 className="font-extrabold text-lg flex items-center gap-2">
            <AlertCircle className="w-5 h-5" />
            Error loading supplier bills
          </h3>
          <p className="text-sm mt-1">{(error as any)?.message}</p>
        </div>
      ) : paginatedBills.length === 0 ? (
        <div className="bg-white dark:bg-slate-900 rounded-xl border border-slate-200 dark:border-slate-800 p-12 text-center text-slate-400">
          No supplier bills found.
        </div>
      ) : (
        <div className="bg-white dark:bg-slate-900 rounded-xl border border-slate-200 dark:border-slate-800 shadow-sm overflow-hidden">
          <div className="overflow-x-auto">
            <table className="w-full text-left border-collapse text-sm">
              <thead>
                <tr className="border-b border-slate-200 dark:border-slate-800 bg-slate-50/50 dark:bg-slate-900/50 text-slate-500 dark:text-slate-400 font-bold">
                  <th className="p-4 cursor-pointer hover:bg-slate-100 dark:hover:bg-slate-800" onClick={() => handleSort('billNumber')}>
                    <span className="flex items-center gap-1.5">
                      Bill #
                      <ArrowUpDown className="w-3.5 h-3.5" />
                    </span>
                  </th>
                  <th className="p-4 cursor-pointer hover:bg-slate-100 dark:hover:bg-slate-800" onClick={() => handleSort('supplierName')}>
                    <span className="flex items-center gap-1.5">
                      Supplier Name
                      <ArrowUpDown className="w-3.5 h-3.5" />
                    </span>
                  </th>
                  <th className="p-4 cursor-pointer hover:bg-slate-100 dark:hover:bg-slate-800" onClick={() => handleSort('billDate')}>
                    <span className="flex items-center gap-1.5">
                      Bill Date
                      <ArrowUpDown className="w-3.5 h-3.5" />
                    </span>
                  </th>
                  <th className="p-4 cursor-pointer hover:bg-slate-100 dark:hover:bg-slate-800" onClick={() => handleSort('dueDate')}>
                    <span className="flex items-center gap-1.5">
                      Due Date
                      <ArrowUpDown className="w-3.5 h-3.5" />
                    </span>
                  </th>
                  <th className="p-4 text-center cursor-pointer hover:bg-slate-100 dark:hover:bg-slate-800" onClick={() => handleSort('status')}>
                    <span className="flex items-center justify-center gap-1.5">
                      Status
                      <ArrowUpDown className="w-3.5 h-3.5" />
                    </span>
                  </th>
                  <th className="p-4 text-right cursor-pointer hover:bg-slate-100 dark:hover:bg-slate-800" onClick={() => handleSort('totalAmount')}>
                    <span className="flex items-center justify-end gap-1.5">
                      Total Amount
                      <ArrowUpDown className="w-3.5 h-3.5" />
                    </span>
                  </th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-150 dark:divide-slate-800/80">
                {paginatedBills.map(bill => (
                  <tr key={bill.id} className="hover:bg-slate-50 dark:hover:bg-slate-800/30 transition-colors">
                    <td className="p-4 font-mono font-bold text-slate-800 dark:text-slate-100">{bill.billNumber}</td>
                    <td className="p-4 font-bold text-slate-700 dark:text-slate-300">{bill.supplierName}</td>
                    <td className="p-4 text-slate-600 dark:text-slate-400">
                      <span className="flex items-center gap-2">
                        <Calendar className="w-4 h-4 text-slate-400" />
                        {new Date(bill.billDate).toLocaleDateString()}
                      </span>
                    </td>
                    <td className="p-4 text-slate-600 dark:text-slate-400">
                      <span className="flex items-center gap-2">
                        <Calendar className="w-4 h-4 text-slate-400" />
                        {bill.dueDate ? new Date(bill.dueDate).toLocaleDateString() : '-'}
                      </span>
                    </td>
                    <td className="p-4 text-center">
                      <span className={`inline-block px-2.5 py-1 rounded-full text-xs font-bold uppercase tracking-wider border ${getStatusBadgeClass(bill.status)}`}>
                        {getStatusLabel(bill.status)}
                      </span>
                    </td>
                    <td className="p-4 text-right font-black text-slate-800 dark:text-white">
                      {formatCurrency(bill.totalAmount)}
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

      {/* ENTER BILL MODAL */}
      <Modal
        isOpen={isModalOpen}
        onClose={() => setIsModalOpen(false)}
        title="Enter Supplier Bill (AP)"
      >
        <form onSubmit={handleCreateBill} className="space-y-4">
          {errorMessage && (
            <div className="p-3 bg-red-50 border border-red-200 dark:bg-red-950/40 dark:border-red-800 rounded-xl text-red-600 dark:text-red-300 text-sm flex items-center gap-2">
              <AlertCircle className="w-4 h-4 shrink-0" />
              <span>{errorMessage}</span>
            </div>
          )}

          <div>
            <label className="block text-xs font-bold text-slate-600 dark:text-slate-400 uppercase mb-1">Vendor Bill Number *</label>
            <input
              type="text"
              required
              placeholder="e.g. INV-2026-904"
              value={billNumber}
              onChange={(e) => setBillNumber(e.target.value)}
              className="w-full px-3 py-2 border border-slate-300 dark:border-slate-700 bg-white dark:bg-slate-900 rounded-lg text-sm dark:text-white outline-none focus:ring-2 focus:ring-rose-500 font-mono"
            />
          </div>

          <div>
            <label className="block text-xs font-bold text-slate-600 dark:text-slate-400 uppercase mb-1">Bill Date *</label>
            <input
              type="date"
              required
              value={billDate}
              onChange={(e) => setBillDate(e.target.value)}
              className="w-full px-3 py-2 border border-slate-300 dark:border-slate-700 bg-white dark:bg-slate-900 rounded-lg text-sm dark:text-white outline-none focus:ring-2 focus:ring-rose-500"
            />
          </div>

          <div>
            <label className="block text-xs font-bold text-slate-600 dark:text-slate-400 uppercase mb-1">GRN Header ID (Optional)</label>
            <input
              type="text"
              placeholder="GUID for linked Goods Receipt Note..."
              value={grnHeaderId}
              onChange={(e) => setGrnHeaderId(e.target.value)}
              className="w-full px-3 py-2 border border-slate-300 dark:border-slate-700 bg-white dark:bg-slate-900 rounded-lg text-sm dark:text-white outline-none focus:ring-2 focus:ring-rose-500 font-mono"
            />
          </div>

          <div className="pt-4 flex justify-end gap-3 border-t border-slate-200 dark:border-slate-800">
            <button
              type="button"
              onClick={() => setIsModalOpen(false)}
              className="px-4 py-2 text-sm font-semibold text-slate-600 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-800 rounded-lg transition cursor-pointer"
            >
              Cancel
            </button>
            <button
              type="submit"
              disabled={isSubmitting}
              className="px-5 py-2 text-sm font-bold text-white bg-rose-600 hover:bg-rose-700 disabled:opacity-50 rounded-lg shadow-md shadow-rose-600/30 transition flex items-center gap-2 cursor-pointer"
            >
              {isSubmitting ? 'Saving...' : 'Enter Bill'}
            </button>
          </div>
        </form>
      </Modal>
    </div>
  );
};
