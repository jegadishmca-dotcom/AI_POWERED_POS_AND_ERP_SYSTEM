import { getPosPermissions } from '../../settings/api/settings.api';
import { translateProductNameSync } from '../../../utils/translationEngine';

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

/**
 * Resolves item product name based on global receipt language setting:
 * - 'secondary' (default): Regional (Tamil) name if available, fallback to English.
 * - 'primary': Standard English product name.
 * - 'both': Dual English / Tamil names.
 */
function getItemDisplayName(item: any, langMode: string = 'secondary'): string {
  const primary = item.name || item.productName || '';
  let secondary = item.nameTamil || item.secondaryName || item.tamilName || '';

  // Synchronous dictionary fallback if secondary name is missing on older items
  if (!secondary && primary) {
    const autoTrans = translateProductNameSync(primary, 'ta');
    if (autoTrans && autoTrans !== primary) {
      secondary = autoTrans;
    }
  }

  if (langMode === 'primary') {
    return primary || secondary || '-';
  }
  if (langMode === 'both') {
    return secondary ? `${primary} / ${secondary}` : (primary || '-');
  }
  // Default 'secondary': Regional (Tamil) product name
  return secondary || primary || '-';
}

function generateReceiptText(invoice: any, terminalCode: string, langMode: string = 'secondary'): string {
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

  const taxSlabs: Record<string, { taxable: number; cgst: number; sgst: number; cess: number }> = {};
  let totalCessAmount = 0;
  let totalGstAmount = 0;
  const hasCess = (invoice.items || []).some((item: any) => safe(item.cessRate) > 0 || safe(item.cessAmount) > 0);

  (invoice.items || []).forEach((item: any) => {
    const qty  = safe(item.quantity, safe(item.qty));
    const disc = safe(item.discountAmount);
    const lineAmt = safe(item.unitPrice) * qty - disc;

    let name = getItemDisplayName(item, langMode);
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
    const cessRate = safe(item.cessRate);
    const totalRate = cgstRate + sgstRate + cessRate;
    const itemBaseTaxable = totalRate > 0 ? lineAmt / (1 + totalRate / 100) : lineAmt;

    const cgstAmt = item.cgstAmount !== undefined ? safe(item.cgstAmount) : itemBaseTaxable * (cgstRate / 100);
    const sgstAmt = item.sgstAmount !== undefined ? safe(item.sgstAmount) : itemBaseTaxable * (sgstRate / 100);
    const cessAmt = item.cessAmount !== undefined ? safe(item.cessAmount) : itemBaseTaxable * (cessRate / 100);

    totalCessAmount += cessAmt;
    totalGstAmount += cgstAmt + sgstAmt;

    if ((cgstRate + sgstRate) > 0 || cessRate > 0) {
      const key = `GST ${(cgstRate + sgstRate)}%`;
      if (!taxSlabs[key]) taxSlabs[key] = { taxable: 0, cgst: 0, sgst: 0, cess: 0 };
      taxSlabs[key].taxable += itemBaseTaxable;
      taxSlabs[key].cgst    += cgstAmt;
      taxSlabs[key].sgst    += sgstAmt;
      taxSlabs[key].cess    += cessAmt;
    }
  });

  sb += "----------------------------------------\n";
  sb += `Sub Total:                  ${fmt(invoice.subTotal || invoice.totalAmount).padStart(12, ' ')}\n`;
  if (safe(invoice.discountAmount) > 0) {
    sb += `Discount:                  -${fmt(invoice.discountAmount).padStart(12, ' ')}\n`;
  }
  if (totalGstAmount > 0) {
    sb += `GST (CGST+SGST):           +${fmt(totalGstAmount).padStart(12, ' ')}\n`;
  } else if (safe(invoice.taxAmount) > 0 && !totalCessAmount) {
    sb += `GST (CGST+SGST):           +${fmt(invoice.taxAmount).padStart(12, ' ')}\n`;
  }
  if (totalCessAmount > 0) {
    sb += `GST CESS:                  +${fmt(totalCessAmount).padStart(12, ' ')}\n`;
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

  const hasAnyTax = Object.keys(taxSlabs).length > 0;
  if (hasAnyTax) {
    if (hasCess) {
      sb += "Slab".padEnd(8, ' ') + " " + "Taxable".padStart(7, ' ') + " " + "CGST".padStart(7, ' ') + " " + "SGST".padStart(7, ' ') + " " + "CESS".padStart(7, ' ') + "\n";
      Object.entries(taxSlabs).forEach(([key, s]) => {
        sb += `${key.padEnd(8, ' ')} ${fmt(s.taxable).padStart(7, ' ')} ${fmt(s.cgst).padStart(7, ' ')} ${fmt(s.sgst).padStart(7, ' ')} ${fmt(s.cess).padStart(7, ' ')}\n`;
      });
    } else {
      sb += "Slab      Taxable       CGST        SGST\n";
      Object.entries(taxSlabs).forEach(([key, s]) => {
        sb += `${key.padEnd(9, ' ')} ${fmt(s.taxable).padStart(10, ' ')} ${fmt(s.cgst).padStart(10, ' ')} ${fmt(s.sgst).padStart(10, ' ')}\n`;
      });
    }
  } else {
    sb += "All items: Nil Rated / Exempt\n";
  }

  const earned  = safe(invoice.loyaltyPointsEarned);
  const balance = safe(invoice.loyaltyPointsBalance);
  const oldPts  = Math.max(0, balance - earned);

  sb += "----------------------------------------\n";
  sb += `Payment Method   : ${(invoice.paymentMode || 'CASH').toUpperCase()}\n`;
  sb += `Amount Tendered  : Rs. ${fmt(tendered > 0 ? tendered : rounded)}\n`;
  sb += `Change Returned  : Rs. ${fmt(change)}\n`;

  if (earned > 0 || oldPts > 0 || balance > 0) {
    sb += "----------------------------------------\n";
    sb += "        LOYALTY REWARDS PROGRAM         \n";
    sb += `Opening Balance  : ${oldPts.toFixed(2)} Pts\n`;
    sb += `Points Earned    : +${earned.toFixed(2)} Pts\n`;
    sb += `Closing Balance  : ${balance.toFixed(2)} Pts\n`;
  }

  sb += "----------------------------------------\n";
  sb += "    அனைத்தும் வாங்க ஆப்பிளுக்கு வாங்க\n";
  sb += "   Thank you for shopping with us!\n";
  sb += "             Visit Again!\n";
  sb += "\n\n\n\n";

  return sb;
}

function triggerSystemPrint(invoice: any, terminalCode: string, langMode: string = 'secondary') {
  const rounded   = Math.round(safe(invoice.totalAmount));
  const roundOff  = +(rounded - safe(invoice.totalAmount)).toFixed(2);
  const cashAmt   = safe(invoice.cashAmount);
  const upiAmt    = safe(invoice.upiAmount);
  const cardAmt   = safe(invoice.cardAmount);
  const walletAmt = safe(invoice.walletAmountUsed || invoice.walletAmount);
  const tendered  = cashAmt + upiAmt + cardAmt + walletAmt;
  const change    = Math.max(0, tendered - rounded);

  const dateStr  = invoice.businessDate ? new Date(invoice.businessDate).toLocaleDateString('en-IN') : new Date().toLocaleDateString('en-IN');
  const timeStr  = invoice.businessDate ? new Date(invoice.businessDate).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }) : new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });

  // Build items rows with global ERP standard 1-line formatting
  let itemsRowsHtml = '';
  (invoice.items || []).forEach((item: any, idx: number) => {
    const qty  = safe(item.quantity, safe(item.qty));
    const disc = safe(item.discountAmount);
    const lineAmt = safe(item.unitPrice) * qty - disc;
    const mrp = safe(item.mrp || item.unitPrice);
    const mrpVal = mrp > 0 ? Math.round(mrp).toString() : '-';

    const displayName = getItemDisplayName(item, langMode);

    itemsRowsHtml += `
      <tr style="border-bottom: 1px solid #000;">
        <td style="text-align:center; padding:3px 1px; font-weight:700; font-size:10px; vertical-align:middle; border-right: 1px solid #000;">${idx + 1}</td>
        <td style="text-align:left; padding:3px 3px; font-weight:800; font-size:10px; vertical-align:middle; border-right: 1px solid #000; overflow:hidden; text-overflow:ellipsis; white-space:nowrap;" title="${displayName}">${displayName}</td>
        <td style="text-align:center; padding:3px 1px; font-weight:800; font-size:10px; vertical-align:middle; border-right: 1px solid #000; white-space:nowrap;">${qty}</td>
        <td style="text-align:right; padding:3px 2px; font-weight:700; font-size:10px; vertical-align:middle; border-right: 1px solid #000; white-space:nowrap;">${mrpVal}</td>
        <td style="text-align:right; padding:3px 2px; font-weight:700; font-size:10px; vertical-align:middle; border-right: 1px solid #000; white-space:nowrap;">${fmt(item.unitPrice)}</td>
        <td style="text-align:right; padding:3px 3px; font-weight:900; font-size:10.5px; vertical-align:middle; white-space:nowrap;">${fmt(lineAmt)}</td>
      </tr>`;
  });

  // Customer Loyalty / Points
  const earned  = safe(invoice.loyaltyPointsEarned);
  const balance = safe(invoice.loyaltyPointsBalance);
  const oldPts  = Math.max(0, balance - earned);
  const oldPtsStr  = oldPts.toFixed(2);
  const earnedStr  = earned.toFixed(2);
  const balanceStr = balance.toFixed(2);
  const rcvdStr    = fmt(tendered > 0 ? tendered : rounded);
  const rfndStr    = fmt(change);

  // Modern Payment Mode formatting
  let payModeDisplay = (invoice.paymentMode || 'CASH').toUpperCase();
  if (cashAmt > 0 && (upiAmt > 0 || cardAmt > 0 || walletAmt > 0)) {
    const parts = [];
    if (cashAmt > 0) parts.push('Cash');
    if (upiAmt > 0) parts.push('UPI');
    if (cardAmt > 0) parts.push('Card');
    if (walletAmt > 0) parts.push('Wallet');
    payModeDisplay = `SPLIT (${parts.join('/')})`;
  }

  const html = `<!DOCTYPE html>
<html lang="ta">
<head>
  <meta charset="UTF-8"/>
  <meta name="viewport" content="width=device-width,initial-scale=1"/>
  <title>Receipt - ${invoice.invoiceNumber || ''}</title>
  <style>
    @page {
      size: 80mm auto;
      margin: 0 auto !important;
    }
    @media print {
      html, body {
        width: 100% !important;
        margin: 0 !important;
        padding: 0 !important;
        display: flex !important;
        justify-content: center !important;
        align-items: flex-start !important;
        background: #fff !important;
        color: #000 !important;
        -webkit-print-color-adjust: exact;
      }
      .receipt-container {
        width: 72mm !important;
        margin: 0 auto !important;
        padding: 2mm 1mm !important;
      }
    }
    * {
      box-sizing: border-box;
      margin: 0;
      padding: 0;
      font-family: Arial, "Helvetica Neue", Helvetica, sans-serif;
      color: #000 !important;
    }
    body {
      width: 100%;
      display: flex;
      justify-content: center;
      align-items: flex-start;
      margin: 0 auto;
      padding: 0;
      background: #fff;
      -webkit-font-smoothing: antialiased;
    }
    .receipt-container {
      width: 72mm;
      margin: 0 auto;
      padding: 2mm 1mm;
      font-size: 11px;
      line-height: 1.25;
      background: #fff;
    }
    .text-center { text-align: center; }
    .text-right { text-align: right; }
    .text-left { text-align: left; }
    
    /* Strict Enterprise ERP Alignment Geometry */
    table.receipt-table {
      width: 100% !important;
      max-width: 100% !important;
      table-layout: fixed !important;
      border-collapse: collapse !important;
      margin: 0 0 5px 0 !important;
    }
    table.receipt-table td, table.receipt-table th {
      padding: 3px 2px;
      vertical-align: middle;
    }
    
    .border-box {
      border: 1.5px solid #000;
    }
    .divider-dash {
      border-top: 1px dashed #000;
      margin: 4px 0;
    }
    .divider-solid {
      border-top: 1.5px solid #000;
      margin: 4px 0;
    }
  </style>
</head>
<body>
  <div class="receipt-container">

  <!-- STORE HEADER -->
  <div class="text-center font-black" style="font-size: 18px; line-height: 1.1; margin-top: 1px;">${STORE.nameTamil}</div>
  <div class="text-center font-black" style="font-size: 11px; letter-spacing: 0.5px; margin-top: 1px; margin-bottom: 2px;">${STORE.nameEn}</div>
  <div class="text-center" style="font-size: 10px; font-weight: 700; line-height: 1.3;">
    ${STORE.address} ${STORE.city}<br/>
    GSTIN: ${STORE.gstin} | FSSAI: ${STORE.fssai}<br/>
    Ph: ${STORE.phone}
  </div>

  <div class="divider-solid"></div>
  <div class="text-center font-black" style="font-size: 12px; letter-spacing: 1px; margin: 2px 0 4px 0;">TAX INVOICE</div>

  <!-- METADATA BOX (Perfect 2-Column Key-Value Alignment) -->
  <table class="receipt-table border-box">
    <tr>
      <td style="width: 58%; padding: 4px 5px; border-right: 1.5px solid #000; vertical-align: top;">
        <span style="font-size: 8.5px; font-weight: 800; color: #444; display: block; text-transform: uppercase;">Bill No</span>
        <strong style="font-size: 10px; font-weight: 900; font-family: monospace; word-break: break-all; display: block; line-height: 1.2;">${invoice.invoiceNumber || '-'}</strong>
        <span style="font-size: 10px; font-weight: 800; display: block; margin-top: 2px;">Cashier: ${invoice.cashierName || 'Cashier'}</span>
      </td>
      <td style="width: 42%; padding: 4px 5px; vertical-align: top;">
        <span style="font-size: 8.5px; font-weight: 800; color: #444; display: block; text-transform: uppercase;">Date & Time</span>
        <strong style="font-size: 10.5px; font-weight: 800;">${dateStr}</strong><br/>
        <span style="font-size: 10.5px; font-weight: 800;">${timeStr}</span>
      </td>
    </tr>
    ${invoice.customerName ? `
    <tr style="border-top: 1px solid #000;">
      <td style="padding: 3px 5px; border-right: 1.5px solid #000; overflow: hidden; text-overflow: ellipsis; white-space: nowrap;">
        <strong style="font-weight: 800;">NAME:</strong> ${invoice.customerName}
      </td>
      <td style="padding: 3px 5px; white-space: nowrap;">
        <strong style="font-weight: 800;">CELL:</strong> ${invoice.customerPhone || '-'}
      </td>
    </tr>` : ''}
  </table>

  <!-- ITEMS BOXED TABLE (Strict Table-Layout Fixed Alignment - Full Decimal Display) -->
  <table class="receipt-table border-box">
    <thead>
      <tr style="background: #f0f0f0; border-bottom: 1.5px solid #000;">
        <th style="width: 6%; text-align: center; font-weight: 900; font-size: 10px; padding: 4px 1px; border-right: 1px solid #000;">#</th>
        <th style="width: 38%; text-align: left; font-weight: 900; font-size: 10px; padding: 4px 3px; border-right: 1px solid #000;">Items</th>
        <th style="width: 10%; text-align: center; font-weight: 900; font-size: 10px; padding: 4px 1px; border-right: 1px solid #000;">Qty</th>
        <th style="width: 13%; text-align: right; font-weight: 900; font-size: 10px; padding: 4px 2px; border-right: 1px solid #000;">MRP</th>
        <th style="width: 15%; text-align: right; font-weight: 900; font-size: 10px; padding: 4px 2px; border-right: 1px solid #000;">Rate</th>
        <th style="width: 18%; text-align: right; font-weight: 900; font-size: 10.5px; padding: 4px 3px;">Amt</th>
      </tr>
    </thead>
    <tbody>
      ${itemsRowsHtml}
    </tbody>
  </table>

  <!-- TOTAL BANNER (High Impact Bold Alignment) -->
  <table class="receipt-table border-box">
    <tr>
      <td style="width: 45%; padding: 6px 8px; font-size: 17px; font-weight: 900; text-align: left; vertical-align: middle; border-right: 1.5px solid #000;">
        TOTAL
      </td>
      <td style="width: 55%; padding: 6px 8px; font-size: 23px; font-weight: 900; text-align: right; vertical-align: middle;">
        ₹${fmt(rounded)}
      </td>
    </tr>
  </table>

  <!-- MODERN ENTERPRISE PAYMENT SUMMARY -->
  <table class="receipt-table" style="font-size: 10.5px; font-weight: 800; margin-top: 4px; margin-bottom: 2px;">
    <tr>
      <td style="width: 55%; text-align: left; padding: 1.5px 0;">Payment Method</td>
      <td style="width: 45%; text-align: right; padding: 1.5px 0; font-weight: 900;">${payModeDisplay}</td>
    </tr>
    <tr>
      <td style="width: 55%; text-align: left; padding: 1.5px 0;">Amount Tendered</td>
      <td style="width: 45%; text-align: right; padding: 1.5px 0; font-weight: 900;">₹${rcvdStr}</td>
    </tr>
    <tr>
      <td style="width: 55%; text-align: left; padding: 1.5px 0;">Change Returned</td>
      <td style="width: 45%; text-align: right; padding: 1.5px 0; font-weight: 900;">₹${rfndStr}</td>
    </tr>
  </table>

  <!-- MODERN LOYALTY REWARDS CARD -->
  ${(earned > 0 || oldPts > 0 || balance > 0) ? `
  <div style="margin-top: 4px; border: 1.5px solid #000; padding: 4px 5px; border-radius: 2px; text-align: center; background: #fafafa;">
    <div style="font-weight: 900; font-size: 9px; text-transform: uppercase; letter-spacing: 0.5px; border-bottom: 1px solid #000; padding-bottom: 2px; margin-bottom: 3px;">
      ★ Store Loyalty Rewards
    </div>
    <table class="receipt-table" style="font-size: 9.5px; font-weight: 800; margin: 0;">
      <tr>
        <td style="text-align: left; width: 33%;">Opening: <strong>${oldPtsStr}</strong></td>
        <td style="text-align: center; width: 34%;">Earned: <strong>+${earnedStr}</strong></td>
        <td style="text-align: right; width: 33%;">Balance: <strong>${balanceStr} Pts</strong></td>
      </tr>
    </table>
  </div>` : ''}

  <!-- FOOTER TAMIL SLOGAN -->
  <div class="divider-dash"></div>
  <div class="text-center" style="font-size: 14px; font-weight: 900; margin-top: 4px; margin-bottom: 3px; line-height: 1.35;">
    அனைத்தும் வாங்க<br/>
    ஆப்பிளுக்கு வாங்க
  </div>
  <div class="text-center" style="font-size: 10px; font-weight: 700; color: #222; margin-top: 2px;">
    Thank you for shopping with us! Visit Again!
  </div>
  </div>

  <script>
    window.onload = function() {
      window.print();
      setTimeout(function() { window.close(); }, 500);
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

  let receiptLangMode = 'secondary';
  try {
    const perms = await getPosPermissions();
    if (perms && perms.receiptProductLanguage) {
      receiptLangMode = perms.receiptProductLanguage;
    }
  } catch (err) {
    console.warn('Could not fetch receipt language setting, using default (secondary/Tamil)', err);
  }

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
      triggerSystemPrint(invoice, terminalCode, receiptLangMode);
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
      
      const textContent = generateReceiptText(invoice, terminalCode, receiptLangMode);
      
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
      triggerSystemPrint(invoice, terminalCode, receiptLangMode);
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
