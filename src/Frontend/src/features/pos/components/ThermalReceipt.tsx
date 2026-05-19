import React from 'react';
import { Invoice } from '../types';

// Used for printing via CSS media queries (@media print)
export const ThermalReceipt = React.forwardRef<HTMLDivElement, { invoice: any }>(({ invoice }, ref) => {
  if (!invoice) return null;
  return (
    <div ref={ref} className="hidden print:block print-receipt-container w-[80mm] text-black font-mono text-xs bg-white p-2">
      <div className="text-center font-bold text-lg mb-2">ENTERPRISE SUPERMARKET</div>
      <div className="text-center mb-4">Tax Invoice</div>
      
      <div className="mb-2">Invoice No: {invoice.invoiceNumber}</div>
      <div className="mb-4">Date: {new Date(invoice.businessDate).toLocaleString()}</div>
      
      <table className="w-full text-left mb-4">
        <thead>
          <tr className="border-b border-black">
            <th>Item</th>
            <th className="text-right">Qty</th>
            <th className="text-right">Amt</th>
          </tr>
        </thead>
        <tbody>
          {invoice.items.map((item, idx) => (
            <tr key={idx}>
              <td>{item.name}</td>
              <td className="text-right">{item.quantity}</td>
              <td className="text-right">{item.totalAmount.toFixed(2)}</td>
            </tr>
          ))}
        </tbody>
      </table>
      
      <div className="border-t border-black pt-2 mb-4">
        <div className="flex justify-between font-bold">
          <span>Net Payable:</span>
          <span>{invoice.netPayable.toFixed(2)}</span>
        </div>
      </div>
      
      <div className="text-center mt-8">Thank you for shopping!</div>
    </div>
  );
});
