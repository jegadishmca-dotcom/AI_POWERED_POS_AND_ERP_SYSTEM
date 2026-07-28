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
             <style>{`
          @media screen { .pos-receipt { display: none !important; } }
          @media print  {
            @page { size: 80mm auto; margin: 0 auto !important; }
            html, body { width: 100% !important; margin: 0 !important; padding: 0 !important; display: flex !important; justify-content: center !important; align-items: flex-start !important; background: #fff !important; color: #000 !important; -webkit-print-color-adjust: exact; }
            body * { visibility: hidden; }
            .pos-receipt { visibility: visible !important; position: relative !important; width: 72mm !important; margin: 0 auto !important; padding: 2mm 3mm; background: #fff; color: #000; }
            .pos-receipt * { visibility: visible !important; font-weight: 800 !important; color: #000 !important; }
          }
        `}</style>

        <div ref={ref} className="pos-receipt" style={{ width: '72mm', margin: '0 auto', padding: '2mm 3mm', fontFamily: 'Arial, sans-serif', color: '#000', background: '#fff', fontSize: '11.5px', lineHeight: '1.25', fontWeight: 800 }}>

          {/* ── HEADER ── */}
          <div style={{ textAlign: 'center', fontWeight: 900, fontSize: '17px', marginTop: '2px' }}>{STORE.nameTamil}</div>
          <div style={{ textAlign: 'center', fontWeight: 900, fontSize: '11px', marginTop: '1px' }}>GST :{STORE.gstin}</div>
          <div style={{ textAlign: 'center', fontWeight: 900, fontSize: '11px' }}>FSSAI : {STORE.fssai}</div>
          <div style={{ textAlign: 'center', fontWeight: 800, fontSize: '11px' }}>1E -1G மாதா கோவில் தெரு</div>
          <div style={{ textAlign: 'center', fontWeight: 800, fontSize: '11px' }}>இளையான்குடி -630702</div>
          <div style={{ textAlign: 'center', fontWeight: 900, fontSize: '11px', marginBottom: '4px' }}>CELL:{STORE.phone}</div>

          {/* ── INVOICE META BOX ── */}
          <table style={{ width: '100%', borderCollapse: 'collapse', border: '2px solid #000', marginBottom: '4px', fontSize: '11px' }}>
            <tbody>
              <tr>
                <td style={{ border: '1.5px solid #000', padding: '3px 4px', fontWeight: 900, width: '55%' }}>
                  Bill No : &nbsp; {invoice.invoiceNumber || '-'}<br/>
                  <span style={{ fontSize: '11.5px', fontWeight: 900 }}>{invoice.cashierName || 'USER3'}</span>
                </td>
                <td style={{ border: '1.5px solid #000', padding: '3px 4px', fontWeight: 900, width: '45%' }}>
                  Date : {invoice.businessDate ? new Date(invoice.businessDate).toLocaleDateString('en-IN') : new Date().toLocaleDateString('en-IN')}<br/>
                  Time : {invoice.businessDate ? new Date(invoice.businessDate).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }) : new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}
                </td>
              </tr>
              <tr>
                <td style={{ border: '1.5px solid #000', padding: '3px 4px', fontWeight: 900 }}>NAME : {invoice.customerName || ''}</td>
                <td style={{ border: '1.5px solid #000', padding: '3px 4px', fontWeight: 900 }}>CELL : {invoice.customerPhone || ''}</td>
              </tr>
            </tbody>
          </table>

          {/* ── ITEMS BOXED TABLE ── */}
          <table style={{ width: '100%', borderCollapse: 'collapse', border: '2px solid #000', marginBottom: '4px', fontSize: '11px' }}>
            <thead>
              <tr>
                <th style={{ border: '1.5px solid #000', padding: '3px 2px', textAlign: 'center', width: '44%', fontWeight: 900 }}>Items</th>
                <th style={{ border: '1.5px solid #000', padding: '3px 2px', textAlign: 'center', width: '10%', fontWeight: 900 }}>Qty</th>
                <th style={{ border: '1.5px solid #000', padding: '3px 2px', textAlign: 'center', width: '14%', fontWeight: 900 }}>MRP</th>
                <th style={{ border: '1.5px solid #000', padding: '3px 2px', textAlign: 'center', width: '16%', fontWeight: 900 }}>Rate</th>
                <th style={{ border: '1.5px solid #000', padding: '3px 2px', textAlign: 'center', width: '16%', fontWeight: 900 }}>Amt</th>
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
                    <td style={{ border: '1.5px solid #000', padding: '3px 2px', fontWeight: 900 }}>{item.name || item.productName || '-'}</td>
                    <td style={{ border: '1.5px solid #000', padding: '3px 2px', textAlign: 'center', fontWeight: 800 }}>{qty}</td>
                    <td style={{ border: '1.5px solid #000', padding: '3px 2px', textAlign: 'center', fontWeight: 800 }}>{mrpVal}</td>
                    <td style={{ border: '1.5px solid #000', padding: '3px 2px', textAlign: 'right', fontWeight: 800 }}>{fmt(item.unitPrice)}</td>
                    <td style={{ border: '1.5px solid #000', padding: '3px 2px', textAlign: 'right', fontWeight: 900 }}>{fmt(lineAmt)}</td>
                  </tr>
                );
              })}
            </tbody>
          </table>

          {/* ── TOTAL BOX ── */}
          <table style={{ width: '100%', borderCollapse: 'collapse', border: '2px solid #000', marginBottom: '4px' }}>
            <tbody>
              <tr>
                <td style={{ border: '1.5px solid #000', padding: '5px 4px', fontSize: '18px', fontWeight: 900, textAlign: 'center', width: '50%' }}>
                  TOTAL
                </td>
                <td style={{ border: '1.5px solid #000', padding: '5px 4px', fontSize: '22px', fontWeight: 900, textAlign: 'right', width: '50%' }}>
                  {fmt(Math.round(safe(invoice.totalAmount)))}
                </td>
              </tr>
            </tbody>
          </table>

          {/* ── SUMMARY & POINTS ── */}
          <table style={{ width: '100%', fontSize: '11px', fontWeight: 900, marginBottom: '4px', borderCollapse: 'collapse' }}>
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
          <div style={{ textAlign: 'center', fontWeight: 900, fontSize: '15px', marginTop: '6px', marginBottom: '4px', lineHeight: '1.3' }}>
            அனைத்தும் வாங்க<br/>
            ஆப்பிளுக்கு வாங்க
          </div>

        </div>
      </>
    );
  }
);={{ width: '45%', textAlign: 'right' }}>RFND : {fmt(changeDue)}</td>
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
