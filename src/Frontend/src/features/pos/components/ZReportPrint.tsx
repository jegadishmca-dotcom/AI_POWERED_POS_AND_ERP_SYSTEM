import React, { forwardRef } from 'react';

interface ZReportData {
  terminalId: string;
  businessDate: string;
  totalInvoices: number;
  totalSales: number;
  totalTax: number;
  totalDiscount: number;
  cashCollected: number;
  cardCollected: number;
  upiCollected: number;
}

interface ZReportPrintProps {
  report: ZReportData;
  openingCash: number;
  declaredCash: number;
  cashierName: string;
}

export const ZReportPrint = forwardRef<HTMLDivElement, ZReportPrintProps>(
  ({ report, openingCash, declaredCash, cashierName }, ref) => {
    
    // Calculations
    const expectedCash = openingCash + report.cashCollected;
    const cashDifference = declaredCash - expectedCash;

    return (
      <div ref={ref} className="bg-white text-black p-4 text-[12px] leading-tight font-mono w-[300px] mx-auto print:p-0 print:m-0 print:w-full">
        <div className="text-center mb-4">
          <h1 className="text-lg font-bold">ஆப்பிள் சூப்பர் மார்க்கெட்</h1>
          <h2 className="text-md font-bold">Apple Super Market</h2>
          <p>1E-16, Matha Kovil Street,</p>
          <p>Ilayankudi - 630702</p>
          <p>Ph: 7339056767 / 04564-221190</p>
          <h3 className="text-base font-bold mt-2 border-y border-dashed border-black py-1">Z-REPORT (END OF SHIFT)</h3>
        </div>

        <div className="mb-4 space-y-1">
          <div className="flex justify-between">
            <span>Date:</span>
            <span>{new Date(report.businessDate).toLocaleDateString('en-GB')}</span>
          </div>
          <div className="flex justify-between">
            <span>Time:</span>
            <span>{new Date().toLocaleTimeString('en-US', { hour: '2-digit', minute: '2-digit' })}</span>
          </div>
          <div className="flex justify-between">
            <span>Terminal:</span>
            <span>{report.terminalId.substring(0,8).toUpperCase()}</span>
          </div>
          <div className="flex justify-between">
            <span>Cashier:</span>
            <span>{cashierName}</span>
          </div>
        </div>

        <div className="border-t border-dashed border-black pt-2 mb-4 space-y-1">
          <h4 className="font-bold mb-1">SALES SUMMARY</h4>
          <div className="flex justify-between">
            <span>Total Invoices:</span>
            <span>{report.totalInvoices}</span>
          </div>
          <div className="flex justify-between font-bold text-sm">
            <span>Gross Sales:</span>
            <span>₹ {report.totalSales.toFixed(2)}</span>
          </div>
          <div className="flex justify-between">
            <span>Total Tax:</span>
            <span>₹ {report.totalTax.toFixed(2)}</span>
          </div>
          <div className="flex justify-between">
            <span>Total Discount:</span>
            <span>₹ {report.totalDiscount.toFixed(2)}</span>
          </div>
        </div>

        <div className="border-t border-dashed border-black pt-2 mb-4 space-y-1">
          <h4 className="font-bold mb-1">TENDER SUMMARY</h4>
          <div className="flex justify-between">
            <span>Cash Sales:</span>
            <span>₹ {report.cashCollected.toFixed(2)}</span>
          </div>
          <div className="flex justify-between">
            <span>UPI Sales:</span>
            <span>₹ {report.upiCollected.toFixed(2)}</span>
          </div>
          <div className="flex justify-between">
            <span>Card Sales:</span>
            <span>₹ {report.cardCollected.toFixed(2)}</span>
          </div>
        </div>

        <div className="border-y border-dashed border-black py-2 mb-4 space-y-1">
          <h4 className="font-bold mb-1">CASH RECONCILIATION</h4>
          <div className="flex justify-between">
            <span>Opening Float:</span>
            <span>₹ {openingCash.toFixed(2)}</span>
          </div>
          <div className="flex justify-between">
            <span>(+) Cash Sales:</span>
            <span>₹ {report.cashCollected.toFixed(2)}</span>
          </div>
          <div className="flex justify-between font-bold border-t border-dashed border-black pt-1 mt-1">
            <span>Expected Cash:</span>
            <span>₹ {expectedCash.toFixed(2)}</span>
          </div>
          <div className="flex justify-between">
            <span>Declared Cash:</span>
            <span>₹ {declaredCash.toFixed(2)}</span>
          </div>
          <div className={`flex justify-between font-bold pt-1 mt-1 ${cashDifference < 0 ? 'text-black' : ''}`}>
            <span>Difference:</span>
            <span>{cashDifference > 0 ? '+' : ''}₹ {cashDifference.toFixed(2)}</span>
          </div>
        </div>

        <div className="text-center mt-6 text-[10px]">
          <p>*** END OF REPORT ***</p>
          <p className="mt-4 border-t border-solid border-black pt-1 mx-8">Cashier Signature</p>
        </div>
      </div>
    );
  }
);
ZReportPrint.displayName = 'ZReportPrint';
