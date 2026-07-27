import React from 'react';

// ── Store Details (from actual invoice header) ──────────────────────────────
const STORE = {
  nameTamil: 'ஆப்பிள் சூப்பர் மார்க்கெட்',
  nameEn:    'Apple Super Market',
  gstin:     '33ABTFA7190F1Z7',
  fssai:     '12421019000047',
  address:   '1E-16, Matha Kovil Street,',
  city:      'Ilayankudi - 630702',
  phone:     '7339056767 / 04564-221190',
};

const safe = (n: any, fallback = 0): number => (typeof n === 'number' && !isNaN(n) ? n : fallback);
const fmt  = (n: any) => safe(n).toFixed(2);
const roundNearest = (amount: number) => {
  const rounded = Math.round(safe(amount));
  const diff    = +(rounded - safe(amount)).toFixed(2);
  return { rounded, diff };
};

export const ThermalReceipt = React.forwardRef<HTMLDivElement, { invoice: any }>(
  ({ invoice }, ref) => {
    if (!invoice) return null;

    // ── Totals ────────────────────────────────────────────────────────────────
    const netPayable = safe(invoice.netPayable);
    const roundOffAmt = safe(invoice.roundOff);
    const tenderCash   = safe(invoice.cashAmount);
    const tenderUpi    = safe(invoice.upiAmount);
    const tenderCard   = safe(invoice.cardAmount);
    const tenderWallet = safe(invoice.walletAmountUsed);
    const totalTendered = tenderCash + tenderUpi + tenderCard + tenderWallet;
    const changeDue     = Math.max(0, totalTendered - netPayable);

    // ── Per-slab GST summary ─────────────────────────────────────────────────
    const taxSlabs: Record<string, { taxable: number; cgst: number; sgst: number; cess: number }> = {};
    let totalCessAmount = 0;
    let totalGstAmount = 0;
    const hasCess = (invoice.items || []).some((item: any) => safe(item.cessRate) > 0 || safe(item.cessAmount) > 0);
    (invoice.items || []).forEach((item: any) => {
      const cgstRate = safe(item.cgstRate);
      const sgstRate = safe(item.sgstRate);
      const cessRate = safe(item.cessRate);
      const totalRate = cgstRate + sgstRate + cessRate;
      const itemBaseInclusive = safe(item.unitPrice) * safe(item.quantity) - safe(item.discountAmount);
      const itemBaseTaxable = totalRate > 0 ? itemBaseInclusive / (1 + totalRate / 100) : itemBaseInclusive;
      
      const cgstAmt = item.cgstAmount !== undefined ? safe(item.cgstAmount) : itemBaseTaxable * (cgstRate / 100);
      const sgstAmt = item.sgstAmount !== undefined ? safe(item.sgstAmount) : itemBaseTaxable * (sgstRate / 100);
      const cessAmt = item.cessAmount !== undefined ? safe(item.cessAmount) : itemBaseTaxable * (cessRate / 100);
      
      totalCessAmount += cessAmt;
      totalGstAmount += cgstAmt + sgstAmt;

      if ((cgstRate + sgstRate) === 0 && cessRate === 0) return;
      const key       = `GST ${(cgstRate + sgstRate)}%`;
      if (!taxSlabs[key]) taxSlabs[key] = { taxable: 0, cgst: 0, sgst: 0, cess: 0 };
      taxSlabs[key].taxable += itemBaseTaxable;
      taxSlabs[key].cgst    += cgstAmt;
      taxSlabs[key].sgst    += sgstAmt;
      taxSlabs[key].cess    += cessAmt;
    });

    // ── Terminal code from localStorage ──────────────────────────────────────
    const terminalCode = (() => {
      try { return localStorage.getItem('pos_terminal_code') || 'POS-01'; }
      catch { return 'POS-01'; }
    })();

    // ── Styles (inline — no Tailwind dependency) ───────────────────────────
    const S: Record<string, React.CSSProperties> = {
      wrap:      { fontFamily: 'monospace', fontSize: '11px', width: '80mm', padding: '0 3mm', color: '#000', background: '#fff' },
      center:    { textAlign: 'center' },
      bold:      { fontWeight: 'bold' },
      row:       { display: 'flex', justifyContent: 'space-between' },
      hr:        { borderTop: '1px dashed #000', margin: '4px 0' },
      bigTotal:  { display: 'flex', justifyContent: 'space-between', fontWeight: 'bold', fontSize: '14px', borderTop: '1px dashed #000', paddingTop: '4px', marginTop: '4px' },
      tableHead: { display: 'flex', fontWeight: 'bold', borderBottom: '1px dashed #000', paddingBottom: '2px', marginBottom: '2px' },
      itemRow:   { display: 'flex', marginBottom: '1px' },
      name:      { flex: 1 },
      qty:       { width: '24px', textAlign: 'center' },
      rate:      { width: '50px', textAlign: 'right' },
      amt:       { width: '52px', textAlign: 'right' },
      small:     { fontSize: '9px' },
      discLine:  { fontSize: '9px', paddingLeft: '8px', color: '#2d6a2d' },
      footer:    { textAlign: 'center', marginTop: '8px', paddingTop: '6px', borderTop: '1px dashed #000', paddingBottom: '4px' },
    };

    return (
      <>
        {/* 
          PRINT CSS: visibility:hidden on body lets children override with visibility:visible.
          display:none on parent would block all children — that was causing the blank receipt.
        */}
        <style>{`
          @media screen { .pos-receipt { display: none !important; } }
          @media print  {
            @page { size: 76mm auto; margin: 0mm !important; }
            html, body { width: 76mm !important; margin: 0 !important; padding: 0 !important; background: #fff !important; color: #000 !important; -webkit-print-color-adjust: exact; }
            body * { visibility: hidden; }
            .pos-receipt { visibility: visible !important; position: absolute; top: 0; left: 0; width: 76mm !important; margin: 0 auto; padding: 1mm 2mm; background: #fff; color: #000; }
            .pos-receipt * { visibility: visible !important; }
          }
        `}</style>

        <div ref={ref} className="pos-receipt" style={{ width: '76mm', padding: '1mm 2mm', fontFamily: 'Arial, sans-serif', color: '#000', background: '#fff', fontSize: '11px', lineHeight: '1.2' }}>

          {/* ── HEADER ── */}
          <div style={{ textAlign: 'center', fontWeight: 'bold', fontSize: '15px', marginTop: '2px' }}>{STORE.nameTamil}</div>
          <div style={{ textAlign: 'center', fontWeight: 'bold', fontSize: '10px', marginTop: '1px' }}>GST :{STORE.gstin}</div>
          <div style={{ textAlign: 'center', fontWeight: 'bold', fontSize: '10px' }}>FSSAI : {STORE.fssai}</div>
          <div style={{ textAlign: 'center', fontSize: '10px' }}>1E -1G மாதா கோவில் தெரு</div>
          <div style={{ textAlign: 'center', fontSize: '10px' }}>இளையான்குடி -630702</div>
          <div style={{ textAlign: 'center', fontWeight: 'bold', fontSize: '10px', marginBottom: '4px' }}>CELL:{STORE.phone}</div>

          {/* ── INVOICE META BOX ── */}
          <table style={{ width: '100%', borderCollapse: 'collapse', border: '1px solid #000', marginBottom: '4px', fontSize: '10px' }}>
            <tbody>
              <tr>
                <td style={{ border: '1px solid #000', padding: '2px 4px', fontWeight: 'bold', width: '55%' }}>
                  Bill No : &nbsp; {invoice.invoiceNumber || '-'}<br/>
                  <span style={{ fontSize: '11px' }}>{invoice.cashierName || 'USER3'}</span>
                </td>
                <td style={{ border: '1px solid #000', padding: '2px 4px', fontWeight: 'bold', width: '45%' }}>
                  Date : {invoice.businessDate ? new Date(invoice.businessDate).toLocaleDateString('en-IN') : new Date().toLocaleDateString('en-IN')}<br/>
                  Time : {invoice.businessDate ? new Date(invoice.businessDate).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }) : new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}
                </td>
              </tr>
              <tr>
                <td style={{ border: '1px solid #000', padding: '2px 4px', fontWeight: 'bold' }}>NAME : {invoice.customerName || ''}</td>
                <td style={{ border: '1px solid #000', padding: '2px 4px', fontWeight: 'bold' }}>CELL : {invoice.customerPhone || ''}</td>
              </tr>
            </tbody>
          </table>

          {/* ── ITEMS BOXED TABLE ── */}
          <table style={{ width: '100%', borderCollapse: 'collapse', border: '1px solid #000', marginBottom: '4px', fontSize: '10px' }}>
            <thead>
              <tr>
                <th style={{ border: '1px solid #000', padding: '2px', textAlign: 'center', width: '44%' }}>Items</th>
                <th style={{ border: '1px solid #000', padding: '2px', textAlign: 'center', width: '10%' }}>Qty</th>
                <th style={{ border: '1px solid #000', padding: '2px', textAlign: 'center', width: '14%' }}>MRP</th>
                <th style={{ border: '1px solid #000', padding: '2px', textAlign: 'center', width: '16%' }}>Rate</th>
                <th style={{ border: '1px solid #000', padding: '2px', textAlign: 'center', width: '16%' }}>Amt</th>
              </tr>
            </thead>
            <tbody>
              {(invoice.items || []).map((item: any, idx: number) => {
                const qty = safe(item.quantity, safe(item.qty));
                const disc = safe(item.discountAmount);
                const lineAmt = safe(item.unitPrice) * qty - disc;
                const mrp = safe(item.mrp || item.unitPrice);
                const mrpVal = mrp > 0 ? Math.round(mrp).toString() : '-';
                return (
                  <tr key={idx}>
                    <td style={{ border: '1px solid #000', padding: '2px', fontWeight: 'bold' }}>{item.name || item.productName || '-'}</td>
                    <td style={{ border: '1px solid #000', padding: '2px', textAlign: 'center' }}>{qty}</td>
                    <td style={{ border: '1px solid #000', padding: '2px', textAlign: 'center' }}>{mrpVal}</td>
                    <td style={{ border: '1px solid #000', padding: '2px', textAlign: 'right' }}>{fmt(item.unitPrice)}</td>
                    <td style={{ border: '1px solid #000', padding: '2px', textAlign: 'right', fontWeight: 'bold' }}>{fmt(lineAmt)}</td>
                  </tr>
                );
              })}
            </tbody>
          </table>

          {/* ── TOTAL BOX ── */}
          <table style={{ width: '100%', borderCollapse: 'collapse', border: '1px solid #000', marginBottom: '4px' }}>
            <tbody>
              <tr>
                <td style={{ border: '1px solid #000', padding: '4px', fontSize: '16px', fontWeight: 'bold', textAlign: 'center', width: '55%' }}>
                  TOTAL
                </td>
                <td style={{ border: '1px solid #000', padding: '4px', fontSize: '18px', fontWeight: 'bold', textAlign: 'right', width: '45%' }}>
                  {fmt(Math.round(safe(invoice.totalAmount)))}
                </td>
              </tr>
            </tbody>
          </table>

          {/* ── SUMMARY & POINTS ── */}
          <table style={{ width: '100%', fontSize: '10px', fontWeight: 'bold', marginBttom: '4px', borderCollapse: 'collapse' }}>
            <tbody>
              <tr>
                <td style={{ width: '55%' }}>OLD POINTS : {Math.max(0, safe(invoice.loyaltyPointsBalance) - safe(invoice.loyaltyPointsEarned)).toFixed(2)}</td>
                <td style={{ width: '45%', textAlign: 'right' }}>RCVD : {fmt(totalTendered > 0 ? totalTendered : netPayable)}</td>
              </tr>
              <tr>
                <td style={{ width: '55%' }}>TODAY PTS : {safe(invoice.loyaltyPointsEarned).toFixed(2)}</td>
                <td style={{ width: '45%', textAlign: 'right' }}>RFND : {fmt(changeDue)}</td>
              </tr>
              <tr>
                <td style={{ width: '55%' }}>TOTAL PTS : {safe(invoice.loyaltyPointsBalance).toFixed(2)}</td>
                <td style={{ width: '45%' }}></td>
              </tr>
            </tbody>
          </table>

          {/* ── FOOTER TAMIL SLOGAN ── */}
          <div style={{ textAlign: 'center', fontWeight: 'bold', fontSize: '13px', marginTop: '4px', marginBottom: '4px' }}>
            அனைத்தும் வாங்க<br/>
            ஆப்பிளுக்கு வாங்க
          </div>

        </div>
      </>
    );
  }
);
