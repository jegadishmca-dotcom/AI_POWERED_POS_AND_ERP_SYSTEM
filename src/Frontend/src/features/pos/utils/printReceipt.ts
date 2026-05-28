import { api } from '@/utils/api';

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

function generateReceiptText(invoice: any, terminalCode: string): string {
  const rounded   = Math.round(safe(invoice.totalAmount));
  const roundOff  = +(rounded - safe(invoice.totalAmount)).toFixed(2);
  const cashAmt   = safe(invoice.cashAmount);
  const upiAmt    = safe(invoice.upiAmount);
  const cardAmt   = safe(invoice.cardAmount);
  const walletAmt = safe(invoice.walletAmountUsed || invoice.walletAmount);
  const tendered  = cashAmt + upiAmt + cardAmt + walletAmt;
  const change    = Math.max(0, tendered - rounded);

  const dateStr  = invoice.businessDate ? new Date(invoice.businessDate).toLocaleDateString('en-IN') : '-';
  const timeStr  = invoice.businessDate ? new Date(invoice.businessDate).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }) : '-';

  let sb = "";
  sb += "         ஆப்பிள் சூப்பர் மார்க்கெட்\n";
  sb += "            Apple Super Market\n";
  sb += `       ${STORE.address}\n`;
  sb += `          ${STORE.city}\n`;
  sb += `      Ph: ${STORE.phone}\n`;
  sb += `          GSTIN: ${STORE.gstin}\n`;
  sb += `          FSSAI: ${STORE.fssai}\n`;
  sb += "               TAX INVOICE\n";
  sb += "----------------------------------------\n";
  sb += `Bill No: ${invoice.invoiceNumber || '-'}\n`;
  sb += `Date: ${dateStr}  Time: ${timeStr}\n`;
  sb += `Cashier: ${(invoice.cashierName || 'Cashier').padEnd(15, ' ')} Term: ${terminalCode}\n`;
  if (invoice.customerName) {
    sb += `Customer: ${invoice.customerName} | ${invoice.customerPhone || ''}\n`;
  }
  sb += "----------------------------------------\n";
  sb += "Item                     Qty  Rate   Amt\n";
  sb += "----------------------------------------\n";

  const taxSlabs: Record<string, { taxable: number; cgst: number; sgst: number }> = {};

  (invoice.items || []).forEach((item: any) => {
    const qty  = safe(item.quantity, safe(item.qty));
    const disc = safe(item.discountAmount);
    const lineAmt = safe(item.unitPrice) * qty - disc;

    let name = item.name || item.productName || '-';
    if (name.length > 20) name = name.substring(0, 19) + ".";

    const qtyStr = qty.toString();
    const rateStr = fmt(item.unitPrice);
    const amtStr = fmt(lineAmt);

    sb += `${name.padEnd(20, ' ')} ${qtyStr.padStart(3, ' ')} ${rateStr.padStart(6, ' ')} ${amtStr.padStart(7, ' ')}\n`;
    if (disc > 0) {
      sb += `  Discount: -${fmt(disc)}\n`;
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

  sb += "----------------------------------------\n";
  sb += `Sub Total:                  ${fmt(invoice.subTotal || invoice.totalAmount).padStart(12, ' ')}\n`;
  if (safe(invoice.discountAmount) > 0) {
    sb += `Discount:                  -${fmt(invoice.discountAmount).padStart(12, ' ')}\n`;
  }
  if (safe(invoice.taxAmount) > 0) {
    sb += `GST (CGST+SGST):           +${fmt(invoice.taxAmount).padStart(12, ' ')}\n`;
  }
  if (roundOff !== 0) {
    const sign = roundOff > 0 ? '+' : '';
    sb += `Round Off:                 ${sign}${fmt(roundOff).padStart(12, ' ')}\n`;
  }
  sb += "----------------------------------------\n";
  sb += `NET PAYABLE:               INR ${fmt(rounded).padStart(8, ' ')}\n`;
  sb += "----------------------------------------\n";

  if (cashAmt > 0)   sb += `Cash Tendered:              ${fmt(cashAmt).padStart(12, ' ')}\n`;
  if (upiAmt > 0)    sb += `UPI Paid:                   ${fmt(upiAmt).padStart(12, ' ')}\n`;
  if (cardAmt > 0)   sb += `Card Paid:                  ${fmt(cardAmt).padStart(12, ' ')}\n`;
  if (walletAmt > 0) sb += `Wallet Paid:                ${fmt(walletAmt).padStart(12, ' ')}\n`;
  if (change > 0)    sb += `Change Due:                 ${fmt(change).padStart(12, ' ')}\n`;

  sb += "----------------------------------------\n";
  sb += "GST Summary:\n";
  sb += "Slab      Taxable       CGST        SGST\n";

  const hasAnyTax = Object.keys(taxSlabs).length > 0;
  if (hasAnyTax) {
    Object.entries(taxSlabs).forEach(([key, s]) => {
      sb += `${key.padEnd(9, ' ')} ${fmt(s.taxable).padStart(10, ' ')} ${fmt(s.cgst).padStart(10, ' ')} ${fmt(s.sgst).padStart(10, ' ')}\n`;
    });
  } else {
    sb += "All items: Nil Rated / Exempt\n";
  }

  if (invoice.customerName) {
    sb += "----------------------------------------\n";
    const earned  = safe(invoice.loyaltyPointsEarned);
    const balance = safe(invoice.loyaltyPointsBalance);
    const oldPts  = Math.max(0, balance - earned);
    sb += `OLD POINTS : ${oldPts.toFixed(2).padEnd(10, ' ')} CASH RECEIVED : ${fmt(tendered)}\n`;
    sb += `TODAY PTS  : ${earned.toFixed(2).padEnd(10, ' ')} REFUND        : ${fmt(change)}\n`;
    sb += `TOTAL PTS  : ${balance.toFixed(2).padEnd(10, ' ')}\n`;
  }

  sb += "----------------------------------------\n";
  sb += "    அனைத்தும் வாங்க ஆப்பிளுக்கு வாங்க\n";
  sb += "   Thank you for shopping with us!\n";
  sb += "             Visit Again!\n";
  sb += "\n\n\n\n";

  return sb;
}

function triggerSystemPrint(invoice: any, terminalCode: string) {
  const rounded   = Math.round(safe(invoice.totalAmount));
  const roundOff  = +(rounded - safe(invoice.totalAmount)).toFixed(2);
  const cashAmt   = safe(invoice.cashAmount);
  const upiAmt    = safe(invoice.upiAmount);
  const cardAmt   = safe(invoice.cardAmount);
  const walletAmt = safe(invoice.walletAmountUsed || invoice.walletAmount);
  const tendered  = cashAmt + upiAmt + cardAmt + walletAmt;
  const change    = Math.max(0, tendered - rounded);

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
        <span style="flex:1;">${item.name || item.productName || '-'}</span>
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
  const hasAnyTax = Object.keys(taxSlabs).length > 0;
  gstHtml = hr() + `<div style="font-weight:bold;font-size:9px;margin-bottom:2px;">GST Summary</div>`;
  if (hasAnyTax) {
    gstHtml += `<div style="display:flex;font-weight:bold;font-size:9px;border-bottom:1px dashed #000;padding-bottom:1px;">
      <span style="width:42px;">Slab</span>
      <span style="flex:1;text-align:right;">Taxable</span>
      <span style="flex:1;text-align:right;">CGST</span>
      <span style="flex:1;text-align:right;">SGST</span>
    </div>`;
    let totalCgst = 0, totalSgst = 0, totalTaxable = 0;
    Object.entries(taxSlabs).forEach(([key, s]) => {
      gstHtml += `<div style="display:flex;font-size:9px;">
        <span style="width:42px;">${key}</span>
        <span style="flex:1;text-align:right;">${fmt(s.taxable)}</span>
        <span style="flex:1;text-align:right;">${fmt(s.cgst)}</span>
        <span style="flex:1;text-align:right;">${fmt(s.sgst)}</span>
      </div>`;
      totalCgst += s.cgst; totalSgst += s.sgst; totalTaxable += s.taxable;
    });
    gstHtml += `<div style="display:flex;font-size:9px;font-weight:bold;border-top:1px dashed #000;padding-top:1px;">
      <span style="width:42px;">Total</span>
      <span style="flex:1;text-align:right;">${fmt(totalTaxable)}</span>
      <span style="flex:1;text-align:right;">${fmt(totalCgst)}</span>
      <span style="flex:1;text-align:right;">${fmt(totalSgst)}</span>
    </div>`;
  } else {
    gstHtml += `<div style="font-size:9px;font-style:italic;">All items: Nil Rated / Exempt (GST \u20b90.00)</div>`;
  }

  let loyaltyHtml = '';
  if (invoice.customerName) {
    const earned  = safe(invoice.loyaltyPointsEarned);
    const balance = safe(invoice.loyaltyPointsBalance);
    const oldPts  = Math.max(0, balance - earned);
    const rcvd    = tendered;
    const rfnd    = change;

    const earnedStr  = earned.toFixed(2);
    const balanceStr = balance.toFixed(2);
    const oldPtsStr  = oldPts.toFixed(2);
    const rcvdStr    = fmt(rcvd);
    const rfndStr    = fmt(rfnd);

    const maxValLeftLen = Math.max(oldPtsStr.length, earnedStr.length, balanceStr.length);
    const oldPtsVal  = oldPtsStr.padStart(maxValLeftLen, ' ');
    const earnedVal  = earnedStr.padStart(maxValLeftLen, ' ');
    const balanceVal = balanceStr.padStart(maxValLeftLen, ' ');

    const maxValRightLen = Math.max(rcvdStr.length, rfndStr.length);
    const rcvdVal    = rcvdStr.padStart(maxValRightLen, ' ');
    const rfndVal    = rfndStr.padStart(maxValRightLen, ' ');

    const leftColWidth = 10;
    const rightColWidth = 13;

    const oldPtsLine  = `${'OLD POINTS'.padEnd(leftColWidth, ' ')} : ${oldPtsVal}`;
    const todayPtsLine = `${'TODAY PTS'.padEnd(leftColWidth, ' ')} : ${earnedVal}`;
    const totalPtsLine = `${'TOTAL PTS'.padEnd(leftColWidth, ' ')} : ${balanceVal}`;

    const cashRcvdLine = `${'CASH RECEIVED'.padEnd(rightColWidth, ' ')} : ${rcvdVal}`;
    const refundLine   = `${'REFUND'.padEnd(rightColWidth, ' ')} : ${rfndVal}`;

    loyaltyHtml = hr() + `
      <div style="font-size:10px; font-family:monospace; line-height:1.2;">
        <div style="display:flex;justify-content:space-between;">
          <span style="white-space:pre;">${oldPtsLine}</span>
          <span style="white-space:pre;">${cashRcvdLine}</span>
        </div>
        <div style="display:flex;justify-content:space-between;">
          <span style="white-space:pre;">${todayPtsLine}</span>
          <span style="white-space:pre;">${refundLine}</span>
        </div>
        <div><span style="white-space:pre;">${totalPtsLine}</span></div>
      </div>`;
  }

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
  ${gstHtml}
  ${loyaltyHtml}
  <div style="text-align:center;margin-top:8px;padding-top:6px;border-top:1px dashed #000;padding-bottom:4px;">
    <div>Tax Invoice | GSTIN: ${STORE.gstin}</div>
    <div style="margin-top:4px;">${STORE.nameTamil}</div>
    <div>அனைத்தும் வாங்க ஆப்பிளுக்கு வாங்க</div>
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

export async function printReceipt(invoice: any): Promise<void> {
  if (!invoice) return;

  const terminalCode = (() => { try { return localStorage.getItem('pos_terminal_code') || 'POS-01'; } catch { return 'POS-01'; } })();

  const savedConfig = localStorage.getItem('pos_printer_config');
  let config: any = { receiptMode: 'system', receiptIp: '', receiptBaudRate: 9600 };
  if (savedConfig) {
    try {
      config = JSON.parse(savedConfig);
    } catch (e) {
      console.error('Failed to parse printer config:', e);
    }
  }

  if (config.receiptMode === 'usb') {
    if (!('serial' in navigator)) {
      alert('Web Serial API is not supported in this browser. Please use Chrome.');
      triggerSystemPrint(invoice, terminalCode);
      return;
    }
    try {
      let port;
      // @ts-ignore
      const ports = await navigator.serial.getPorts();
      if (ports && ports.length > 0) {
        port = ports[0];
      } else {
        // @ts-ignore
        port = await navigator.serial.requestPort();
      }
      await port.open({ baudRate: config.receiptBaudRate || 9600 });
      const writer = port.writable.getWriter();
      const encoder = new TextEncoder();
      
      const textContent = generateReceiptText(invoice, terminalCode);
      
      // Init ESC/POS
      await writer.write(new Uint8Array([0x1B, 0x40]));
      // Write text
      await writer.write(encoder.encode(textContent));
      // Cut paper
      await writer.write(new Uint8Array([0x0A, 0x0A, 0x0A, 0x0A, 0x1D, 0x56, 0x00]));
      
      writer.releaseLock();
      await port.close();
    } catch (err: any) {
      console.error('USB print failed:', err);
      alert('USB print failed: ' + (err.message || err) + '. Falling back to system print.');
      triggerSystemPrint(invoice, terminalCode);
    }
  } else if (config.receiptMode === 'network') {
    try {
      const printerIp = config.receiptIp || '192.168.1.100';
      await api.post(`/api/pos/print/${invoice.id || invoice.Id}?printerIp=${encodeURIComponent(printerIp)}`);
    } catch (err: any) {
      console.error('Network print failed:', err);
      alert('Network print failed. Falling back to system print.');
      triggerSystemPrint(invoice, terminalCode);
    }
  } else {
    triggerSystemPrint(invoice, terminalCode);
  }
}
