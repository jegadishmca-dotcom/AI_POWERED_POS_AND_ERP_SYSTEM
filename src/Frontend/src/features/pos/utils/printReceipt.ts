// printReceipt.ts
// Opens a dedicated popup window with pure HTML receipt and auto-prints it.
// This is the standard approach used by Shopify POS, Square, and Lightspeed.

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

export function printReceipt(invoice: any): void {
  if (!invoice) return;

  const rounded   = Math.round(safe(invoice.totalAmount));
  const roundOff  = +(rounded - safe(invoice.totalAmount)).toFixed(2);
  const cashAmt   = safe(invoice.cashAmount);
  const upiAmt    = safe(invoice.upiAmount);
  const cardAmt   = safe(invoice.cardAmount);
  const walletAmt = safe(invoice.walletAmountUsed);
  const tendered  = cashAmt + upiAmt + cardAmt + walletAmt;
  const change    = Math.max(0, tendered - rounded);

  const terminalCode = (() => { try { return localStorage.getItem('pos_terminal_code') || 'POS-01'; } catch { return 'POS-01'; } })();
  const dateStr  = invoice.businessDate ? new Date(invoice.businessDate).toLocaleDateString('en-IN') : '-';
  const timeStr  = invoice.businessDate ? new Date(invoice.businessDate).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }) : '-';

  // Build items HTML
  let itemsHtml = `
    <div style="display:flex;font-weight:bold;border-bottom:1px dashed #000;padding-bottom:2px;margin-bottom:2px;">
      <span style="flex:1;">Item</span>
      <span style="width:24px;text-align:center;">Qty</span>
      <span style="width:52px;text-align:right;">Rate</span>
      <span style="width:54px;text-align:right;">Amt</span>
    </div>`;

  // GST slab summary
  const taxSlabs: Record<string, { taxable: number; cgst: number; sgst: number }> = {};

  (invoice.items || []).forEach((item: any) => {
    const qty  = safe(item.quantity, safe(item.qty));
    const disc = safe(item.discountAmount);
    const lineAmt = safe(item.unitPrice) * qty - disc;
    itemsHtml += `
      <div style="display:flex;margin-bottom:1px;">
        <span style="flex:1;">${item.name || '-'}</span>
        <span style="width:24px;text-align:center;">${qty}</span>
        <span style="width:52px;text-align:right;">${fmt(item.unitPrice)}</span>
        <span style="width:54px;text-align:right;">${fmt(lineAmt)}</span>
      </div>`;
    if (disc > 0) {
      itemsHtml += `<div style="font-size:9px;padding-left:8px;color:#2d6a2d;">Discount: -${fmt(disc)}</div>`;
    }

    const cgstRate = safe(item.cgstRate);
    const sgstRate = safe(item.sgstRate);
    const totalRate = cgstRate + sgstRate;
    if (totalRate > 0) {
      const key = `GST ${totalRate}%`;
      if (!taxSlabs[key]) taxSlabs[key] = { taxable: 0, cgst: 0, sgst: 0 };
      taxSlabs[key].taxable += lineAmt;
      taxSlabs[key].cgst    += lineAmt * (cgstRate / 100);
      taxSlabs[key].sgst    += lineAmt * (sgstRate / 100);
    }
  });

  // Payment rows
  let paymentHtml = '';
  if (cashAmt > 0)   paymentHtml += row('Cash Tendered', fmt(cashAmt));
  if (upiAmt > 0)    paymentHtml += row('UPI Paid', fmt(upiAmt));
  if (cardAmt > 0)   paymentHtml += row('Card Paid', fmt(cardAmt));
  if (walletAmt > 0) paymentHtml += row('Wallet Paid', fmt(walletAmt));
  if (!cashAmt && !upiAmt && !cardAmt && !walletAmt)
    paymentHtml += row('Paid via', invoice.paymentMode || 'CASH');
  if (change > 0) paymentHtml += row('Change Due', fmt(change), true);

  // GST summary table
  let gstHtml = '';
  if (Object.keys(taxSlabs).length > 0) {
    gstHtml = hr() + `<div style="font-weight:bold;font-size:9px;margin-bottom:2px;">GST Summary</div>
      <div style="display:flex;font-weight:bold;font-size:9px;border-bottom:1px dashed #000;padding-bottom:1px;">
        <span style="width:42px;">Slab</span>
        <span style="flex:1;text-align:right;">Taxable</span>
        <span style="flex:1;text-align:right;">CGST</span>
        <span style="flex:1;text-align:right;">SGST</span>
      </div>`;
    Object.entries(taxSlabs).forEach(([key, s]) => {
      gstHtml += `<div style="display:flex;font-size:9px;">
        <span style="width:42px;">${key}</span>
        <span style="flex:1;text-align:right;">${fmt(s.taxable)}</span>
        <span style="flex:1;text-align:right;">${fmt(s.cgst)}</span>
        <span style="flex:1;text-align:right;">${fmt(s.sgst)}</span>
      </div>`;
    });
  }

  // Loyalty
  let loyaltyHtml = '';
  if (invoice.customerName && (safe(invoice.loyaltyPointsEarned) > 0 || safe(invoice.loyaltyPointsBalance) > 0)) {
    loyaltyHtml = hr();
    if (safe(invoice.loyaltyPointsEarned) > 0)
      loyaltyHtml += row('Points Earned Today', `+${fmt(invoice.loyaltyPointsEarned)}`);
    if (safe(invoice.loyaltyPointsBalance) > 0)
      loyaltyHtml += row('Total Points Balance', fmt(invoice.loyaltyPointsBalance));
  }

  // Full receipt HTML
  const html = `<!DOCTYPE html>
<html lang="ta">
<head>
  <meta charset="UTF-8"/>
  <meta name="viewport" content="width=device-width,initial-scale=1"/>
  <title>Receipt - ${invoice.invoiceNumber || ''}</title>
  <style>
    * { margin: 0; padding: 0; box-sizing: border-box; }
    body { font-family: monospace; font-size: 11px; width: 80mm; color: #000; background: #fff; padding: 0 3mm; }
    @media print { body { width: 80mm; } }
  </style>
</head>
<body>
  <div style="text-align:center;font-weight:bold;font-size:14px;margin-top:6px;">${STORE.nameTamil}</div>
  <div style="text-align:center;font-weight:bold;font-size:12px;">${STORE.nameEn}</div>
  <div style="text-align:center;margin-top:3px;">${STORE.address}</div>
  <div style="text-align:center;">${STORE.city}</div>
  <div style="text-align:center;">Ph: ${STORE.phone}</div>
  <div style="text-align:center;margin-top:2px;">GSTIN: ${STORE.gstin}</div>
  <div style="text-align:center;">FSSAI: ${STORE.fssai}</div>
  <div style="text-align:center;font-weight:bold;margin-top:3px;">TAX INVOICE</div>
  ${hr()}
  ${row('Bill No: ' + (invoice.invoiceNumber || '-'), '')}
  <div style="display:flex;justify-content:space-between;"><span>Date: ${dateStr}</span><span>Time: ${timeStr}</span></div>
  <div style="display:flex;justify-content:space-between;"><span>Cashier: ${invoice.cashierName || 'Cashier'}</span><span>Terminal: ${terminalCode}</span></div>
  ${invoice.customerName ? `<div>Customer: ${invoice.customerName}${invoice.customerPhone ? ' | ' + invoice.customerPhone : ''}</div>` : ''}
  ${hr()}
  ${itemsHtml}
  ${hr()}
  ${row('Sub Total', fmt(invoice.subTotal || invoice.totalAmount))}
  ${safe(invoice.discountAmount) > 0 ? row('Discount', '-' + fmt(invoice.discountAmount)) : ''}
  ${safe(invoice.taxAmount) > 0 ? row('GST (CGST+SGST)', '+' + fmt(invoice.taxAmount)) : ''}
  ${roundOff !== 0 ? row('Round Off', (roundOff > 0 ? '+' : '') + fmt(roundOff)) : ''}
  <div style="display:flex;justify-content:space-between;font-weight:bold;font-size:14px;border-top:1px dashed #000;padding-top:4px;margin-top:4px;">
    <span>NET PAYABLE</span><span>&#8377; ${rounded}</span>
  </div>
  ${hr()}
  ${paymentHtml}
  ${loyaltyHtml}
  ${gstHtml}
  <div style="text-align:center;margin-top:8px;padding-top:6px;border-top:1px dashed #000;padding-bottom:4px;">
    <div>Tax Invoice | GSTIN: ${STORE.gstin}</div>
    <div style="margin-top:4px;">${STORE.nameTamil}</div>
    <div>அனைத்தும் வாங்க நன்றிபிளுக்கு வாங்க</div>
    <div>Thank you for shopping with us!</div>
    <div>Visit Again!</div>
  </div>
  <div style="height:20mm;"></div>
  <script>
    window.onload = function() {
      window.print();
      setTimeout(function() { window.close(); }, 1000);
    };
  </script>
</body>
</html>`;

  const printWindow = window.open('', '_blank', 'width=400,height=600,scrollbars=yes');
  if (printWindow) {
    printWindow.document.open();
    printWindow.document.write(html);
    printWindow.document.close();
  } else {
    alert('Please allow popups for this site to print receipts.');
  }
}
