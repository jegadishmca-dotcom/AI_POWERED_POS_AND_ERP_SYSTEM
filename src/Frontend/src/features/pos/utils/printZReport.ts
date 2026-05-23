const STORE = {
  nameTamil: 'ஆப்பிள் சூப்பர் மார்க்கெட்',
  nameEn:    'Apple Super Market',
  gstin:     '33ABTFA7190F1Z7',
  fssai:     '12421019000047',
  address:   '1E-16, Matha Kovil Street,',
  city:      'Ilayankudi - 630702',
  phone:     '7339056767 / 04564-221190',
};

const safe  = (n: any, d = 0): number => (typeof n === 'number' && !isNaN(n) ? n : d);
const fmt   = (n: any): string => safe(n).toFixed(2);
const row   = (l: string, r: string, bold = false) =>
  `<div style="display:flex;justify-content:space-between;${bold ? 'font-weight:bold;font-size:13px;' : ''}">`+
  `<span>${l}</span><span>${r}</span></div>`;
const hr    = () => `<hr style="border:none;border-top:1px dashed #000;margin:4px 0;"/>`;

export function printZReport(report: any, openingCash: number, declaredCash: number, cashierName: string): void {
  if (!report) return;

  const expectedCash = openingCash + safe(report.cashCollected);
  const diff = declaredCash - expectedCash;
  const terminalCode = (() => { try { return localStorage.getItem('pos_terminal_code') || 'POS-01'; } catch { return 'POS-01'; } })();
  
  const dateStr  = report.businessDate ? new Date(report.businessDate).toLocaleDateString('en-IN') : '-';
  const timeStr  = new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });

  const html = `
    <html>
      <head>
        <title>Z-Report ${terminalCode}</title>
        <style>
          @import url('https://fonts.googleapis.com/css2?family=Courier+Prime:wght@400;700&display=swap');
          body { 
            font-family: 'Courier Prime', monospace; 
            font-size: 12px; 
            color: #000; 
            margin: 0; 
            padding: 0; 
            line-height: 1.2;
          }
          .receipt { width: 300px; padding: 10px; margin: 0 auto; }
          .center { text-align: center; }
          .bold { font-weight: bold; }
          .title-ta { font-size: 16px; font-weight: bold; margin-bottom: 2px; }
          .title-en { font-size: 14px; font-weight: bold; margin-bottom: 4px; }
          @media print {
            body { margin: 0; padding: 0; }
            .receipt { width: 100%; padding: 0; }
            @page { margin: 0; }
          }
        </style>
      </head>
      <body>
        <div class="receipt">
          <div class="center">
            <div class="title-ta">${STORE.nameTamil}</div>
            <div class="title-en">${STORE.nameEn}</div>
            <div>${STORE.address}</div>
            <div>${STORE.city}</div>
            <div>Ph: ${STORE.phone}</div>
            <div>GSTIN: ${STORE.gstin}</div>
            <div>FSSAI: ${STORE.fssai}</div>
            <div class="bold" style="margin-top:6px;">*** Z-REPORT ***</div>
          </div>
          ${hr()}
          ${row('Date:', dateStr)}${row('Time:', timeStr)}
          ${row('Cashier:', cashierName)}${row('Terminal:', terminalCode)}
          ${hr()}
          
          <div class="bold" style="margin-bottom:4px;">SALES SUMMARY</div>
          ${row('Total Invoices:', String(report.totalInvoices))}
          ${row('Gross Sales:', fmt(report.totalSales), true)}
          ${row('Total Tax:', fmt(report.totalTax))}
          ${row('Total Discount:', fmt(report.totalDiscount))}
          ${hr()}

          <div class="bold" style="margin-bottom:4px;">TENDER SUMMARY</div>
          ${row('Cash Sales:', fmt(report.cashCollected))}
          ${row('UPI Sales:', fmt(report.upiCollected))}
          ${row('Card Sales:', fmt(report.cardCollected))}
          ${hr()}

          <div class="bold" style="margin-bottom:4px;">CASH RECONCILIATION</div>
          ${row('Opening Float:', fmt(openingCash))}
          ${row('(+) Cash Sales:', fmt(report.cashCollected))}
          ${hr()}
          ${row('Expected Cash:', fmt(expectedCash), true)}
          ${row('Declared Cash:', fmt(declaredCash), true)}
          ${hr()}
          ${row('Difference:', (diff > 0 ? '+' : '') + fmt(diff), true)}
          
          <div class="center" style="margin-top:20px;font-size:10px;">
            <p>*** END OF REPORT ***</p>
            <p style="margin-top:30px; border-top:1px solid #000; display:inline-block; padding-top:4px;">Cashier Signature</p>
          </div>
        </div>
        <script>
          window.onload = () => { window.print(); window.close(); };
        </script>
      </body>
    </html>
  `;

  const blob = new Blob([html], { type: 'text/html' });
  const url = URL.createObjectURL(blob);
  const win = window.open(url, '_blank', 'width=320,height=600');
  if (win) {
    win.onload = () => URL.revokeObjectURL(url);
  }
}
