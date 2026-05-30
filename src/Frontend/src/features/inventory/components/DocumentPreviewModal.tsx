import React, { useState, useEffect } from 'react';
import { X, FileText, Calendar, User, Tag, ShoppingBag, Receipt, Truck, ArrowRightLeft, ShieldAlert, ClipboardCheck } from 'lucide-react';
import { api } from '../../../utils/api';

interface DocumentPreviewModalProps {
  docId: string;
  docType: string;
  referenceNumber?: string;
  onClose: () => void;
}

export const DocumentPreviewModal = ({ docId, docType, referenceNumber, onClose }: DocumentPreviewModalProps) => {
  const [data, setData] = useState<any>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  let normalizedType = docType.toUpperCase() === 'SALE_OVERRIDE' ? 'SALE' : docType.toUpperCase();
  if (normalizedType === 'ADJ' && referenceNumber && referenceNumber.startsWith('TAKE-')) {
    normalizedType = 'STOCK_TAKE';
  }

  useEffect(() => {
    const fetchDoc = async () => {
      try {
        setLoading(true);
        setError(null);
        let endpoint = '';

        if (normalizedType === 'SALE') {
          endpoint = `/api/pos/invoice/${docId}`;
        } else if (normalizedType === 'ADJ' || normalizedType === 'ADJUSTMENT') {
          endpoint = `/api/inventory/stock-adjustment/${docId}`;
        } else if (normalizedType === 'STOCK_TAKE') {
          endpoint = `/api/inventory/stock-take/${docId}`;
        } else if (normalizedType === 'GRN') {
          endpoint = `/api/inventory/grn/${docId}`;
        } else {
          throw new Error(`Unsupported document type: ${docType}`);
        }

        const response = await api.get(endpoint);
        setData(response.data);
      } catch (err: any) {
        console.error('Failed to load document details', err);
        setError(err.response?.data?.message || err.message || 'Failed to load details.');
      } finally {
        setLoading(false);
      }
    };

    if (docId && docId !== '00000000-0000-0000-0000-000000000000') {
      fetchDoc();
    } else {
      setLoading(false);
      setError('Invalid reference document key. This is a system-generated adjustment or seed balance.');
    }
  }, [docId, docType, referenceNumber]);

  const renderSalesInvoice = () => {
    if (!data) return null;
    const tendered = (data.cashAmount || 0) + (data.upiAmount || 0) + (data.cardAmount || 0) + (data.walletAmount || 0);
    const change = Math.max(0, tendered - (data.netPayable || 0));

    return (
      <div className="space-y-6">
        {/* Header Details */}
        <div className="grid grid-cols-2 gap-4 bg-slate-50 p-4 rounded-lg border border-slate-100">
          <div>
            <div className="text-xs text-slate-500 font-semibold uppercase tracking-wider">Invoice Info</div>
            <div className="text-sm font-bold text-slate-800 mt-1">{data.invoiceNumber}</div>
            <div className="text-xs text-slate-500 mt-1">Date: {new Date(data.businessDate).toLocaleDateString()} {new Date(data.createdAt).toLocaleTimeString()}</div>
          </div>
          <div>
            <div className="text-xs text-slate-500 font-semibold uppercase tracking-wider">Terminal & Staff</div>
            <div className="text-sm font-semibold text-slate-800 mt-1">Terminal: {data.terminalCode || 'POS-01'}</div>
            <div className="text-xs text-slate-500 mt-1">Cashier: {data.cashierName || 'System'}</div>
          </div>
          {data.customerName && (
            <div className="col-span-2 border-t pt-2 mt-2">
              <div className="text-xs text-slate-500 font-semibold uppercase tracking-wider">Customer</div>
              <div className="text-sm font-medium text-slate-800 mt-0.5">{data.customerName} {data.customerPhone ? `(${data.customerPhone})` : ''}</div>
            </div>
          )}
        </div>

        {/* Items List */}
        <div>
          <h4 className="text-sm font-bold text-slate-700 mb-3 flex items-center">
            <ShoppingBag className="w-4 h-4 mr-1.5 text-blue-500" /> Sold Items
          </h4>
          <div className="max-h-60 overflow-y-auto border border-slate-200 rounded-lg">
            <table className="w-full text-left border-collapse">
              <thead className="bg-slate-50 text-xs font-semibold text-slate-600 sticky top-0">
                <tr className="border-b">
                  <th className="p-2.5">Item Name</th>
                  <th className="p-2.5 text-right">Qty</th>
                  <th className="p-2.5 text-right">Price</th>
                  <th className="p-2.5 text-right">Disc</th>
                  <th className="p-2.5 text-right">GST %</th>
                  <th className="p-2.5 text-right">Net Total</th>
                </tr>
              </thead>
              <tbody className="text-xs text-slate-800">
                {data.items?.map((item: any) => (
                  <tr key={item.id} className="border-b hover:bg-slate-50">
                    <td className="p-2.5">
                      <div className="font-semibold">{item.productName}</div>
                      {item.barcode && <div className="text-[10px] text-slate-400 font-mono mt-0.5">{item.barcode}</div>}
                    </td>
                    <td className="p-2.5 text-right font-medium">{item.quantity}</td>
                    <td className="p-2.5 text-right">₹{item.unitPrice.toFixed(2)}</td>
                    <td className="p-2.5 text-right text-red-500">{item.discountAmount > 0 ? `-₹${item.discountAmount.toFixed(2)}` : '-'}</td>
                    <td className="p-2.5 text-right text-slate-600">{(item.cgstRate + item.sgstRate).toFixed(0)}%</td>
                    <td className="p-2.5 text-right font-bold">₹{item.totalAmount.toFixed(2)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>

        {/* GST Summary */}
        <div className="bg-slate-50 p-4 rounded-lg border border-slate-100 text-xs">
          <div className="font-bold text-slate-700 mb-2">GST Breakup Summary</div>
          <div className="grid grid-cols-4 font-semibold text-slate-500 border-b pb-1 mb-1.5">
            <div>Slab</div>
            <div className="text-right">Taxable</div>
            <div className="text-right">CGST</div>
            <div className="text-right">SGST</div>
          </div>
          {data.items && Array.from(new Set(data.items.map((i: any) => (i.cgstRate + i.sgstRate)))).map((slabRate: any) => {
            const slabItems = data.items.filter((i: any) => (i.cgstRate + i.sgstRate) === slabRate);
            const taxable = slabItems.reduce((sum: number, i: any) => sum + (i.quantity * i.unitPrice - i.discountAmount), 0);
            const cgst = slabItems.reduce((sum: number, i: any) => sum + i.cgstAmount, 0);
            const sgst = slabItems.reduce((sum: number, i: any) => sum + i.sgstAmount, 0);
            if (slabRate === 0) return null;
            return (
              <div key={slabRate} className="grid grid-cols-4 text-slate-700 mt-1">
                <div className="font-medium">GST {slabRate.toFixed(0)}%</div>
                <div className="text-right">₹{taxable.toFixed(2)}</div>
                <div className="text-right">₹{cgst.toFixed(2)}</div>
                <div className="text-right">₹{sgst.toFixed(2)}</div>
              </div>
            );
          })}
        </div>

        {/* Invoice Financial Summary */}
        <div className="grid grid-cols-2 gap-6 pt-2 border-t">
          {/* Payment breakdown */}
          <div className="space-y-1.5 text-xs text-slate-650">
            <span className="font-bold text-slate-700 block mb-1">Tender Breakdown</span>
            {data.cashAmount > 0 && <div className="flex justify-between font-medium"><span>Cash:</span><span>₹{data.cashAmount.toFixed(2)}</span></div>}
            {data.upiAmount > 0 && <div className="flex justify-between font-medium"><span>UPI Payment:</span><span>₹{data.upiAmount.toFixed(2)}</span></div>}
            {data.cardAmount > 0 && <div className="flex justify-between font-medium"><span>Card:</span><span>₹{data.cardAmount.toFixed(2)}</span></div>}
            {data.walletAmount > 0 && <div className="flex justify-between font-medium"><span>Wallet:</span><span>₹{data.walletAmount.toFixed(2)}</span></div>}
            <div className="flex justify-between text-slate-500 border-t pt-1 mt-1 font-semibold">
              <span>Total Tendered:</span><span>₹{tendered.toFixed(2)}</span>
            </div>
            {change > 0 && (
              <div className="flex justify-between text-green-600 font-bold">
                <span>Change Returned:</span><span>₹{change.toFixed(2)}</span>
              </div>
            )}
          </div>

          {/* Subtotals */}
          <div className="space-y-2 text-sm text-slate-750">
            <div className="flex justify-between text-xs text-slate-500 font-medium">
              <span>Sub Total:</span><span>₹{data.subTotal.toFixed(2)}</span>
            </div>
            {data.discountAmount > 0 && (
              <div className="flex justify-between text-xs text-red-500 font-medium">
                <span>Discount:</span><span>-₹{data.discountAmount.toFixed(2)}</span>
              </div>
            )}
            <div className="flex justify-between text-xs text-slate-500 font-medium">
              <span>Tax (GST):</span><span>+₹{data.taxAmount.toFixed(2)}</span>
            </div>
            {data.roundOff !== 0 && (
              <div className="flex justify-between text-xs text-slate-400 font-medium">
                <span>Round Off:</span><span>{data.roundOff > 0 ? '+' : ''}₹{data.roundOff.toFixed(2)}</span>
              </div>
            )}
            <div className="flex justify-between font-black text-lg text-slate-800 border-t pt-2">
              <span>Net Payable:</span><span>₹{data.netPayable.toFixed(2)}</span>
            </div>
          </div>
        </div>

        {/* E-Invoice data if any */}
        {data.irn && (
          <div className="p-3 bg-slate-50 border rounded-lg text-[10px] font-mono text-slate-500 break-all space-y-1">
            <div className="font-bold">E-INVOICE VALIDATED</div>
            <div>IRN: {data.irn}</div>
            <div>Ack No: {data.ackNo} | Date: {data.ackDate}</div>
          </div>
        )}
      </div>
    );
  };

  const renderStockAdjustment = () => {
    if (!data) return null;
    return (
      <div className="space-y-6">
        <div className="grid grid-cols-2 gap-4 bg-slate-50 p-4 rounded-lg border border-slate-100">
          <div>
            <div className="text-xs text-slate-500 font-semibold uppercase tracking-wider">Adjustment Info</div>
            <div className="text-sm font-bold text-slate-800 mt-1">{data.adjustmentNumber}</div>
            <div className="text-xs text-slate-500 mt-1">Date: {new Date(data.createdAt).toLocaleDateString()} {new Date(data.createdAt).toLocaleTimeString()}</div>
          </div>
          <div>
            <div className="text-xs text-slate-500 font-semibold uppercase tracking-wider">Details</div>
            <div className="text-sm font-semibold text-slate-800 mt-1">
              Reason: <span className="px-2 py-0.5 rounded-full text-xs bg-slate-200 text-slate-800 font-bold">{data.reason}</span>
            </div>
            <div className="text-xs text-slate-500 mt-1">
              Status: <span className={`px-2 py-0.5 rounded-full text-xs font-bold ${data.status === 'APPROVED' ? 'bg-green-100 text-green-800' : 'bg-amber-100 text-amber-800'}`}>{data.status}</span>
            </div>
          </div>
          {data.approvedByName && (
            <div className="col-span-2 border-t pt-2 mt-2">
              <div className="text-xs text-slate-500 font-semibold uppercase tracking-wider">Approved By</div>
              <div className="text-sm font-medium text-slate-800 mt-0.5">{data.approvedByName}</div>
            </div>
          )}
        </div>

        <div>
          <h4 className="text-sm font-bold text-slate-700 mb-3 flex items-center">
            <ArrowRightLeft className="w-4 h-4 mr-1.5 text-orange-500" /> Adjusted Products
          </h4>
          <div className="border rounded-lg overflow-hidden">
            <table className="w-full text-left border-collapse text-xs">
              <thead className="bg-slate-50 font-semibold text-slate-600">
                <tr className="border-b">
                  <th className="p-3">Product Name</th>
                  <th className="p-3 text-right">Adjusted Qty</th>
                  <th className="p-3 text-right">Unit Cost</th>
                  <th className="p-3 text-right">Total Cost</th>
                </tr>
              </thead>
              <tbody>
                {data.items?.map((item: any) => (
                  <tr key={item.id} className="border-b hover:bg-slate-50">
                    <td className="p-3">
                      <div className="font-semibold text-slate-800">{item.productName}</div>
                      <div className="text-[10px] text-slate-400 font-mono mt-0.5">{item.productCode}</div>
                    </td>
                    <td className="p-3 text-right">
                      <span className={`font-bold ${item.adjustedQuantity > 0 ? 'text-green-600' : 'text-red-600'}`}>
                        {item.adjustedQuantity > 0 ? '+' : ''}{item.adjustedQuantity}
                      </span>
                    </td>
                    <td className="p-3 text-right text-slate-600">₹{item.unitCost.toFixed(2)}</td>
                    <td className="p-3 text-right font-bold text-slate-800">
                      ₹{Math.abs(item.adjustedQuantity * item.unitCost).toFixed(2)}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      </div>
    );
  };

  const renderGRN = () => {
    if (!data) return null;
    return (
      <div className="space-y-6">
        <div className="grid grid-cols-2 gap-4 bg-slate-50 p-4 rounded-lg border border-slate-100">
          <div>
            <div className="text-xs text-slate-500 font-semibold uppercase tracking-wider">GRN Details</div>
            <div className="text-sm font-bold text-slate-800 mt-1">{data.grnNumber}</div>
            <div className="text-xs text-slate-500 mt-1">Date: {new Date(data.receivedDate || data.createdAt).toLocaleDateString()}</div>
          </div>
          <div>
            <div className="text-xs text-slate-500 font-semibold uppercase tracking-wider">Supplier & Invoice</div>
            <div className="text-sm font-bold text-slate-800 mt-1">{data.supplierName}</div>
            <div className="text-xs text-slate-500 mt-1">Invoice: {data.supplierInvoiceNumber || '-'}</div>
          </div>
          <div className="col-span-2 border-t pt-2 mt-2 grid grid-cols-2 gap-2 text-xs">
            <div>
              <span className="text-slate-500 font-semibold">Status:</span>{' '}
              <span className={`px-2 py-0.5 rounded-full text-xs font-bold ${data.status === 'CONFIRMED' ? 'bg-green-100 text-green-800' : 'bg-amber-100 text-amber-800'}`}>{data.status}</span>
            </div>
            <div className="text-right">
              <span className="text-slate-500 font-semibold">Total Amount:</span>{' '}
              <span className="font-bold text-slate-800">₹{data.totalAmount.toFixed(2)}</span>
            </div>
          </div>
        </div>

        <div>
          <h4 className="text-sm font-bold text-slate-700 mb-3 flex items-center">
            <Truck className="w-4 h-4 mr-1.5 text-green-500" /> Received Items
          </h4>
          <div className="border rounded-lg overflow-hidden">
            <table className="w-full text-left border-collapse text-xs">
              <thead className="bg-slate-50 font-semibold text-slate-600">
                <tr className="border-b">
                  <th className="p-3">Product Name</th>
                  <th className="p-3 text-right">Received</th>
                  <th className="p-3 text-right">Accepted</th>
                  <th className="p-3 text-right">Rejected</th>
                  <th className="p-3 text-right">Unit Cost</th>
                  <th className="p-3 text-right">Total</th>
                </tr>
              </thead>
              <tbody>
                {data.items?.map((item: any) => (
                  <tr key={item.id} className="border-b hover:bg-slate-50">
                    <td className="p-3">
                      <div className="font-semibold text-slate-800">{item.productName}</div>
                      <div className="text-[10px] text-slate-400 font-mono mt-0.5">{item.productCode}</div>
                      {item.batchNumber && (
                        <div className="text-[10px] text-slate-500 mt-0.5">
                          Batch: {item.batchNumber} {item.expiryDate ? `(Exp: ${item.expiryDate.substring(0, 10)})` : ''}
                        </div>
                      )}
                      {item.rejectionReason && (
                        <div className="text-[10px] text-red-500 mt-0.5 italic flex items-center font-semibold">
                          <ShieldAlert className="w-3 h-3 mr-0.5 text-red-550" /> Reason: {item.rejectionReason}
                        </div>
                      )}
                    </td>
                    <td className="p-3 text-right font-medium text-slate-650">{item.receivedQuantity}</td>
                    <td className="p-3 text-right font-bold text-green-600">{item.acceptedQuantity}</td>
                    <td className="p-3 text-right font-bold text-red-600">{item.rejectedQuantity}</td>
                    <td className="p-3 text-right text-slate-600">₹{item.unitCost.toFixed(2)}</td>
                    <td className="p-3 text-right font-bold text-slate-800 font-mono">₹{item.totalCost.toFixed(2)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      </div>
    );
  };

  const renderStockTake = () => {
    if (!data) return null;
    return (
      <div className="space-y-6">
        <div className="grid grid-cols-2 gap-4 bg-slate-50 p-4 rounded-lg border border-slate-100">
          <div>
            <div className="text-xs text-slate-500 font-semibold uppercase tracking-wider">Stock Take Info</div>
            <div className="text-sm font-bold text-slate-800 mt-1">{data.takeNumber}</div>
            <div className="text-xs text-slate-500 mt-1">Date: {new Date(data.createdAt).toLocaleDateString()} {new Date(data.createdAt).toLocaleTimeString()}</div>
          </div>
          <div>
            <div className="text-xs text-slate-500 font-semibold uppercase tracking-wider">Details</div>
            <div className="text-sm font-semibold text-slate-800 mt-1">
              Type: <span className="px-2 py-0.5 rounded-full text-xs bg-indigo-50 text-indigo-800 font-black">Stock Take Variance</span>
            </div>
            <div className="text-xs text-slate-500 mt-1">
              Status: <span className={`px-2 py-0.5 rounded-full text-xs font-bold ${data.status === 'APPROVED' ? 'bg-green-100 text-green-800' : 'bg-amber-100 text-amber-800'}`}>{data.status}</span>
            </div>
          </div>
          {data.approvedByName && (
            <div className="col-span-2 border-t pt-2 mt-2">
              <div className="text-xs text-slate-500 font-semibold uppercase tracking-wider">Approved By</div>
              <div className="text-sm font-medium text-slate-800 mt-0.5">{data.approvedByName}</div>
            </div>
          )}
        </div>

        <div>
          <h4 className="text-sm font-bold text-slate-700 mb-3 flex items-center">
            <ArrowRightLeft className="w-4 h-4 mr-1.5 text-indigo-500" /> Count Variances
          </h4>
          <div className="border rounded-lg overflow-hidden">
            <table className="w-full text-left border-collapse text-xs">
              <thead className="bg-slate-50 font-semibold text-slate-600">
                <tr className="border-b">
                  <th className="p-3">Product Name</th>
                  <th className="p-3 text-right">System Qty</th>
                  <th className="p-3 text-right">Physical Qty</th>
                  <th className="p-3 text-right">Variance</th>
                </tr>
              </thead>
              <tbody>
                {data.items?.map((item: any, idx: number) => (
                  <tr key={idx} className="border-b hover:bg-slate-50">
                    <td className="p-3">
                      <div className="font-semibold text-slate-800">{item.productName}</div>
                      <div className="text-[10px] text-slate-500 mt-0.5">Batch: {item.batchNumber || 'NO BATCH'}</div>
                    </td>
                    <td className="p-3 text-right text-slate-600">{item.systemQuantity}</td>
                    <td className="p-3 text-right text-slate-800 font-medium">{item.physicalQuantity}</td>
                    <td className="p-3 text-right">
                      <span className={`font-black ${item.varianceQuantity > 0 ? 'text-green-600' : item.varianceQuantity < 0 ? 'text-red-600' : 'text-slate-500'}`}>
                        {item.varianceQuantity > 0 ? `+${item.varianceQuantity}` : item.varianceQuantity}
                      </span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      </div>
    );
  };

  const getIcon = () => {
    if (normalizedType === 'SALE') return <Receipt className="w-6 h-6 text-blue-500 mr-2" />;
    if (normalizedType === 'ADJ' || normalizedType === 'ADJUSTMENT') return <ArrowRightLeft className="w-6 h-6 text-orange-500 mr-2" />;
    if (normalizedType === 'STOCK_TAKE') return <ClipboardCheck className="w-6 h-6 text-indigo-500 mr-2" />;
    return <Truck className="w-6 h-6 text-green-500 mr-2" />;
  };

  const getDocTitle = () => {
    if (normalizedType === 'SALE') return 'Sales Tax Invoice';
    if (normalizedType === 'ADJ' || normalizedType === 'ADJUSTMENT') return 'Stock Adjustment Note';
    if (normalizedType === 'STOCK_TAKE') return 'Stock Take Note';
    return 'Goods Receipt Note (GRN)';
  };

  return (
    <div className="fixed inset-0 z-50 overflow-y-auto bg-black bg-opacity-50 flex items-center justify-center p-4">
      <div className="bg-white rounded-2xl shadow-2xl w-full max-w-2xl transform transition-all overflow-hidden flex flex-col max-h-[90vh]">
        {/* Header */}
        <div className="flex justify-between items-center px-6 py-4 border-b border-slate-100 bg-slate-50">
          <div className="flex items-center">
            {getIcon()}
            <div>
              <h3 className="text-lg font-black text-slate-800">{getDocTitle()}</h3>
              <p className="text-xs text-slate-500 font-medium">Ref ID: {docId}</p>
            </div>
          </div>
          <button 
            onClick={onClose}
            className="p-1.5 rounded-full hover:bg-slate-200 text-slate-400 hover:text-slate-600 transition"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Content */}
        <div className="flex-1 overflow-y-auto p-6">
          {loading && (
            <div className="flex flex-col items-center justify-center py-16 space-y-3">
              <div className="w-10 h-10 border-4 border-slate-200 border-t-blue-600 rounded-full animate-spin"></div>
              <div className="text-sm font-semibold text-slate-500">Retrieving document details...</div>
            </div>
          )}

          {error && (
            <div className="bg-red-50 text-red-700 p-4 rounded-xl border border-red-100 flex items-start space-x-3">
              <ShieldAlert className="w-5 h-5 text-red-500 shrink-0 mt-0.5" />
              <div>
                <h5 className="font-bold text-sm">Failed to retrieve document</h5>
                <p className="text-xs mt-1 text-red-600">{error}</p>
              </div>
            </div>
          )}

          {!loading && !error && (
            <>
              {normalizedType === 'SALE' && renderSalesInvoice()}
              {(normalizedType === 'ADJ' || normalizedType === 'ADJUSTMENT') && renderStockAdjustment()}
              {normalizedType === 'STOCK_TAKE' && renderStockTake()}
              {normalizedType === 'GRN' && renderGRN()}
            </>
          )}
        </div>

        {/* Footer */}
        <div className="px-6 py-4 border-t border-slate-100 flex justify-end bg-slate-50">
          <button
            onClick={onClose}
            className="px-5 py-2 bg-slate-200 text-slate-700 font-bold rounded-lg hover:bg-slate-300 transition text-sm"
          >
            Close Preview
          </button>
        </div>
      </div>
    </div>
  );
};
