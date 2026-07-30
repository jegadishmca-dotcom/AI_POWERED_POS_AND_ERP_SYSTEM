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

  const taxSlabs: Record<string, { taxable: number; cgst: number; sgst: number; cess: number }> = {};
  let totalCessAmount = 0;
  let totalGstAmount = 0;
  const hasCess = (invoice.items || []).some((item: any) => safe(item.cessRate) > 0 || safe(item.cessAmount) > 0);

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

  const dateStr  = invoice.businessDate ? new Date(invoice.businessDate).toLocaleDateString('en-IN') : new Date().toLocaleDateString('en-IN');
  const timeStr  = invoice.businessDate ? new Date(invoice.businessDate).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }) : new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });

  // Build items boxed table rows
  let itemsRowsHtml = '';
  (invoice.items || []).forEach((item: any) => {
    const qty  = safe(item.quantity, safe(item.qty));
    const disc = safe(item.discountAmount);
    const lineAmt = safe(item.unitPrice) * qty - disc;
    const mrp = safe(item.mrp || item.unitPrice);
    const mrpVal = mrp > 0 ? Math.round(mrp).toString() : '-';

    itemsRowsHtml += `
      <tr>
        <td style="border:1.5px solid #000; padding:2px 3px; font-weight:800; font-size:11px;">${item.name || item.productName || '-'}</td>
        <td style="border:1.5px solid #000; text-align:center; padding:2px 3px; font-weight:800; font-size:11px;">${qty}</td>
        <td style="border:1.5px solid #000; text-align:center; padding:2px 3px; font-weight:800; font-size:11px;">${mrpVal}</td>
        <td style="border:1.5px solid #000; text-align:right; padding:2px 3px; font-weight:800; font-size:11px;">${fmt(item.unitPrice)}</td>
        <td style="border:1.5px solid #000; text-align:right; padding:2px 3px; font-weight:900; font-size:11.5px;">${fmt(lineAmt)}</td>
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
        padding: 2mm 3mm !important;
      }
    }
    * {
      box-sizing: border-box;
      margin: 0;
      padding: 0;
      font-family: Arial, "Helvetica Neue", Helvetica, sans-serif;
      font-weight: 800; /* Extra bold for maximum thermal print density */
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
      padding: 2mm 3mm;
      font-size: 11.5px;
      line-height: 1.25;
      background: #fff;
    }
    .text-center { text-align: center; }
    .text-right { text-align: right; }
    .font-bold { font-weight: 900; }
    .border-table { border-collapse: collapse; width: 100%; border: 2px solid #000; }
    .border-table th, .border-table td { border: 1.5px solid #000; padding: 3px 3px; font-size: 11px; font-weight: 800; }
  </style>
</head>
<body>
  <div class="receipt-container">
  <!-- HEADER -->
  <div class="text-center font-bold" style="font-size: 17px; margin-top: 2px;">${STORE.nameTamil}</div>
  <div class="text-center font-bold" style="font-size: 11px; margin-top: 1px;">GST :${STORE.gstin}</div>
  <div class="text-center font-bold" style="font-size: 11px;">FSSAI : ${STORE.fssai}</div>
  <div class="text-center font-bold" style="font-size: 11px;">1E -1G மாதா கோவில் தெரு</div>
  <div class="text-center font-bold" style="font-size: 11px;">இளையான்குடி -630702</div>
  <div class="text-center font-bold" style="font-size: 11px; margin-bottom: 4px;">CELL:${STORE.phone}</div>

  <!-- METADATA BOX -->
  <table class="border-table" style="margin-bottom: 4px;">
    <tr>
      <td width="55%" style="font-weight: 900;">
        Bill No : &nbsp; ${invoice.invoiceNumber || '-'}<br/>
        <span style="font-size: 11.5px; font-weight: 900;">${invoice.cashierName || 'USER3'}</span>
      </td>
      <td width="45%" style="font-weight: 900;">
        Date : ${dateStr}<br/>
        Time : ${timeStr}
      </td>
    </tr>
    <tr>
      <td style="font-weight: 900;">NAME : ${invoice.customerName || ''}</td>
      <td style="font-weight: 900;">CELL : ${invoice.customerPhone || ''}</td>
    </tr>
  </table>

  <!-- ITEMS BOXED TABLE -->
  <table class="border-table" style="margin-bottom: 4px;">
    <thead>
      <tr>
        <th style="width: 44%; text-align: center; font-weight: 900; font-size: 11px;">Items</th>
        <th style="width: 10%; text-align: center; font-weight: 900; font-size: 11px;">Qty</th>
        <th style="width: 14%; text-align: center; font-weight: 900; font-size: 11px;">MRP</th>
        <th style="width: 16%; text-align: center; font-weight: 900; font-size: 11px;">Rate</th>
        <th style="width: 16%; text-align: center; font-weight: 900; font-size: 12px;">Amt</th>
      </tr>
    </thead>
    <tbody>
      ${itemsRowsHtml}
    </tbody>
  </table>

  <!-- TOTAL BOX -->
  <table class="border-table" style="margin-bottom: 4px;">
    <tr>
      <td width="50%" class="text-center" style="font-size: 18px; font-weight: 900; padding: 5px 4px;">
        TOTAL
      </td>
      <td width="50%" class="text-right" style="font-size: 22px; font-weight: 900; padding: 5px 4px;">
        ${fmt(rounded)}
      </td>
    </tr>
  </table>

  <!-- SUMMARY & POINTS -->
  <table style="width: 100%; font-size: 11px; font-weight: 900; margin-bottom: 4px; border-collapse: collapse;">
    <tr>
      <td width="55%">OLD POINTS : ${oldPtsStr}</td>
      <td width="45%" class="text-right">RCVD : ${rcvdStr}</td>
    </tr>
    <tr>
      <td width="55%">TODAY PTS : ${earnedStr}</td>
      <td width="45%" class="text-right">RFND : ${rfndStr}</td>
    </tr>
    <tr>
      <td width="55%">TOTAL PTS : ${balanceStr}</td>
      <td width="45%"></td>
    </tr>
  </table>

  <!-- FOOTER TAMIL SLOGAN -->
  <div class="text-center" style="font-size: 15px; font-weight: 900; margin-top: 6px; margin-bottom: 4px; line-height: 1.3;">
    அனைத்தும் வாங்க<br/>
    ஆப்பிளுக்கு வாங்க
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
