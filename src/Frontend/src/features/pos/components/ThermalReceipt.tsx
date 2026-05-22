import React from 'react';
import { Invoice } from '../types';

// Used for printing via CSS media queries (@media print)
export const ThermalReceipt = React.forwardRef<HTMLDivElement, { invoice: any }>(({ invoice }, ref) => {
  if (!invoice) return null;
  return (
    <div ref={ref} className="hidden print:block print-receipt-container text-black bg-white" style={{ width: '80mm', fontFamily: 'monospace', fontSize: '12px', padding: '0 4mm' }}>
      <div className="text-center font-bold text-lg mb-2 pt-2">ENTERPRISE SUPERMARKET</div>
      <div className="text-center mb-4 border-b border-black pb-2 border-dashed">Tax Invoice</div>
      
      <div className="mb-1 flex justify-between"><span>Inv: {invoice.invoiceNumber}</span></div>
      <div className="mb-4 flex justify-between"><span>Date: {new Date(invoice.businessDate).toLocaleDateString()}</span><span>{new Date(invoice.businessDate).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}</span></div>
      
      <table className="w-full text-left mb-2">
        <thead>
          <tr className="border-b border-black border-dashed">
            <th className="py-1">Item</th>
            <th className="text-right py-1">Qty</th>
            <th className="text-right py-1">Amt</th>
          </tr>
        </thead>
        <tbody>
          {invoice.items.map((item: any, idx: number) => (
            <tr key={idx} className="align-top">
              <td className="py-1 pr-2 break-words">{item.name}</td>
              <td className="text-right py-1">{item.quantity}</td>
              <td className="text-right py-1">{item.totalAmount.toFixed(2)}</td>
            </tr>
          ))}
        </tbody>
      </table>
      
      <div className="border-t border-black border-dashed pt-2 mb-4">
        <div className="flex justify-between font-bold text-[14px]">
          <span>Net Payable:</span>
          <span>₹ {invoice.netPayable.toFixed(2)}</span>
        </div>
        {invoice.paymentMode && (
          <div className="flex justify-between mt-1 text-[11px]">
            <span>Paid via:</span>
            <span>{invoice.paymentMode}</span>
          </div>
        )}
      </div>
      
      <div className="text-center mt-6 mb-8 text-[11px] border-t border-black border-dashed pt-4">
        <p>Thank you for shopping!</p>
        <p>Visit again</p>
      </div>
      
      {/* Spacer for thermal printer auto-cutter */}
      <div style={{ height: '15mm' }}></div>
    </div>
  );
});
