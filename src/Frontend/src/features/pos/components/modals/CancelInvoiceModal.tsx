import React, { useState, useRef, useEffect } from 'react';
import { X, Search, AlertOctagon, CornerUpLeft, ShieldAlert, CheckCircle2 } from 'lucide-react';
import { api } from '@/utils/api';
import { CANCELLATION_ALLOWED_ROLES } from '../../constants/roles';

interface InvoiceItem {
  id: string;
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

interface CancelInvoiceModalProps {
  isOpen: boolean;
  onClose: () => void;
  user?: {
    role: string;
    fullName: string;
  };
}

export const CancelInvoiceModal = ({ isOpen, onClose, user }: CancelInvoiceModalProps) => {
  const [invoiceNumber, setInvoiceNumber] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [invoice, setInvoice] = useState<Invoice | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [successMessage, setSuccessMessage] = useState('');

  const timeoutRef = useRef<NodeJS.Timeout | null>(null);

  useEffect(() => {
    return () => {
      if (timeoutRef.current) {
        clearTimeout(timeoutRef.current);
      }
    };
  }, []);

  const handleClose = () => {
    if (timeoutRef.current) {
      clearTimeout(timeoutRef.current);
      timeoutRef.current = null;
    }
    onClose();
  };

  if (!isOpen) return null;

  // Authorization Check - Backend Allow-List alignment
  const isAuthorized = !!(user?.role && CANCELLATION_ALLOWED_ROLES.includes(user.role));

  const handleSearch = async (e?: React.FormEvent) => {
    if (e) e.preventDefault();
    if (!invoiceNumber.trim()) return;

    setLoading(true);
    setError('');
    setInvoice(null);
    setSuccessMessage('');

    try {
      const res = await api.get<Invoice>(`/api/Pos/invoice/number/${invoiceNumber.trim()}`);
      if (res.data) {
        setInvoice(res.data);
      } else {
        setError('Invoice not found.');
      }
    } catch (err: any) {
      console.error(err);
      const msg = err.response?.data?.message || err.response?.data || 'Failed to search invoice.';
      setError(msg);
    } finally {
      setLoading(false);
    }
  };

  const handleCancelInvoice = async () => {
    if (!invoice) return;

    setSubmitting(true);
    setError('');
    setSuccessMessage('');

    try {
      const res = await api.post(`/api/pos/invoice/${invoice.id}/cancel`);
      if (res.data?.success) {
        setSuccessMessage(`Invoice ${invoice.invoiceNumber} has been successfully cancelled.`);
        setInvoice(null);
        setInvoiceNumber('');
        
        // Auto-close modal after 1.5 seconds showing success message
        timeoutRef.current = setTimeout(() => {
          handleClose();
          setSuccessMessage('');
        }, 1500);
      } else {
        setError('Cancellation failed.');
      }
    } catch (err: any) {
      console.error(err);
      const msg = err.response?.data?.message || err.response?.data || 'Failed to cancel invoice.';
      setError(msg);
    } finally {
      setSubmitting(false);
    }
  };

  // Determine if the loaded invoice can be cancelled
  const isCancellable = invoice && invoice.status !== 'CANCELLED' && invoice.status !== 'HOLD';

  return (
    <div className="fixed inset-0 bg-black/60 z-[60] flex items-center justify-center p-4 backdrop-blur-sm">
      <div className="bg-white rounded-2xl shadow-2xl w-full max-w-3xl overflow-hidden flex flex-col border border-slate-100">
        
        {/* Header */}
        <div className="bg-rose-950 p-5 flex justify-between items-center text-white border-b border-rose-900">
          <h2 className="text-xl font-bold flex items-center gap-2.5">
            <ShieldAlert className="w-6 h-6 text-rose-400 animate-pulse" />
            <span>Cancel POS Invoice</span>
          </h2>
          <button 
            onClick={handleClose} 
            className="text-rose-200 hover:text-white transition-colors p-1 hover:bg-rose-900/50 rounded-lg"
          >
            <X className="w-6 h-6" />
          </button>
        </div>

        {/* Content */}
        <div className="p-6 flex-1 overflow-y-auto max-h-[75vh]">
          {!isAuthorized ? (
            <div className="text-center py-10">
              <AlertOctagon className="w-16 h-16 text-rose-600 mx-auto mb-4" />
              <h3 className="text-lg font-bold text-slate-800 mb-2">Access Denied</h3>
              <p className="text-slate-600 max-w-md mx-auto">
                You are not authorized to cancel invoices. Please contact an Admin, Manager, Owner, or Supervisor to perform this operation.
              </p>
            </div>
          ) : (
            <div className="space-y-6">
              
              {/* Search Bar */}
              <form onSubmit={handleSearch} className="flex gap-3">
                <div className="relative flex-1">
                  <input
                    type="text"
                    value={invoiceNumber}
                    onChange={(e) => setInvoiceNumber(e.target.value)}
                    placeholder="Enter Invoice Number (e.g. INV-2026-0001)..."
                    className="w-full bg-slate-50 border border-slate-200 hover:border-slate-300 focus:border-rose-600 focus:bg-white text-slate-800 px-4 py-3 rounded-xl outline-none transition-all font-medium"
                    disabled={loading || submitting}
                  />
                </div>
                <button
                  type="submit"
                  disabled={loading || submitting}
                  className="bg-slate-900 hover:bg-slate-800 text-white font-bold px-6 py-3 rounded-xl transition-all flex items-center gap-2 hover:shadow-lg disabled:opacity-50"
                >
                  <Search className="w-5 h-5" />
                  <span>Search</span>
                </button>
              </form>

              {/* Status Messages */}
              {error && (
                <div className="bg-rose-50 border border-rose-200 text-rose-900 px-4 py-3.5 rounded-xl flex items-start gap-3">
                  <AlertOctagon className="w-5 h-5 text-rose-600 shrink-0 mt-0.5" />
                  <p className="text-sm font-semibold">{error}</p>
                </div>
              )}

              {successMessage && (
                <div className="bg-emerald-50 border border-emerald-200 text-emerald-900 px-4 py-3.5 rounded-xl flex items-start gap-3">
                  <CheckCircle2 className="w-5 h-5 text-emerald-600 shrink-0 mt-0.5" />
                  <p className="text-sm font-semibold">{successMessage}</p>
                </div>
              )}

              {/* Invoice Detail View */}
              {invoice && (
                <div className="space-y-6">
                  
                  {/* Metadata Header */}
                  <div className="bg-slate-50 rounded-xl p-4 border border-slate-200/60 grid grid-cols-2 sm:grid-cols-4 gap-4">
                    <div>
                      <span className="text-xs font-bold text-slate-400 block uppercase">Invoice Number</span>
                      <span className="font-mono font-bold text-slate-800">{invoice.invoiceNumber}</span>
                    </div>
                    <div>
                      <span className="text-xs font-bold text-slate-400 block uppercase">Date</span>
                      <span className="font-semibold text-slate-800">
                        {new Date(invoice.businessDate).toLocaleDateString()}
                      </span>
                    </div>
                    <div>
                      <span className="text-xs font-bold text-slate-400 block uppercase">Customer</span>
                      <span className="font-semibold text-slate-800">{invoice.customerName || 'Walk-in'}</span>
                    </div>
                    <div>
                      <span className="text-xs font-bold text-slate-400 block uppercase">Status</span>
                      <span className={`inline-block px-2.5 py-0.5 rounded-full text-xs font-bold ${
                        invoice.status === 'CANCELLED' ? 'bg-red-100 text-red-700' :
                        invoice.status === 'HOLD' ? 'bg-amber-100 text-amber-700' :
                        'bg-emerald-100 text-emerald-700'
                      }`}>
                        {invoice.status}
                      </span>
                    </div>
                  </div>

                  {/* Warning Guards */}
                  {invoice.status === 'CANCELLED' && (
                    <div className="bg-red-50 border border-red-200 text-red-900 p-4 rounded-xl flex items-start gap-3">
                      <AlertOctagon className="w-5 h-5 text-red-600 shrink-0 mt-0.5" />
                      <p className="text-sm font-semibold">
                        This invoice has already been cancelled and reversed. No further action can be taken.
                      </p>
                    </div>
                  )}

                  {invoice.status === 'HOLD' && (
                    <div className="bg-amber-50 border border-amber-200 text-amber-900 p-4 rounded-xl flex items-start gap-3">
                      <AlertOctagon className="w-5 h-5 text-amber-600 shrink-0 mt-0.5" />
                      <p className="text-sm font-semibold">
                        Held invoices cannot be cancelled. Please delete or resume this hold ticket from the Hold/Resume panel instead.
                      </p>
                    </div>
                  )}

                  {/* Product items table */}
                  <div className="border border-slate-200 rounded-xl overflow-hidden">
                    <table className="w-full text-left border-collapse">
                      <thead className="bg-slate-100">
                        <tr>
                          <th className="p-3 border-b text-xs font-bold text-slate-500 uppercase">Product</th>
                          <th className="p-3 border-b text-xs font-bold text-slate-500 uppercase text-center">Qty</th>
                          <th className="p-3 border-b text-xs font-bold text-slate-500 uppercase text-right">Price</th>
                          <th className="p-3 border-b text-xs font-bold text-slate-500 uppercase text-right">Total</th>
                        </tr>
                      </thead>
                      <tbody className="divide-y divide-slate-100 text-sm">
                        {invoice.items.map((item) => (
                          <tr key={item.id} className="hover:bg-slate-50">
                            <td className="p-3">
                              <div className="font-semibold text-slate-800">{item.productName}</div>
                              {item.barcode && <div className="text-xs text-slate-400 font-mono">{item.barcode}</div>}
                            </td>
                            <td className="p-3 text-center text-slate-700 font-medium">{item.quantity}</td>
                            <td className="p-3 text-right text-slate-700 font-medium">₹{item.unitPrice.toFixed(2)}</td>
                            <td className="p-3 text-right text-slate-800 font-bold">₹{item.totalAmount.toFixed(2)}</td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>

                  {/* Financials & Reversal Summary */}
                  <div className="bg-slate-50 rounded-xl p-5 border border-slate-200 flex flex-col md:flex-row justify-between gap-6">
                    <div className="space-y-2">
                      <h4 className="text-sm font-bold text-slate-700">Reversal Summary</h4>
                      <p className="text-xs text-slate-500 max-w-sm">
                        Cancelling this invoice will trigger dynamic general ledger offset entries and restore physical batch inventories. All loyalty points and wallet balances applied will be fully reversed.
                      </p>
                    </div>
                    <div className="space-y-1.5 min-w-[200px] text-sm">
                      <div className="flex justify-between text-slate-500">
                        <span>Subtotal:</span>
                        <span>₹{invoice.subTotal.toFixed(2)}</span>
                      </div>
                      <div className="flex justify-between text-slate-500">
                        <span>Tax Amount:</span>
                        <span>₹{invoice.taxAmount.toFixed(2)}</span>
                      </div>
                      {invoice.discountAmount > 0 && (
                        <div className="flex justify-between text-rose-600 font-semibold">
                          <span>Discount:</span>
                          <span>-₹{invoice.discountAmount.toFixed(2)}</span>
                        </div>
                      )}
                      <div className="border-t border-slate-200 my-2 pt-2 flex justify-between text-lg font-bold text-rose-700">
                        <span>Refund Total:</span>
                        <span>₹{invoice.netPayable.toFixed(2)}</span>
                      </div>
                    </div>
                  </div>

                  {/* Cancellation Action Box */}
                  {isCancellable && (
                    <div className="bg-rose-50 border border-rose-200 rounded-xl p-4 flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
                      <div className="flex items-start gap-3">
                        <CornerUpLeft className="w-5 h-5 text-rose-700 shrink-0 mt-0.5" />
                        <div>
                          <h4 className="text-sm font-bold text-rose-950">Irreversible Action</h4>
                          <p className="text-xs text-rose-700">
                            Confirm that you wish to cancel this transaction. This action cannot be undone.
                          </p>
                        </div>
                      </div>
                      <button
                        onClick={handleCancelInvoice}
                        disabled={submitting}
                        className="bg-rose-700 hover:bg-rose-800 active:scale-[0.98] text-white font-bold px-6 py-3 rounded-xl transition-all shadow-md hover:shadow-lg disabled:opacity-50 inline-flex items-center justify-center gap-2 shrink-0"
                      >
                        {submitting ? 'Cancelling...' : 'Confirm Cancellation'}
                      </button>
                    </div>
                  )}

                </div>
              )}

            </div>
          )}
        </div>

      </div>
    </div>
  );
};
