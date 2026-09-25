import React, { useState, useEffect } from 'react';
import { 
  X, Search, RotateCcw, CreditCard, Award, MessageSquare, 
  AlertCircle, RefreshCw, Calendar, ArrowLeft, CheckCircle2, User, Phone, Receipt, Printer
} from 'lucide-react';
import { api } from '@/utils/api';
import { printSalesReturnReceipt } from '../../utils/printReceipt';

// Strongly typed local interfaces for POS invoice structure
interface InvoiceItem {
  id: string; // unique GUID for the line item
  productId: string;
  barcode?: string;
  productName: string;
  quantity: number;
  unitPrice: number;
  discountAmount: number;
  cgstRate: number;
  cgstAmount: number;
  sgstRate: number;
  sgstAmount: number;
  cessRate: number;
  cessAmount: number;
  totalAmount: number;
}

interface Invoice {
  id: string;
  storeId: string;
  businessDate: string;
  invoiceNumber: string;
  terminalId: string;
  terminalCode: string;
  cashierId: string;
  cashierName: string;
  customerId?: string;
  customerName?: string;
  customerPhone?: string;
  subTotal: number;
  discountAmount: number;
  taxAmount: number;
  totalAmount: number;
  roundOff: number;
  netPayable: number;
  status: string;
  paymentMode: string;
  cashAmount: number;
  upiAmount: number;
  cardAmount: number;
  walletAmount: number;
  createdAt: string;
  items: InvoiceItem[];
}

interface ReturnItemPayload {
  productId: string;
  batchId: string | null;
  quantity: number;
  itemId: string;
}

interface SalesReturnModalProps {
  isOpen: boolean;
  onClose: () => void;
  user?: {
    role: string;
    fullName: string;
  };
  requestManagerOverride: (action: string, callback: (pin?: string) => void) => void;
}

