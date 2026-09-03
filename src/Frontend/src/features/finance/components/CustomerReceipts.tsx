import React, { useState, useEffect } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { getCustomerReceipts, CustomerReceipt } from '../services/finance.service';
import { exportToCsv } from '../../../utils/exportToCsv';
import { Modal } from '../../../components/common/Modal';
import { api } from '../../../utils/api';
import { searchCustomers, CustomerDto } from '../../crm/api/crm.api';
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
  User,
  FileText,
  Check,
  Loader2
} from 'lucide-react';
import { formatCurrency } from '../../../utils/formatters';

export const CustomerReceipts: React.FC = () => {
  const queryClient = useQueryClient();
  const [search, setSearch] = useState('');
  const [sortBy, setSortBy] = useState<keyof CustomerReceipt>('receiptDate');
  const [sortOrder, setSortOrder] = useState<'asc' | 'desc'>('desc');
  const [page, setPage] = useState(1);
  const pageSize = 10;

  // Modal State
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [customerSearchQuery, setCustomerSearchQuery] = useState('');
  const [selectedCustomer, setSelectedCustomer] = useState<CustomerDto | null>(null);
  const [customerSuggestions, setCustomerSuggestions] = useState<CustomerDto[]>([]);
  const [isSearchingCustomers, setIsSearchingCustomers] = useState(false);
  const [isDropdownOpen, setIsDropdownOpen] = useState(false);
  const [amount, setAmount] = useState('');
  const [receiptDate, setReceiptDate] = useState(new Date().toISOString().split('T')[0]);
  const [paymentMode, setPaymentMode] = useState('UPI');
  const [referenceNumber, setReferenceNumber] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  // Debounced search for customers whenever modal is open and query changes
  useEffect(() => {
    if (!isModalOpen) {
      setIsDropdownOpen(false);
      return;
    }
    const timer = setTimeout(async () => {
      try {
        setIsSearchingCustomers(true);
        const results = await searchCustomers(customerSearchQuery);
        setCustomerSuggestions(results || []);
      } catch (err) {
        console.error('Failed to search customers', err);
      } finally {
        setIsSearchingCustomers(false);
      }
    }, 250);
    return () => clearTimeout(timer);
  }, [customerSearchQuery, isModalOpen]);

  const { data: receipts = [], isLoading, error } = useQuery({
    queryKey: ['customerReceipts'],
    queryFn: () => getCustomerReceipts()
  });

  const handleSort = (field: keyof CustomerReceipt) => {
    if (sortBy === field) {
      setSortOrder(sortOrder === 'asc' ? 'desc' : 'asc');
    } else {
      setSortBy(field);
      setSortOrder('desc');
    }
  };

  const handleExport = () => {
    exportToCsv(filteredReceipts, 'Customer_Receipts_Report', [
      { key: 'receiptNumber', label: 'Receipt #' },
      { key: 'customerName', label: 'Customer Name' },
      { key: 'receiptDate', label: 'Date' },
      { key: 'paymentMode', label: 'Payment Mode' },
      { key: 'amount', label: 'Amount (₹)' },
      { key: 'referenceNumber', label: 'Ref #' },
      { key: 'notes', label: 'Notes' },
    ]);
  };

  const handleSelectCustomer = (cust: CustomerDto) => {
    setSelectedCustomer(cust);
    setCustomerSearchQuery(cust.name);
    setIsDropdownOpen(false);
    setErrorMessage(null);
  };

  const handleRecordReceipt = async (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMessage(null);
    if (!selectedCustomer) {
      setErrorMessage('Please select a valid customer from the dropdown suggestions.');
      return;
    }
    if (!amount || Number(amount) <= 0) {
      setErrorMessage('Please provide a positive receipt amount.');
      return;
    }

    setIsSubmitting(true);
    try {
      await api.post('/api/AccountsReceivable/receipts', {
        storeId: '00000000-0000-0000-0000-000000000000',
        customerId: selectedCustomer.id,
        customerName: selectedCustomer.name,
        amount: Number(amount),
        receiptDate,
        paymentMode,
        referenceNumber: referenceNumber.trim() || undefined
      });
      queryClient.invalidateQueries({ queryKey: ['customerReceipts'] });
      setIsModalOpen(false);
      setSelectedCustomer(null);
      setCustomerSearchQuery('');
      setAmount('');
      setReferenceNumber('');
    } catch (err: any) {
      setErrorMessage(err.response?.data?.message || err.message || 'Failed to record customer receipt.');
    } finally {
      setIsSubmitting(false);
    }
  };

  // Filter receipts
  const filteredReceipts = receipts.filter(rcpt => {
    return (
      rcpt.receiptNumber.toLowerCase().includes(search.toLowerCase()) ||
      rcpt.customerName.toLowerCase().includes(search.toLowerCase()) ||
      (rcpt.referenceNumber && rcpt.referenceNumber.toLowerCase().includes(search.toLowerCase()))
    );
  });

  // Sort receipts
  const sortedReceipts = [...filteredReceipts].sort((a, b) => {
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

  // Paginate receipts
  const totalItems = sortedReceipts.length;
  const totalPages = Math.ceil(totalItems / pageSize);
  const paginatedReceipts = sortedReceipts.slice((page - 1) * pageSize, page * pageSize);

  const getModeBadgeClass = (mode: string) => {
    switch (mode.toUpperCase()) {
      case 'CASH':
        return 'bg-emerald-100 text-emerald-800 dark:bg-emerald-950/40 dark:text-emerald-400 border-emerald-200 dark:border-emerald-800/40';
      case 'UPI':
        return 'bg-violet-100 text-violet-800 dark:bg-violet-950/40 dark:text-violet-400 border-violet-200 dark:border-violet-800/40';
      case 'CARD':
        return 'bg-blue-100 text-blue-800 dark:bg-blue-950/40 dark:text-blue-400 border-blue-200 dark:border-blue-800/40';
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
            <Banknote className="w-7 h-7 text-blue-600" />
            Customer Receipts (AR)
          </h2>
          <p className="text-slate-500 dark:text-slate-400 mt-1">Record collections from B2B customers</p>
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
            className="bg-blue-600 hover:bg-blue-700 text-white px-4 py-2.5 rounded-lg font-bold flex items-center gap-2 shadow-lg shadow-blue-600/30 transition-all text-sm cursor-pointer"
          >
            <Plus className="w-4 h-4" />
            Record Receipt
          </button>
        </div>
      </div>

      <Modal isOpen={isModalOpen} onClose={() => setIsModalOpen(false)} title="Record New Receipt">
        <form onSubmit={handleRecordReceipt} className="space-y-4">
          {errorMessage && <div className="text-sm text-rose-600 bg-rose-50 p-3 rounded-lg">{errorMessage}</div>}
          <div className="relative">
            <label className="block text-sm font-bold text-slate-700 dark:text-slate-300 mb-1">
              Customer <span className="text-rose-500">*</span>
            </label>
            <div className="relative">
              <User className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-400" />
              <input
                type="text"
                required
                value={customerSearchQuery}
                onFocus={() => setIsDropdownOpen(true)}
                onChange={(e) => {
                  setCustomerSearchQuery(e.target.value);
                  if (selectedCustomer && e.target.value !== selectedCustomer.name) {
                    setSelectedCustomer(null);
                  }
                  setIsDropdownOpen(true);
                }}
                className="w-full pl-10 pr-24 py-2 bg-slate-50 border border-slate-200 rounded-lg text-slate-800 dark:text-white dark:bg-slate-800 dark:border-slate-700 focus:ring-2 focus:ring-blue-500 outline-none"
                placeholder="Type customer name or phone..."
              />
              {isSearchingCustomers ? (
                <Loader2 className="absolute right-3 top-1/2 -translate-y-1/2 w-4 h-4 text-blue-500 animate-spin" />
              ) : selectedCustomer ? (
                <span className="absolute right-2.5 top-1/2 -translate-y-1/2 inline-flex items-center gap-1 text-[11px] bg-emerald-100 text-emerald-800 dark:bg-emerald-950/60 dark:text-emerald-300 px-2 py-0.5 rounded-full font-semibold">
                  <Check className="w-3 h-3 text-emerald-600 dark:text-emerald-400" /> Selected
                </span>
              ) : null}
            </div>

            {/* Autocomplete Dropdown Menu */}
            {isDropdownOpen && (
              <div className="absolute z-50 left-0 right-0 mt-1 bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-lg shadow-xl max-h-52 overflow-y-auto">
                {isSearchingCustomers && customerSuggestions.length === 0 ? (
                  <div className="p-3 text-xs text-slate-400 text-center flex items-center justify-center gap-2">
                    <Loader2 className="w-3.5 h-3.5 animate-spin" /> Searching customers...
                  </div>
                ) : customerSuggestions.length > 0 ? (
                  customerSuggestions.map((cust) => (
                    <button
                      type="button"
                      key={cust.id}
                      data-testid="customer-option"
                      onClick={() => handleSelectCustomer(cust)}
                      className="w-full text-left px-3 py-2 hover:bg-blue-50 dark:hover:bg-slate-700 cursor-pointer border-b border-slate-100 dark:border-slate-700/50 last:border-b-0 flex items-center justify-between transition-colors"
                    >
                      <div>
                        <div className="text-sm font-semibold text-slate-800 dark:text-white">{cust.name}</div>
                        <div className="text-xs text-slate-400">Phone: {cust.phone || 'N/A'}</div>
                      </div>
                      {cust.tierName && (
                        <span className="text-[11px] px-2 py-0.5 rounded bg-blue-50 dark:bg-blue-950/50 text-blue-700 dark:text-blue-300 font-medium">
                          {cust.tierName}
                        </span>
                      )}
                    </button>
                  ))
                ) : (
                  <div className="p-3 text-xs text-slate-400 text-center">
                    {customerSearchQuery ? 'No matching customers found' : 'Type to search customers'}
                  </div>
                )}
              </div>
            )}
          </div>
          <div className="grid grid-cols-2 gap-4">
            <div>
              <label className="block text-sm font-bold text-slate-700 dark:text-slate-300 mb-1">Amount</label>
              <input type="number" required value={amount} onChange={(e) => setAmount(e.target.value)} className="w-full px-4 py-2 bg-slate-50 border border-slate-200 rounded-lg text-slate-800 dark:text-white dark:bg-slate-800 dark:border-slate-700" placeholder="0.00" />
            </div>
            <div>
              <label className="block text-sm font-bold text-slate-700 dark:text-slate-300 mb-1">Date</label>
              <input type="date" required value={receiptDate} onChange={(e) => setReceiptDate(e.target.value)} className="w-full px-4 py-2 bg-slate-50 border border-slate-200 rounded-lg text-slate-800 dark:text-white dark:bg-slate-800 dark:border-slate-700" />
            </div>
          </div>
          <div>
            <label className="block text-sm font-bold text-slate-700 dark:text-slate-300 mb-1">Payment Mode</label>
            <select value={paymentMode} onChange={(e) => setPaymentMode(e.target.value)} className="w-full px-4 py-2 bg-slate-50 border border-slate-200 rounded-lg text-slate-800 dark:text-white dark:bg-slate-800 dark:border-slate-700">
              <option value="UPI">UPI</option>
              <option value="CASH">CASH</option>
              <option value="CARD">CARD</option>
              <option value="BANK">BANK</option>
            </select>
          </div>
          <div>
            <label className="block text-sm font-bold text-slate-700 dark:text-slate-300 mb-1">Reference Number</label>
            <div className="relative">
              <FileText className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-400" />
              <input type="text" value={referenceNumber} onChange={(e) => setReferenceNumber(e.target.value)} className="w-full pl-10 pr-4 py-2 bg-slate-50 border border-slate-200 rounded-lg text-slate-800 dark:text-white dark:bg-slate-800 dark:border-slate-700" placeholder="Transaction ID..." />
            </div>
          </div>
          <button type="submit" disabled={isSubmitting} className="w-full bg-blue-600 text-white font-bold py-2.5 rounded-lg hover:bg-blue-700 transition-all cursor-pointer">
            {isSubmitting ? 'Recording...' : 'Save Receipt'}
          </button>
        </form>
      </Modal>

      {/* Search Input */}
      <div className="bg-white dark:bg-slate-900 p-4 rounded-xl border border-slate-200 dark:border-slate-800 flex shadow-sm">
        <div className="relative w-full">
          <Search className="absolute left-3.5 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-400" />
          <input
            type="text"
            placeholder="Search by receipt number, customer name, or transaction ID..."
            value={search}
            onChange={(e) => { setSearch(e.target.value); setPage(1); }}
            className="w-full pl-10 pr-4 py-2.5 bg-slate-50 dark:bg-slate-950 border border-slate-200 dark:border-slate-800 rounded-lg text-sm text-slate-800 dark:text-white placeholder-slate-400 focus:bg-white dark:focus:bg-slate-900 focus:ring-2 focus:ring-blue-500 outline-none transition-all"
          />
        </div>
      </div>

      {/* Main Table */}
      {isLoading ? (
        <div className="bg-white dark:bg-slate-900 rounded-xl border border-slate-200 dark:border-slate-800 p-12 text-center text-slate-400 font-bold">
          <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-blue-600 mx-auto mb-4"></div>
          Loading receipts...
        </div>
      ) : error ? (
        <div className="bg-rose-50 dark:bg-rose-950/20 text-rose-600 p-6 rounded-xl border border-rose-200 dark:border-rose-800">
          <h3 className="font-extrabold text-lg flex items-center gap-2">
            <AlertCircle className="w-5 h-5" />
            Error loading customer receipts
          </h3>
          <p className="text-sm mt-1">{(error as any)?.message}</p>
        </div>
      ) : paginatedReceipts.length === 0 ? (
        <div className="bg-white dark:bg-slate-900 rounded-xl border border-slate-200 dark:border-slate-800 p-12 text-center text-slate-400">
          No customer receipts found.
        </div>
      ) : (
        <div className="bg-white dark:bg-slate-900 rounded-xl border border-slate-200 dark:border-slate-800 shadow-sm overflow-hidden">
          <div className="overflow-x-auto">
            <table className="w-full text-left border-collapse text-sm">
              <thead>
                <tr className="border-b border-slate-200 dark:border-slate-800 bg-slate-50/50 dark:bg-slate-900/50 text-slate-500 dark:text-slate-400 font-bold">
                  <th className="p-4 cursor-pointer hover:bg-slate-100 dark:hover:bg-slate-800" onClick={() => handleSort('receiptNumber')}>
                    <span className="flex items-center gap-1.5">
                      Receipt ID
                      <ArrowUpDown className="w-3.5 h-3.5" />
                    </span>
                  </th>
                  <th className="p-4 cursor-pointer hover:bg-slate-100 dark:hover:bg-slate-800" onClick={() => handleSort('customerName')}>
                    <span className="flex items-center gap-1.5">
                      Customer
                      <ArrowUpDown className="w-3.5 h-3.5" />
                    </span>
                  </th>
                  <th className="p-4 cursor-pointer hover:bg-slate-100 dark:hover:bg-slate-800" onClick={() => handleSort('receiptDate')}>
                    <span className="flex items-center gap-1.5">
                      Receipt Date
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
                      Amount Received
                      <ArrowUpDown className="w-3.5 h-3.5" />
                    </span>
                  </th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-150 dark:divide-slate-800/80">
                {paginatedReceipts.map(rcpt => (
                  <tr key={rcpt.id} className="hover:bg-slate-50 dark:hover:bg-slate-800/30 transition-colors">
                    <td className="p-4 font-bold text-slate-800 dark:text-slate-100">{rcpt.receiptNumber}</td>
                    <td className="p-4 font-bold text-slate-700 dark:text-slate-300">{rcpt.customerName}</td>
                    <td className="p-4 text-slate-600 dark:text-slate-400">
                      <span className="flex items-center gap-2">
                        <Calendar className="w-4 h-4 text-slate-400" />
                        {new Date(rcpt.receiptDate).toLocaleDateString()}
                      </span>
                    </td>
                    <td className="p-4 text-center">
                      <span className={`inline-block px-2.5 py-1 rounded-full text-xs font-bold uppercase tracking-wider border ${getModeBadgeClass(rcpt.paymentMode)}`}>
                        {rcpt.paymentMode}
                      </span>
                    </td>
                    <td className="p-4 text-slate-500 font-mono text-xs">{rcpt.referenceNumber || '-'}</td>
                    <td className="p-4 text-right font-black text-emerald-600">
                      {formatCurrency(rcpt.amount)}
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
