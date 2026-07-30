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

export const ThermalReceipt = React.forwardRef<HTMLDivElement, { invoice: any }>(
  ({ invoice }, ref) => {
    if (!invoice) return null;

    // ── Totals ────────────────────────────────────────────────────────────────
    const netPayable = safe(invoice.netPayable);
    const tenderCash   = safe(invoice.cashAmount);
    const tenderUpi    = safe(invoice.upiAmount);
    const tenderCard   = safe(invoice.cardAmount);
    const tenderWallet = safe(invoice.walletAmountUsed);
    const totalTendered = tenderCash + tenderUpi + tenderCard + tenderWallet;
    const changeDue     = Math.max(0, totalTendered - netPayable);

    return (
      <>
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
                <th style={{ border: '1.5px solid #000', padding: '3px 2px', textAlign: 'center', width: '10%', fontWeight: 900 }}>S.No</th>
                <th style={{ border: '1.5px solid #000', padding: '3px 2px', textAlign: 'center', width: '34%', fontWeight: 900 }}>Items</th>
                <th style={{ border: '1.5px solid #000', padding: '3px 2px', textAlign: 'center', width: '10%', fontWeight: 900 }}>Qty</th>
                <th style={{ border: '1.5px solid #000', padding: '3px 2px', textAlign: 'center', width: '14%', fontWeight: 900 }}>MRP</th>
                <th style={{ border: '1.5px solid #000', padding: '3px 2px', textAlign: 'center', width: '16%', fontWeight: 900 }}>Rate</th>
                <th style={{ border: '1.5px solid #000', padding: '3px 2px', textAlign: 'center', width: '16%', fontWeight: 900, fontSize: '12px' }}>Amt</th>
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
                    <td style={{ border: '1.5px solid #000', padding: '3px 2px', textAlign: 'center', fontWeight: 800 }}>{idx + 1}</td>
                    <td style={{ border: '1.5px solid #000', padding: '3px 2px', fontWeight: 900 }}>{item.name || item.productName || '-'}</td>
                    <td style={{ border: '1.5px solid #000', padding: '3px 2px', textAlign: 'center', fontWeight: 800 }}>{qty}</td>
                    <td style={{ border: '1.5px solid #000', padding: '3px 2px', textAlign: 'center', fontWeight: 800 }}>{mrpVal}</td>
                    <td style={{ border: '1.5px solid #000', padding: '3px 2px', textAlign: 'right', fontWeight: 800 }}>{fmt(item.unitPrice)}</td>
                    <td style={{ border: '1.5px solid #000', padding: '3px 2px', textAlign: 'right', fontWeight: 900, fontSize: '11.5px' }}>{fmt(lineAmt)}</td>
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
);