export const SalesReturnModal = ({ isOpen, onClose, user, requestManagerOverride }: SalesReturnModalProps) => {
  const [invoiceNumber, setInvoiceNumber] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [invoice, setInvoice] = useState<Invoice | null>(null);
  const [returnItems, setReturnItems] = useState<Record<string, number>>({}); // keyed by InvoiceItem.id
  const [refundMode, setRefundMode] = useState<'CASH' | 'UPI' | 'CREDIT_NOTE'>('CASH');
  const [notes, setNotes] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [successMessage, setSuccessMessage] = useState('');
  const [lastCompletedReturn, setLastCompletedReturn] = useState<any | null>(null);
  
  // Recent invoices state
  const [recentInvoices, setRecentInvoices] = useState<Invoice[]>([]);
  const [recentLoading, setRecentLoading] = useState(false);
  const [viewMode, setViewMode] = useState<'today' | 'all'>('today');
  const [searchResults, setSearchResults] = useState<Invoice[]>([]);

  // Fetch recent completed invoices on modal open or viewMode change
  const fetchRecentInvoices = async (mode: 'today' | 'all' = viewMode) => {
    if (!isOpen) return;
    setRecentLoading(true);
    try {
      const todayParam = mode === 'today' ? '&todayOnly=true' : '&todayOnly=false';
      const res = await api.get<Invoice[]>(`/api/pos/invoices/recent-for-return?limit=50${todayParam}`);
      if (res.data) {
        setRecentInvoices(res.data);
      }
    } catch (err) {
      console.error('Failed to load recent invoices:', err);
    } finally {
      setRecentLoading(false);
    }
  };

  useEffect(() => {
    if (isOpen) {
      setError('');
      setSuccessMessage('');
      fetchRecentInvoices(viewMode);
    } else {
      // Reset state on close
      setInvoice(null);
      setInvoiceNumber('');
      setReturnItems({});
      setSearchResults([]);
    }
  }, [isOpen, viewMode]);

  if (!isOpen) return null;

  // Mask phone helper to protect customer privacy on POS floor (displays only last 4 digits in lists)
  const maskPhone = (phone?: string) => {
    if (!phone) return '';
    const trimmed = phone.trim();
    if (trimmed.length <= 4) return trimmed;
    const last4 = trimmed.slice(-4);
    return `••••••${last4}`;
  };

  const handleSelectInvoice = (inv: Invoice) => {
    setInvoice(inv);
    setSearchResults([]);
    const initialQtys: Record<string, number> = {};
    (inv.items || []).forEach((item: InvoiceItem) => {
      initialQtys[item.id] = 0;
    });
    setReturnItems(initialQtys);
    setInvoiceNumber(inv.invoiceNumber);
    setError('');
  };

  const handleSearch = async (e?: React.FormEvent) => {
    if (e) e.preventDefault();
    if (!invoiceNumber.trim()) {
      setSearchResults([]);
      return;
    }

    setLoading(true);
    setError('');
    setInvoice(null);
    setReturnItems({});
    setSuccessMessage('');
    setSearchResults([]);

    try {
      const res = await api.get<Invoice[]>(`/api/pos/invoice/search?query=${encodeURIComponent(invoiceNumber.trim())}&limit=50`);
      if (res.data && res.data.length > 0) {
        setSearchResults(res.data);
        if (res.data.length === 1) {
          handleSelectInvoice(res.data[0]);
        }
      } else {
        setError(`No matching invoices found for "${invoiceNumber.trim()}".`);
      }
    } catch (err: any) {
      console.error(err);
      setError('Invoice search failed. Please verify connection and try again.');
    } finally {
      setLoading(false);
    }
  };

  const handleQtyChange = (itemId: string, val: number, maxQty: number) => {
    // Math.max and Math.min support fractional decimal quantities (e.g. 0.5 kg)
    const cleanVal = Math.max(0, Math.min(maxQty, val));
    setReturnItems(prev => ({
      ...prev,
      [itemId]: cleanVal
    }));
  };

  const handleSetAllMax = () => {
    if (!invoice) return;
    const maxQtys: Record<string, number> = {};
    invoice.items.forEach(item => {
      maxQtys[item.id] = item.quantity;
    });
    setReturnItems(maxQtys);
  };

  const handleClearAllQtys = () => {
    if (!invoice) return;
    const zeroQtys: Record<string, number> = {};
    invoice.items.forEach(item => {
      zeroQtys[item.id] = 0;
    });
    setReturnItems(zeroQtys);
  };

  const calculateTotalRefund = () => {
    if (!invoice) return 0;
    return invoice.items.reduce((sum: number, item: InvoiceItem) => {
      const qty = returnItems[item.id] || 0;
      return sum + (qty * item.unitPrice);
    }, 0);
  };

  const totalRefund = calculateTotalRefund();

  const handleProcessReturn = () => {
    if (!invoice) return;

    const itemsToReturn = Object.entries(returnItems)
      .filter(([_, qty]) => qty > 0)
      .map(([itemId, qty]) => {
        const item = invoice.items.find((i: InvoiceItem) => i.id === itemId);
        if (!item) throw new Error('Invoice item reference mismatch.');
        return {
          productId: item.productId,
          batchId: null, // Backend FIFO resolves batch automatically
          quantity: qty,
          itemId: item.id // unique GUID mapping to specific invoice item line
        } as ReturnItemPayload;
      });

    if (itemsToReturn.length === 0) {
      alert('Please select at least one item and quantity to return.');
      return;
    }

    const executeSubmit = async (pin?: string) => {
      setSubmitting(true);
      setError('');
      try {
        const payload = {
          storeId: invoice.storeId || '00000000-0000-0000-0000-000000000000',
          invoiceId: invoice.id,
          returnDate: new Date().toISOString(),
          refundMode,
          items: itemsToReturn,
          managerOverridePin: pin || null
        };

        const res = await api.post('/api/AccountsReceivable/returns', payload);
        const returnId = res.data?.id;

        // Automatically fetch full return payload and trigger thermal receipt print
        if (returnId) {
          try {
            const detailRes = await api.get(`/api/AccountsReceivable/returns/${returnId}`);
            if (detailRes.data) {
              setLastCompletedReturn(detailRes.data);
              await printSalesReturnReceipt(detailRes.data);
            }
          } catch (printErr) {
            console.error('Auto receipt print failed:', printErr);
          }
        }
        
        // Show friendly cashier-readable success message
        setSuccessMessage(`Refund processed successfully for ${invoice.invoiceNumber}. Return transaction completed.`);
        setInvoice(null);
        setInvoiceNumber('');
        setReturnItems({});
        setNotes('');
        // Refresh recent list to reflect any changes
        fetchRecentInvoices(viewMode);
      } catch (err: any) {
        console.error(err);
        let userFriendlyError = 'An unexpected server error occurred during the return.';
        if (err.response?.data) {
          userFriendlyError = typeof err.response.data === 'string' 
            ? err.response.data 
            : (err.response.data.message || 'Refund request was rejected by the server.');
        }
        setError(`Return failed: ${userFriendlyError}`);
      } finally {
        setSubmitting(false);
      }
    };

    // If caller role is Cashier, prompt for Manager PIN override first
    if (user?.role === 'Cashier') {
      requestManagerOverride('Sales Return', (pin?: string) => {
        executeSubmit(pin);
      });
    } else {
      executeSubmit();
    }
  };

  // Filter recent invoices locally if user has typed something before submitting search
  const filteredRecentInvoices = recentInvoices.filter(inv => {
    if (!invoiceNumber.trim()) return true;
    const q = invoiceNumber.trim().toLowerCase();
    return (
      inv.invoiceNumber.toLowerCase().includes(q) ||
      (inv.customerPhone && inv.customerPhone.includes(q)) ||
      (inv.customerName && inv.customerName.toLowerCase().includes(q)) ||
      (inv.cashierName && inv.cashierName.toLowerCase().includes(q))
    );
  });

  return (
    <div className="fixed inset-0 bg-black/75 z-modal flex items-center justify-center p-4 backdrop-blur-md">
      <div className="bg-slate-900 border border-slate-800 rounded-2xl shadow-2xl w-full max-w-4xl overflow-hidden flex flex-col text-slate-100 max-h-[92vh]">
        {/* Header */}
        <div className="bg-slate-950 p-5 border-b border-slate-800 flex justify-between items-center">
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 rounded-xl bg-indigo-500/10 border border-indigo-500/20 flex items-center justify-center text-indigo-400">
              <RotateCcw className="w-5 h-5 animate-pulse" />
            </div>
            <div>
              <h2 className="text-xl font-bold text-white tracking-tight">Process Sales Return</h2>
              <p className="text-xs text-slate-400">Select a recent completed invoice or search by invoice #, phone, or barcode</p>
            </div>
          </div>
          <button 
            onClick={onClose} 
            className="text-slate-400 hover:text-white transition-colors p-2 rounded-xl hover:bg-slate-800"
            disabled={submitting}
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Content */}
        <div className="p-6 overflow-y-auto space-y-5 flex-1">
          {successMessage && (
            <div className="bg-emerald-950/80 border border-emerald-500/30 p-4 rounded-xl text-emerald-400 flex items-center justify-between gap-3 shadow-lg shadow-emerald-950/50">
              <div className="flex items-center gap-3">
                <Award className="w-6 h-6 shrink-0 text-emerald-400" />
                <div>
                  <p className="font-bold text-sm">Transaction Complete</p>
                  <p className="text-xs opacity-90">{successMessage}</p>
                </div>
              </div>
              {lastCompletedReturn && (
                <button
                  type="button"
                  onClick={() => printSalesReturnReceipt(lastCompletedReturn)}
                  className="bg-emerald-600 hover:bg-emerald-500 text-white font-bold px-3 py-1.5 rounded-lg text-xs flex items-center gap-1.5 shadow transition-all active:scale-95 shrink-0 cursor-pointer"
                  title="Reprint Sales Return Receipt"
                >
                  <Printer className="w-4 h-4" /> Reprint Receipt
                </button>
              )}
            </div>
          )}

          {error && (
            <div className="bg-rose-950/85 border border-rose-500/30 p-4 rounded-xl text-rose-400 flex items-center gap-3 shadow-lg shadow-rose-950/50">
              <AlertCircle className="w-6 h-6 shrink-0 text-rose-400" />
              <p className="text-sm font-semibold">{error}</p>
            </div>
          )}

          {/* Search Bar & View Mode Controls */}
          {!invoice && (
            <div className="space-y-3">
              <form onSubmit={handleSearch} className="flex gap-2">
                <div className="relative flex-1">
                  <Search className="absolute left-3.5 top-1/2 -translate-y-1/2 text-slate-500 w-4 h-4" />
                  <input
                    type="text"
                    placeholder="Search by Invoice # (e.g. INV-...), Phone, Customer name, or Barcode..."
                    value={invoiceNumber}
                    onChange={(e) => setInvoiceNumber(e.target.value)}
                    className="w-full bg-slate-950 border border-slate-800 rounded-xl py-2.5 pl-10 pr-4 text-white font-medium outline-none focus:ring-2 focus:ring-indigo-500 transition-all text-sm placeholder:text-slate-600"
                    disabled={loading || submitting}
                  />
                  {invoiceNumber && (
                    <button
                      type="button"
                      onClick={() => { setInvoiceNumber(''); setSearchResults([]); }}
                      className="absolute right-3 top-1/2 -translate-y-1/2 text-slate-500 hover:text-slate-300 text-xs"
                    >
                      Clear
                    </button>
                  )}
                </div>
                <button
                  type="submit"
                  disabled={loading || submitting || !invoiceNumber.trim()}
                  className="bg-indigo-600 hover:bg-indigo-500 disabled:bg-slate-800 disabled:text-slate-500 text-white font-bold px-5 rounded-xl transition-all active:scale-95 flex items-center justify-center gap-2 text-sm shadow-md shadow-indigo-600/10 shrink-0"
                >
                  {loading ? 'Searching...' : 'Search'}
                </button>
              </form>

              {/* Tabs for Recent Invoices: Today vs All */}
              <div className="flex items-center justify-between pt-1">
                <div className="flex items-center gap-2 bg-slate-950 p-1 rounded-xl border border-slate-800">
                  <button
                    type="button"
                    onClick={() => setViewMode('today')}
                    className={`px-3.5 py-1.5 rounded-lg text-xs font-bold transition-all flex items-center gap-1.5 ${
                      viewMode === 'today'
                        ? 'bg-indigo-600 text-white shadow'
                        : 'text-slate-400 hover:text-white'
                    }`}
                  >
                    <Calendar className="w-3.5 h-3.5" />
                    Today's Invoices
                  </button>
                  <button
                    type="button"
                    onClick={() => setViewMode('all')}
                    className={`px-3.5 py-1.5 rounded-lg text-xs font-bold transition-all flex items-center gap-1.5 ${
                      viewMode === 'all'
                        ? 'bg-indigo-600 text-white shadow'
                        : 'text-slate-400 hover:text-white'
                    }`}
                  >
                    <Receipt className="w-3.5 h-3.5" />
                    All Recent Invoices (50)
                  </button>
                </div>

                <button
                  type="button"
                  onClick={() => fetchRecentInvoices(viewMode)}
                  disabled={recentLoading}
                  className="text-slate-400 hover:text-indigo-400 transition-colors p-1.5 rounded-lg hover:bg-slate-800 text-xs flex items-center gap-1"
                  title="Refresh list"
                >
                  <RefreshCw className={`w-3.5 h-3.5 ${recentLoading ? 'animate-spin text-indigo-400' : ''}`} />
                  <span className="hidden sm:inline">Refresh</span>
                </button>
              </div>
            </div>
          )}

          {/* Search Results (When explicit search was performed with > 1 result) */}
          {searchResults.length > 1 && !invoice && (
            <div className="bg-slate-950 border border-indigo-500/30 rounded-xl p-4 space-y-2">
              <div className="flex items-center justify-between mb-2">
                <p className="text-xs font-bold text-indigo-400 uppercase tracking-wider flex items-center gap-1.5">
                  <Search className="w-3.5 h-3.5" /> Search Results ({searchResults.length} matches):
                </p>
                <button
                  type="button"
                  onClick={() => setSearchResults([])}
                  className="text-slate-500 hover:text-slate-300 text-xs"
                >
                  Dismiss Search
                </button>
              </div>
              <div className="divide-y divide-slate-850 max-h-56 overflow-y-auto">
                {searchResults.map((inv: Invoice) => (
                  <button
                    key={inv.id}
                    type="button"
                    onClick={() => handleSelectInvoice(inv)}
                    className="w-full text-left py-2.5 px-3 hover:bg-slate-900 rounded-lg transition-colors flex justify-between items-center text-sm group"
                  >
                    <div>
                      <div className="flex items-center gap-2">
                        <span className="font-mono font-bold text-white group-hover:text-indigo-300">{inv.invoiceNumber}</span>
                        <span className="text-slate-500 text-xs font-sans">
                          {new Date(inv.createdAt || inv.businessDate).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}
                        </span>
                      </div>
                      <span className="text-slate-400 text-xs block">
                        Cashier: {inv.cashierName || 'Cashier'} • Terminal: {inv.terminalCode || 'POS-01'}
                      </span>
                    </div>
                    <div className="text-right">
                      <span className="block text-slate-300 text-xs font-semibold">
                        {inv.customerName || 'Walk-in Customer'}
                        {inv.customerPhone && <span className="font-mono text-slate-500 ml-1">({maskPhone(inv.customerPhone)})</span>}
                      </span>
                      <span className="text-sm text-indigo-400 font-black">₹{inv.netPayable.toFixed(2)}</span>
                    </div>
                  </button>
                ))}
              </div>
            </div>
          )}

          {/* Recent Invoices Dropdown / Card Selector (When no invoice selected) */}
          {!invoice && searchResults.length <= 1 && (
            <div className="bg-slate-950/80 border border-slate-800 rounded-xl p-4 space-y-3">
              <div className="flex items-center justify-between">
                <p className="text-xs font-bold text-slate-400 uppercase tracking-wider flex items-center gap-1.5">
                  <Receipt className="w-3.5 h-3.5 text-indigo-400" />
                  {viewMode === 'today' ? "Today's Invoices" : "Recent Completed Invoices"} 
                  <span className="text-slate-500 font-normal">
                    ({filteredRecentInvoices.length} {filteredRecentInvoices.length === 1 ? 'invoice' : 'invoices'})
                  </span>
                </p>
                <span className="text-[11px] text-slate-500">Click any row to select for return</span>
              </div>

              {recentLoading ? (
                <div className="py-12 flex flex-col items-center justify-center gap-3 text-slate-500 text-sm">
                  <RefreshCw className="w-6 h-6 animate-spin text-indigo-400" />
                  <span>Loading recent completed invoices...</span>
                </div>
              ) : filteredRecentInvoices.length === 0 ? (
                <div className="py-12 text-center text-slate-500 text-sm space-y-2">
                  <Receipt className="w-8 h-8 mx-auto text-slate-600 stroke-[1.5]" />
                  <p className="font-semibold text-slate-400">
                    {invoiceNumber ? `No recent invoices matching "${invoiceNumber}".` : "No completed invoices found for this selection."}
                  </p>
                  <p className="text-xs text-slate-500 max-w-sm mx-auto">
                    {viewMode === 'today' 
                      ? 'No sales recorded for today yet. Switch to "All Recent Invoices" or search by invoice number above.' 
                      : 'You can search by typing the invoice number or scanning an item barcode.'}
                  </p>
                  {viewMode === 'today' && (
                    <button
                      type="button"
                      onClick={() => setViewMode('all')}
                      className="mt-2 text-xs font-bold text-indigo-400 hover:text-indigo-300 underline"
                    >
                      Switch to All Recent Invoices →
                    </button>
                  )}
                </div>
              ) : (
                <div className="divide-y divide-slate-850/80 max-h-80 overflow-y-auto pr-1">
                  {filteredRecentInvoices.map((inv: Invoice) => (
                    <div
                      key={inv.id}
                      onClick={() => handleSelectInvoice(inv)}
                      className="py-3 px-3 hover:bg-slate-900/90 rounded-xl transition-all cursor-pointer flex justify-between items-center group border border-transparent hover:border-indigo-500/20 my-0.5"
                    >
                      <div className="space-y-1">
                        <div className="flex items-center gap-2">
                          <span className="font-mono font-bold text-sm text-slate-100 group-hover:text-indigo-300 transition-colors">
                            {inv.invoiceNumber}
                          </span>
                          <span className="text-[11px] font-medium bg-slate-800 text-slate-400 px-2 py-0.5 rounded">
                            {new Date(inv.createdAt || inv.businessDate).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}
                          </span>
                          <span className="text-[11px] font-semibold text-emerald-400 bg-emerald-950/60 border border-emerald-500/20 px-1.5 py-0.2 rounded">
                            {inv.status}
                          </span>
                        </div>
                        <div className="flex items-center gap-4 text-xs text-slate-400">
                          <span className="flex items-center gap-1">
                            <User className="w-3 h-3 text-slate-500" />
                            {inv.customerName || 'Walk-in Customer'}
                            {inv.customerPhone && <span className="font-mono text-slate-500">({maskPhone(inv.customerPhone)})</span>}
                          </span>
                          <span className="text-slate-600">•</span>
                          <span>Cashier: {inv.cashierName || 'Cashier'}</span>
                          <span className="text-slate-600">•</span>
                          <span>{inv.items?.length || 0} items</span>
                        </div>
                      </div>

                      <div className="text-right flex items-center gap-3">
                        <div>
                          <span className="text-base font-black text-indigo-400 block">₹{inv.netPayable.toFixed(2)}</span>
                          <span className="text-[11px] text-slate-500 uppercase font-semibold">{inv.paymentMode}</span>
                        </div>
                        <span className="text-xs font-bold text-indigo-400 group-hover:translate-x-1 transition-transform opacity-70 group-hover:opacity-100 hidden sm:inline">
                          Select →
                        </span>
                      </div>
                    </div>
                  ))}
                </div>
              )}
            </div>
          )}

          {/* Invoice Details and Items table (When an invoice is selected) */}
          {invoice && (
            <div className="space-y-5 animate-in fade-in duration-200">
              {/* Back to recent list banner */}
              <div className="flex items-center justify-between bg-slate-950 p-3 rounded-xl border border-slate-800">
                <button
                  type="button"
                  onClick={() => {
                    setInvoice(null);
                    setReturnItems({});
                    setError('');
                  }}
                  className="flex items-center gap-2 text-xs font-bold text-indigo-400 hover:text-indigo-300 bg-indigo-950/40 hover:bg-indigo-900/50 border border-indigo-800/40 px-3 py-1.5 rounded-lg transition-all active:scale-95"
                >
                  <ArrowLeft className="w-3.5 h-3.5" /> Select Different Invoice
                </button>
                <div className="flex items-center gap-2">
                  <button
                    type="button"
                    onClick={handleSetAllMax}
                    className="text-xs font-semibold text-slate-400 hover:text-white px-2.5 py-1 rounded bg-slate-900 border border-slate-800 hover:bg-slate-800 transition-colors"
                  >
                    Select All Max
                  </button>
                  <button
                    type="button"
                    onClick={handleClearAllQtys}
                    className="text-xs font-semibold text-slate-400 hover:text-white px-2.5 py-1 rounded bg-slate-900 border border-slate-800 hover:bg-slate-800 transition-colors"
                  >
                    Reset Qtys
                  </button>
                </div>
              </div>

              {/* Invoice Metadata */}
              <div className="grid grid-cols-2 sm:grid-cols-4 gap-4 bg-slate-950/60 p-4 rounded-xl border border-slate-800 text-sm">
                <div>
                  <span className="block text-slate-500 text-xs font-bold uppercase tracking-wider mb-1">Invoice Number</span>
                  <span className="font-mono font-bold text-white text-sm">{invoice.invoiceNumber}</span>
                  <span className="block text-[11px] text-slate-500 mt-0.5">Terminal: {invoice.terminalCode || 'POS-01'}</span>
                </div>
                <div>
                  <span className="block text-slate-500 text-xs font-bold uppercase tracking-wider mb-1">Date & Time</span>
                  <span className="font-semibold text-slate-300">
                    {new Date(invoice.createdAt || invoice.businessDate).toLocaleDateString()}
                  </span>
                  <span className="block text-[11px] text-slate-500 mt-0.5">
                    {new Date(invoice.createdAt || invoice.businessDate).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}
                  </span>
                </div>
                <div>
                  <span className="block text-slate-500 text-xs font-bold uppercase tracking-wider mb-1">Customer</span>
                  <span className="font-semibold text-slate-300">{invoice.customerName || 'Walk-in Customer'}</span>
                  {invoice.customerPhone && (
                    <span className="block text-xs text-slate-500 font-mono mt-0.5 flex items-center gap-1">
                      <Phone className="w-3 h-3" /> {invoice.customerPhone}
                    </span>
                  )}
                </div>
                <div>
                  <span className="block text-slate-500 text-xs font-bold uppercase tracking-wider mb-1">Original Total</span>
                  <span className="font-black text-white text-base">₹{invoice.netPayable.toFixed(2)}</span>
                  <span className="block text-[11px] text-slate-500 mt-0.5">Paid via {invoice.paymentMode}</span>
                </div>
              </div>

              {/* Items List */}
              <div className="border border-slate-800 rounded-xl overflow-hidden shadow-inner">
                <table className="w-full text-left border-collapse text-sm">
                  <thead className="bg-slate-950 text-slate-400 font-bold border-b border-slate-800 text-xs">
                    <tr>
                      <th className="p-3">Product Name</th>
                      <th className="p-3 text-right">Unit Price</th>
                      <th className="p-3 text-center">Purchased Qty</th>
                      <th className="p-3 text-center">Return Qty</th>
                      <th className="p-3 text-right">Refund Total</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-slate-850 bg-slate-900/40">
                    {invoice.items.map((item: InvoiceItem) => {
                      const returnQty = returnItems[item.id] || 0;
                      const lineTotal = returnQty * item.unitPrice;
                      return (
                        <tr key={item.id} className="hover:bg-slate-950/40 transition-colors">
                          <td className="p-3">
                            <span className="font-semibold block text-slate-200">{item.productName}</span>
                            {item.barcode && <span className="text-xs text-slate-500 font-mono">{item.barcode}</span>}
                          </td>
                          <td className="p-3 text-right font-semibold text-slate-300">₹{item.unitPrice.toFixed(2)}</td>
                          <td className="p-3 text-center font-medium text-slate-400">{item.quantity}</td>
                          <td className="p-3 text-center">
                            <div className="inline-flex items-center bg-slate-950 border border-slate-800 rounded-lg p-1 gap-1">
                              <button
                                type="button"
                                onClick={() => handleQtyChange(item.id, returnQty - 1, item.quantity)}
                                className="w-7 h-7 rounded bg-slate-850 text-slate-300 font-bold hover:bg-slate-800 active:scale-90 transition-all flex items-center justify-center text-base disabled:opacity-30"
                                disabled={returnQty <= 0}
                              >
                                -
                              </button>
                              <input
                                type="number"
                                step="any"
                                value={returnQty || ''}
                                onChange={(e) => handleQtyChange(item.id, parseFloat(e.target.value) || 0, item.quantity)}
                                className="w-12 bg-transparent text-center text-white font-bold outline-none border-none text-sm [appearance:textfield] [&::-webkit-outer-spin-button]:appearance-none [&::-webkit-inner-spin-button]:appearance-none"
                              />
                              <button
                                type="button"
                                onClick={() => handleQtyChange(item.id, returnQty + 1, item.quantity)}
                                className="w-7 h-7 rounded bg-slate-850 text-slate-300 font-bold hover:bg-slate-800 active:scale-90 transition-all flex items-center justify-center text-base disabled:opacity-30"
                                disabled={returnQty >= item.quantity}
                              >
                                +
                              </button>
                            </div>
                          </td>
                          <td className="p-3 text-right font-bold text-indigo-400">₹{lineTotal.toFixed(2)}</td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>

              {/* Return Notes & Payment Options */}
              <div className="grid grid-cols-1 md:grid-cols-2 gap-5 pt-3 border-t border-slate-800">
                <div className="space-y-2">
                  <label className="text-xs font-bold text-slate-400 uppercase tracking-wider flex items-center gap-1.5">
                    <CreditCard className="w-4 h-4 text-indigo-400" /> Refund Payment Mode
                  </label>
                  <select
                    value={refundMode}
                    onChange={(e) => setRefundMode(e.target.value as any)}
                    className="w-full bg-slate-950 border border-slate-800 rounded-xl py-2.5 px-4 text-white font-semibold outline-none focus:ring-2 focus:ring-indigo-500 transition-all text-sm"
                  >
                    <option value="CASH">Cash Refund</option>
                    <option value="UPI">UPI / Digital Refund</option>
                    <option value="CREDIT_NOTE">Customer Wallet (Credit Note)</option>
                  </select>
                </div>

                <div className="space-y-2">
                  <label className="text-xs font-bold text-slate-400 uppercase tracking-wider flex items-center gap-1.5">
                    <MessageSquare className="w-4 h-4 text-indigo-400" /> Return Reason / Notes
                  </label>
                  <textarea
                    placeholder="Enter customer return reason (e.g. Defective item, wrong size)..."
                    value={notes}
                    onChange={(e) => setNotes(e.target.value)}
                    rows={2}
                    className="w-full bg-slate-950 border border-slate-800 rounded-xl py-2 px-3 text-white font-medium outline-none focus:ring-2 focus:ring-indigo-500 transition-all text-sm placeholder:text-slate-600 resize-none"
                  />
                </div>
              </div>
            </div>
          )}
        </div>

        {/* Footer */}
        {invoice && (
          <div className="bg-slate-950 p-4 border-t border-slate-800 flex flex-col sm:flex-row justify-between items-center gap-4">
            <div className="text-center sm:text-left">
              <span className="block text-xs font-bold text-slate-400 uppercase tracking-wider mb-0.5">Total Refund Amount</span>
              <span className="text-2xl font-black text-indigo-400">₹{totalRefund.toFixed(2)}</span>
            </div>
            <div className="flex gap-3 w-full sm:w-auto">
              <button
                type="button"
                onClick={() => {
                  setInvoice(null);
                  setReturnItems({});
                  setNotes('');
                }}
                className="flex-1 sm:flex-none border border-slate-800 hover:bg-slate-900 text-slate-300 font-bold px-5 py-2.5 rounded-xl transition-all text-sm"
                disabled={submitting}
              >
                Cancel
              </button>
              <button
                type="button"
                onClick={handleProcessReturn}
                disabled={submitting || totalRefund <= 0}
                className="flex-1 sm:flex-none bg-indigo-600 hover:bg-indigo-500 disabled:bg-slate-800 disabled:opacity-50 text-white font-black px-7 py-2.5 rounded-xl transition-all active:scale-95 shadow-lg shadow-indigo-600/10 text-sm flex items-center justify-center gap-2"
              >
                {submitting ? 'Processing...' : user?.role === 'Cashier' ? 'Request Override & Process' : 'Authorize & Process Return'}
              </button>
            </div>
          </div>
        )}
      </div>
    </div>
  );
};
