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

  const html = `<!DOCTYPE html>
<html lang="ta">
<head>
  <meta charset="UTF-8"/>
  <meta name="viewport" content="width=device-width,initial-scale=1"/>
  <title>Z-Report - ${terminalCode}</title>
  <style>
    * { margin: 0; padding: 0; box-sizing: border-box; }
    body { font-family: monospace; font-size: 11px; width: 80mm; color: #000; background: #fff; padding: 0 3mm; line-height: 1.2; }
    @media print { body { width: 80mm; } }
    .center { text-align: center; }
    .bold { font-weight: bold; }
    .title-ta { font-size: 14px; font-weight: bold; margin-top: 6px; }
    .title-en { font-size: 12px; font-weight: bold; }
  </style>
</head>
<body>
  <div class="center">
    <div class="title-ta">${STORE.nameTamil}</div>
    <div class="title-en">${STORE.nameEn}</div>
    <div style="margin-top:3px;">${STORE.address}</div>
    <div>${STORE.city}</div>
    <div>Ph: ${STORE.phone}</div>
    <div style="margin-top:2px;">GSTIN: ${STORE.gstin}</div>
    <div>FSSAI: ${STORE.fssai}</div>
    <div class="bold" style="margin-top:6px;">*** Z-REPORT ***</div>
  </div>
  ${hr()}
  ${row('Date:', dateStr)}
  <div style="display:flex;justify-content:space-between;"><span>Time: ${timeStr}</span><span>Terminal: ${terminalCode}</span></div>
  <div>Cashier: ${cashierName}</div>
  ${hr()}
  
  <div class="bold" style="margin-bottom:2px;">SALES SUMMARY</div>
  ${row('Total Invoices:', String(report.totalInvoices))}
  ${row('Gross Sales:', fmt(report.totalSales), true)}
  ${row('Total Tax:', fmt(report.totalTax))}
  ${row('Total Discount:', fmt(report.totalDiscount))}
  ${hr()}

  <div class="bold" style="margin-bottom:2px;">TENDER SUMMARY</div>
  ${row('Cash Sales:', fmt(report.cashCollected))}
  ${row('UPI Sales:', fmt(report.upiCollected))}
  ${row('Card Sales:', fmt(report.cardCollected))}
  ${hr()}

  <div class="bold" style="margin-bottom:2px;">CASH RECONCILIATION</div>
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
  <div style="height:20mm;"></div>
  <script>
    window.onload = () => { window.print(); setTimeout(() => window.close(), 1000); };
  </script>
</body>
</html>`;

  const win = window.open('', '_blank', 'width=400,height=600,scrollbars=yes');
  if (win) {
    win.document.open();
    win.document.write(html);
    win.document.close();
  } else {
    alert('Please allow popups for this site to print reports.');
  }
}
