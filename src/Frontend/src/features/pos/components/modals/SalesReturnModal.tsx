import React, { useState } from 'react';
import { X, Search, RotateCcw, CreditCard, Award, MessageSquare, AlertCircle } from 'lucide-react';
import { api } from '@/utils/api';

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
  const [searchResults, setSearchResults] = useState<Invoice[]>([]);

  if (!isOpen) return null;

  const handleSelectInvoice = (inv: Invoice) => {
    setInvoice(inv);
    setSearchResults([]);
    const initialQtys: Record<string, number> = {};
    inv.items.forEach((item: InvoiceItem) => {
      initialQtys[item.id] = 0;
    });
    setReturnItems(initialQtys);
    setInvoiceNumber(inv.invoiceNumber);
  };

  const handleSearch = async (e?: React.FormEvent) => {
    if (e) e.preventDefault();
    if (!invoiceNumber.trim()) return;

    setLoading(true);
    setError('');
    setInvoice(null);
    setReturnItems({});
    setSuccessMessage('');
    setSearchResults([]);

    try {
      const res = await api.get<Invoice[]>(`/api/pos/invoice/search?query=${encodeURIComponent(invoiceNumber.trim())}`);
      if (res.data && res.data.length > 0) {
        setSearchResults(res.data);
        if (res.data.length === 1) {
          handleSelectInvoice(res.data[0]);
        }
      } else {
        setError('No matching invoices found.');
      }
    } catch (err: any) {
      console.error(err);
      setError('Invoice not found or search failed. Please try again.');
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

        await api.post('/api/AccountsReceivable/returns', payload);
        
        // Show friendly cashier-readable success message
        setSuccessMessage(`Refund processed successfully. Return transaction completed.`);
        setInvoice(null);
        setInvoiceNumber('');
        setReturnItems({});
        setNotes('');
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

  return (
    <div className="fixed inset-0 bg-black/70 z-modal flex items-center justify-center p-4 backdrop-blur-md">
      <div className="bg-slate-900 border border-slate-800 rounded-2xl shadow-2xl w-full max-w-4xl overflow-hidden flex flex-col text-slate-100 max-h-[90vh]">
        {/* Header */}
        <div className="bg-slate-950 p-5 border-b border-slate-800 flex justify-between items-center">
          <h2 className="text-xl font-bold flex items-center gap-2.5 text-indigo-400">
            <RotateCcw className="w-6 h-6 animate-pulse" /> Process Sales Return
          </h2>
          <button 
            onClick={onClose} 
            className="text-slate-400 hover:text-white transition-colors p-1.5 rounded-lg hover:bg-slate-800"
            disabled={submitting}
          >
            <X className="w-6 h-6" />
          </button>
        </div>

        {/* Content */}
        <div className="p-6 overflow-y-auto space-y-6 flex-1">
          {successMessage && (
            <div className="bg-emerald-950/80 border border-emerald-500/30 p-4 rounded-xl text-emerald-400 flex items-center gap-3">
              <Award className="w-6 h-6 shrink-0 text-emerald-400" />
              <div>
                <p className="font-bold">Transaction Complete</p>
                <p className="text-sm opacity-90">{successMessage}</p>
              </div>
            </div>
          )}

          {error && (
            <div className="bg-rose-950/85 border border-rose-500/30 p-4 rounded-xl text-rose-400 flex items-center gap-3">
              <AlertCircle className="w-6 h-6 shrink-0 text-rose-400" />
              <p className="text-sm font-semibold">{error}</p>
            </div>
          )}

          {/* Search Form */}
          <form onSubmit={handleSearch} className="flex gap-3">
            <div className="relative flex-1">
              <Search className="absolute left-3.5 top-1/2 -translate-y-1/2 text-slate-500 w-5 h-5" />
              <input
                type="text"
                placeholder="Enter Invoice Number (e.g. INV-POS-01-123456)..."
                value={invoiceNumber}
                onChange={(e) => setInvoiceNumber(e.target.value)}
                className="w-full bg-slate-950 border border-slate-800 rounded-xl py-3 pl-11 pr-4 text-white font-medium outline-none focus:ring-2 focus:ring-indigo-500 transition-all text-sm placeholder:text-slate-600"
                disabled={loading || submitting}
              />
            </div>
            <button
              type="submit"
              disabled={loading || submitting || !invoiceNumber.trim()}
              className="bg-indigo-600 hover:bg-indigo-500 disabled:bg-slate-800 text-white font-bold px-6 rounded-xl transition-all active:scale-95 flex items-center justify-center gap-2 text-sm shadow-lg shadow-indigo-600/10"
            >
              {loading ? 'Searching...' : 'Search'}
            </button>
          </form>

          {/* Matches list if multiple */}
          {searchResults.length > 1 && (
            <div className="bg-slate-950 border border-slate-800 rounded-xl p-4 space-y-2">
              <p className="text-xs font-bold text-slate-500 uppercase tracking-wider mb-2">Multiple matches found. Please select one:</p>
              <div className="divide-y divide-slate-800 max-h-48 overflow-y-auto">
                {searchResults.map((inv: any) => (
                  <button
                    key={inv.id}
                    type="button"
                    onClick={() => handleSelectInvoice(inv)}
                    className="w-full text-left py-2.5 px-3 hover:bg-slate-900 rounded-lg transition-colors flex justify-between items-center text-sm"
                  >
                    <div>
                      <span className="font-mono font-bold text-slate-200">{inv.invoiceNumber}</span>
                      <span className="text-slate-500 text-xs ml-2">({new Date(inv.createdAt || inv.businessDate).toLocaleString()})</span>
                      <span className="text-slate-400 text-xs block">Cashier: {inv.cashierName || 'Unknown'}</span>
                    </div>
                    <div className="text-right">
                      <span className="block text-slate-300 font-semibold">{inv.customerName || 'Walk-in'}</span>
                      <span className="text-xs text-indigo-400 font-bold">₹{inv.netPayable.toFixed(2)}</span>
                    </div>
                  </button>
                ))}
              </div>
            </div>
          )}

          {/* Invoice Details and Items table */}
          {invoice && (
            <div className="space-y-6">
              {/* Invoice Metadata */}
              <div className="grid grid-cols-2 sm:grid-cols-4 gap-4 bg-slate-950/60 p-4.5 rounded-xl border border-slate-850 text-sm">
                <div>
                  <span className="block text-slate-500 text-xs font-bold uppercase tracking-wider mb-1">Invoice Number</span>
                  <span className="font-mono font-bold text-slate-200">{invoice.invoiceNumber}</span>
                </div>
                <div>
                  <span className="block text-slate-500 text-xs font-bold uppercase tracking-wider mb-1">Business Date</span>
                  <span className="font-semibold text-slate-300">{new Date(invoice.businessDate).toLocaleDateString()}</span>
                </div>
                <div>
                  <span className="block text-slate-500 text-xs font-bold uppercase tracking-wider mb-1">Customer</span>
                  <span className="font-semibold text-slate-300">{invoice.customerName || 'Walk-in Customer'}</span>
                  {invoice.customerPhone && <span className="block text-xs text-slate-500 font-mono mt-0.5">{invoice.customerPhone}</span>}
                </div>
                <div>
                  <span className="block text-slate-500 text-xs font-bold uppercase tracking-wider mb-1">Original Total</span>
                  <span className="font-black text-slate-200 text-base">₹{invoice.netPayable.toFixed(2)}</span>
                </div>
              </div>

              {/* Items List */}
              <div className="border border-slate-850 rounded-xl overflow-hidden">
                <table className="w-full text-left border-collapse text-sm">
                  <thead className="bg-slate-950 text-slate-400 font-bold border-b border-slate-850">
                    <tr>
                      <th className="p-3">Product Name</th>
                      <th className="p-3 text-right">Unit Price</th>
                      <th className="p-3 text-center">Purchased Qty</th>
                      <th className="p-3 text-center">Return Qty</th>
                      <th className="p-3 text-right">Refund Line Total</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-slate-850 bg-slate-900/40">
                    {invoice.items.map((item: InvoiceItem) => {
                      const returnQty = returnItems[item.id] || 0;
                      const lineTotal = returnQty * item.unitPrice;
                      return (
                        <tr key={item.id} className="hover:bg-slate-950/30">
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
                                className="w-7 h-7 rounded bg-slate-850 text-slate-300 font-bold hover:bg-slate-800 active:scale-90 transition-all flex items-center justify-center text-base"
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
                                className="w-7 h-7 rounded bg-slate-850 text-slate-300 font-bold hover:bg-slate-800 active:scale-90 transition-all flex items-center justify-center text-base"
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
              <div className="grid grid-cols-1 md:grid-cols-2 gap-6 pt-4 border-t border-slate-800">
                <div className="space-y-4">
                  <div className="space-y-2">
                    <label className="text-xs font-bold text-slate-500 uppercase tracking-wider flex items-center gap-1.5">
                      <CreditCard className="w-4 h-4 text-indigo-400" /> Refund Payment Mode
                    </label>
                    <select
                      value={refundMode}
                      onChange={(e) => setRefundMode(e.target.value as any)}
                      className="w-full bg-slate-950 border border-slate-800 rounded-xl py-3 px-4 text-white font-semibold outline-none focus:ring-2 focus:ring-indigo-500 transition-all text-sm"
                    >
                      <option value="CASH">Cash Refund</option>
                      <option value="UPI">UPI / Digital</option>
                      <option value="CREDIT_NOTE">Customer Wallet (Credit Note)</option>
                    </select>
                  </div>
                </div>

                <div className="space-y-4">
                  <div className="space-y-2">
                    <label className="text-xs font-bold text-slate-500 uppercase tracking-wider flex items-center gap-1.5">
                      <MessageSquare className="w-4 h-4 text-indigo-400" /> Reason / Notes (Display Only)
                    </label>
                    <textarea
                      placeholder="Input customer return reason..."
                      value={notes}
                      onChange={(e) => setNotes(e.target.value)}
                      rows={2}
                      className="w-full bg-slate-950 border border-slate-800 rounded-xl py-2.5 px-4 text-white font-medium outline-none focus:ring-2 focus:ring-indigo-500 transition-all text-sm placeholder:text-slate-600 resize-none"
                    />
                  </div>
                </div>
              </div>
            </div>
          )}
        </div>

        {/* Footer */}
        {invoice && (
          <div className="bg-slate-950 p-5 border-t border-slate-850 flex flex-col sm:flex-row justify-between items-center gap-4">
            <div className="text-center sm:text-left">
              <span className="block text-xs font-bold text-slate-500 uppercase tracking-wider mb-0.5">Total Refund Amount</span>
              <span className="text-2xl font-black text-indigo-400">₹{totalRefund.toFixed(2)}</span>
            </div>
            <div className="flex gap-3 w-full sm:w-auto">
              <button
                type="button"
                onClick={() => {
                  setInvoice(null);
                  setInvoiceNumber('');
                  setReturnItems({});
                  setNotes('');
                }}
                className="flex-1 sm:flex-none border border-slate-850 hover:bg-slate-900 text-slate-300 font-bold px-6 py-3 rounded-xl transition-all"
                disabled={submitting}
              >
                Clear
              </button>
              <button
                type="button"
                onClick={handleProcessReturn}
                disabled={submitting || totalRefund <= 0}
                className="flex-1 sm:flex-none bg-indigo-600 hover:bg-indigo-500 disabled:bg-slate-800 disabled:opacity-50 text-white font-black px-8 py-3 rounded-xl transition-all active:scale-95 shadow-lg shadow-indigo-600/10 text-sm"
              >
                {submitting ? 'Processing...' : 'Authorize & Process'}
              </button>
            </div>
          </div>
        )}
      </div>
    </div>
  );
};
