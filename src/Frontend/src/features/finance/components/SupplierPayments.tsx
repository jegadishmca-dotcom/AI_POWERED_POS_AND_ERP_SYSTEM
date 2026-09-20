import React, { useState, useEffect } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { getSupplierPayments, SupplierPayment } from '../services/finance.service';
import { exportToCsv } from '../../../utils/exportToCsv';
import { Modal } from '../../../components/common/Modal';
import { api } from '../../../utils/api';
import { useAuthStore } from '../../auth/store/auth.store';
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
  Building2,
  FileText
} from 'lucide-react';
import { formatCurrency } from '../../../utils/formatters';

export const SupplierPayments: React.FC = () => {
  const queryClient = useQueryClient();
  const { user } = useAuthStore();
  const [search, setSearch] = useState('');
  const [sortBy, setSortBy] = useState<keyof SupplierPayment>('paymentDate');
  const [sortOrder, setSortOrder] = useState<'asc' | 'desc'>('desc');
  const [page, setPage] = useState(1);
  const pageSize = 10;

  // Modal State
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [selectedSupplierId, setSelectedSupplierId] = useState('');
  const [amount, setAmount] = useState('');
  const [paymentDate, setPaymentDate] = useState(new Date().toISOString().split('T')[0]);
  const [paymentMode, setPaymentMode] = useState('BANK_TRANSFER');
  const [referenceNumber, setReferenceNumber] = useState('');
  const [notes, setNotes] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  // Suppliers & Bills for Dropdown & Auto-fill
  const [suppliers, setSuppliers] = useState<any[]>([]);
  const [pendingBills, setPendingBills] = useState<any[]>([]);
  const [loadingBills, setLoadingBills] = useState(false);
  const [totalOutstanding, setTotalOutstanding] = useState(0);

  // Fetch active suppliers when modal opens
  useEffect(() => {
    if (isModalOpen && suppliers.length === 0) {
      api.get('/api/suppliers')
        .then(res => {
          const active = (res.data || []).filter((s: any) => s.isActive !== false);
          setSuppliers(active);
        })
        .catch(err => console.error('Failed to load suppliers', err));
    }
  }, [isModalOpen]);

  // When supplier is selected, fetch unpaid bills and auto-calculate outstanding
  const handleSupplierChange = async (suppId: string) => {
    setSelectedSupplierId(suppId);
    setErrorMessage(null);
    if (!suppId) {
      setPendingBills([]);
      setTotalOutstanding(0);
      setAmount('');
      return;
    }

    setLoadingBills(true);
    try {
      const res = await api.get(`/api/accountspayable/bills?supplierId=${suppId}`);
      const bills = (res.data || []).filter((b: any) => b.status === 'PENDING_PAYMENT' || b.status === 'PARTIALLY_PAID');
      setPendingBills(bills);
      const outstanding = bills.reduce((sum: number, b: any) => sum + (Number(b.totalAmount || 0) - Number(b.paidAmount || 0)), 0);
      setTotalOutstanding(outstanding);
      setAmount(outstanding > 0 ? outstanding.toFixed(2) : '0.00');
    } catch (err: any) {
      console.error('Failed to fetch bills for supplier', err);
      setPendingBills([]);
      setTotalOutstanding(0);
    } finally {
      setLoadingBills(false);
    }
  };

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

  const handleExport = () => {
    exportToCsv(filteredPayments, 'Supplier_Payments_Report', [
      { key: 'paymentNumber', label: 'Payment #' },
      { key: 'supplierName', label: 'Supplier Name' },
      { key: 'paymentDate', label: 'Date' },
      { key: 'paymentMode', label: 'Payment Mode' },
      { key: 'amount', label: 'Amount (₹)' },
      { key: 'referenceNumber', label: 'Ref #' },
    ]);
  };

  const handleRecordPayment = async (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMessage(null);
    if (!selectedSupplierId) {
      setErrorMessage('Please select a supplier from the dropdown.');
      return;
    }
    const paymentAmount = Number(amount);
    if (!paymentAmount || paymentAmount <= 0) {
      setErrorMessage('Please provide a positive payment amount.');
      return;
    }
    if (totalOutstanding > 0 && paymentAmount > totalOutstanding) {
      setErrorMessage(`Payment amount (${formatCurrency(paymentAmount)}) cannot exceed total outstanding balance of ${formatCurrency(totalOutstanding)}.`);
      return;
    }

    setIsSubmitting(true);
    try {
      await api.post('/api/AccountsPayable/payments', {
        storeId: user?.storeId || '00000000-0000-0000-0000-000000000000',
        supplierId: selectedSupplierId,
        amount: paymentAmount,
        paymentDate,
        paymentMode,
        referenceNumber: referenceNumber.trim() || undefined,
        notes: notes.trim() || undefined,
        allocationMode: 'AUTO_FIFO'
      });
      queryClient.invalidateQueries({ queryKey: ['supplierPayments'] });
      setIsModalOpen(false);
      setSelectedSupplierId('');
      setAmount('');
      setReferenceNumber('');
      setNotes('');
      setPendingBills([]);
      setTotalOutstanding(0);
    } catch (err: any) {
      setErrorMessage(err.response?.data?.message || err.message || 'Failed to record supplier payment.');
    } finally {
      setIsSubmitting(false);
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
            Record Payment
          </button>
        </div>
      </div>

      {/* Record Payment Modal */}
      <Modal isOpen={isModalOpen} onClose={() => setIsModalOpen(false)} title="Record Supplier Payment">
        <form onSubmit={handleRecordPayment} className="space-y-4">
          {errorMessage && (
            <div className="p-3 bg-red-50 text-red-600 rounded-lg text-sm font-bold flex items-center gap-2">
              <AlertCircle className="w-4 h-4 shrink-0" />
              <span>{errorMessage}</span>
            </div>
          )}

          <div>
            <label className="block text-xs font-bold text-slate-500 uppercase mb-1">Select Supplier</label>
            <div className="relative">
              <Building2 className="absolute left-3 top-3 w-4 h-4 text-slate-400 pointer-events-none" />
              <select
                value={selectedSupplierId}
                onChange={(e) => handleSupplierChange(e.target.value)}
                required
                className="w-full pl-10 pr-4 py-2.5 bg-slate-50 border border-slate-200 rounded-lg outline-none focus:ring-2 focus:ring-rose-500 dark:bg-slate-800 dark:border-slate-700 dark:text-white text-sm"
              >
                <option value="">-- Choose Supplier --</option>
                {suppliers.map(s => (
                  <option key={s.id} value={s.id}>
                    {s.name} {s.code ? `(${s.code})` : ''}
                  </option>
                ))}
              </select>
            </div>
          </div>

          {selectedSupplierId && (
            <div className="bg-slate-50 dark:bg-slate-800/60 p-3 rounded-lg border border-slate-200 dark:border-slate-700 space-y-2">
              <div className="flex items-center justify-between">
                <span className="text-xs font-bold uppercase tracking-wider text-slate-500">Unpaid Bills</span>
                {loadingBills ? (
                  <span className="text-xs text-slate-400">Loading bills...</span>
                ) : (
                  <span className="text-xs font-bold text-rose-600 bg-rose-50 dark:bg-rose-950/40 px-2 py-0.5 rounded-full border border-rose-200 dark:border-rose-900">
                    Total Due: {formatCurrency(totalOutstanding)}
                  </span>
                )}
              </div>

              {pendingBills.length > 0 ? (
                <div className="max-h-36 overflow-y-auto divide-y divide-slate-200 dark:divide-slate-700 text-xs">
                  {pendingBills.map(b => {
                    const balance = Number(b.totalAmount || 0) - Number(b.paidAmount || 0);
                    return (
                      <div key={b.id} className="py-1.5 flex justify-between items-center text-slate-700 dark:text-slate-300">
                        <div>
                          <span className="font-bold text-indigo-600">{b.billNumber}</span>
                          <span className="text-slate-400 ml-2">({new Date(b.billDate).toLocaleDateString()})</span>
                        </div>
                        <div className="flex items-center gap-2">
                          <span className="font-semibold text-rose-600">{formatCurrency(balance)}</span>
                          <button
                            type="button"
                            onClick={() => setAmount(balance.toFixed(2))}
                            className="px-2 py-0.5 text-[10px] font-bold bg-indigo-50 text-indigo-600 hover:bg-indigo-100 rounded border border-indigo-200 cursor-pointer"
                            title="Auto-fill this bill amount"
                          >
                            Pay Bill
                          </button>
                        </div>
                      </div>
                    );
                  })}
                </div>
              ) : !loadingBills && (
                <p className="text-xs text-slate-400 italic">No pending bills for this supplier. Amount entered will be recorded as advance credit.</p>
              )}
            </div>
          )}

          <div className="grid grid-cols-2 gap-4">
            <div>
              <label className="block text-xs font-bold text-slate-500 uppercase mb-1">Payment Date</label>
              <input
                type="date"
                value={paymentDate}
                onChange={(e) => setPaymentDate(e.target.value)}
                required
                className="w-full px-4 py-2.5 bg-slate-50 border border-slate-200 rounded-lg outline-none focus:ring-2 focus:ring-rose-500 dark:bg-slate-800 dark:border-slate-700 dark:text-white"
              />
            </div>
            <div>
              <label className="block text-xs font-bold text-slate-500 uppercase mb-1">Payment Mode</label>
              <select
                value={paymentMode}
                onChange={(e) => setPaymentMode(e.target.value)}
                className="w-full px-4 py-2.5 bg-slate-50 border border-slate-200 rounded-lg outline-none focus:ring-2 focus:ring-rose-500 dark:bg-slate-800 dark:border-slate-700 dark:text-white"
              >
                <option value="BANK_TRANSFER">BANK TRANSFER (Cr Bank 10200)</option>
                <option value="CASH">CASH (Cr Cash 10100)</option>
                <option value="UPI">UPI (Cr Bank 10200)</option>
                <option value="CHEQUE">CHEQUE (Cr Bank 10200)</option>
              </select>
            </div>
          </div>

          <div>
            <div className="flex justify-between items-center mb-1">
              <label className="block text-xs font-bold text-slate-500 uppercase">Amount (₹)</label>
              {totalOutstanding > 0 && (
                <button
                  type="button"
                  onClick={() => setAmount(totalOutstanding.toFixed(2))}
                  className="text-xs text-indigo-600 dark:text-indigo-400 font-bold hover:underline cursor-pointer"
                >
                  Pay Full ({formatCurrency(totalOutstanding)})
                </button>
              )}
            </div>
            <input
              type="number"
              step="0.01"
              value={amount}
              onChange={(e) => setAmount(e.target.value)}
              required
              className="w-full px-4 py-2.5 bg-slate-50 border border-slate-200 rounded-lg outline-none focus:ring-2 focus:ring-rose-500 dark:bg-slate-800 dark:border-slate-700 dark:text-white font-bold text-base"
              placeholder="0.00"
            />
          </div>

          <div>
            <label className="block text-xs font-bold text-slate-500 uppercase mb-1">Reference Number</label>
            <div className="relative">
              <FileText className="absolute left-3 top-3 w-4 h-4 text-slate-400 pointer-events-none" />
              <input
                type="text"
                value={referenceNumber}
                onChange={(e) => setReferenceNumber(e.target.value)}
                className="w-full pl-10 pr-4 py-2.5 bg-slate-50 border border-slate-200 rounded-lg outline-none focus:ring-2 focus:ring-rose-500 dark:bg-slate-800 dark:border-slate-700 dark:text-white"
                placeholder="Transaction Ref / UTR / Cheque No"
              />
            </div>
          </div>

          <div>
            <label className="block text-xs font-bold text-slate-500 uppercase mb-1">Notes / Remarks</label>
            <input
              type="text"
              value={notes}
              onChange={(e) => setNotes(e.target.value)}
              className="w-full px-4 py-2 bg-slate-50 border border-slate-200 rounded-lg outline-none focus:ring-2 focus:ring-rose-500 dark:bg-slate-800 dark:border-slate-700 dark:text-white text-sm"
              placeholder="Optional payment notes"
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
              disabled={isSubmitting || !selectedSupplierId}
              className="px-5 py-2 text-sm font-bold text-white bg-rose-600 hover:bg-rose-700 disabled:opacity-50 rounded-lg shadow-md shadow-rose-600/30 transition flex items-center gap-2 cursor-pointer"
            >
              {isSubmitting ? 'Saving...' : 'Save Payment'}
            </button>
          </div>
        </form>
      </Modal>

      {/* Search Input */}
      <div className="bg-white dark:bg-slate-900 p-4 rounded-xl border border-slate-200 dark:border-slate-800 flex shadow-sm">
        <div className="relative w-full">
          <Search className="absolute left-3.5 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-400" />
          <input
            type="text"
            placeholder="Search by payment number, supplier name, or transaction ID..."
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
          Loading supplier payments...
        </div>
      ) : error ? (
        <div className="bg-rose-50 dark:bg-rose-950/20 text-rose-600 p-6 rounded-xl border border-rose-200 dark:border-rose-800">
          <h3 className="font-extrabold text-lg flex items-center gap-2">
            <AlertCircle className="w-5 h-5" />
            Error loading supplier payments
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
                      Supplier Name
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
                      Amount Paid
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
                        {pmt.paymentMode}
                      </span>
                    </td>
                    <td className="p-4 text-slate-500 font-mono text-xs">{pmt.referenceNumber || '-'}</td>
                    <td className="p-4 text-right font-black text-rose-600">
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
