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
    const { rounded: netPayable, diff: roundOffAmt } = roundNearest(invoice.totalAmount);
    const tenderCash   = safe(invoice.cashAmount);
    const tenderUpi    = safe(invoice.upiAmount);
    const tenderCard   = safe(invoice.cardAmount);
    const tenderWallet = safe(invoice.walletAmountUsed);
    const totalTendered = tenderCash + tenderUpi + tenderCard + tenderWallet;
    const changeDue     = Math.max(0, totalTendered - netPayable);

    // ── Per-slab GST summary ─────────────────────────────────────────────────
    const taxSlabs: Record<string, { taxable: number; cgst: number; sgst: number }> = {};
    (invoice.items || []).forEach((item: any) => {
      const cgstRate = safe(item.cgstRate);
      const sgstRate = safe(item.sgstRate);
      const totalRate = cgstRate + sgstRate;
      if (totalRate === 0) return;
      const key       = `GST ${totalRate}%`;
      const itemBase  = safe(item.unitPrice) * safe(item.quantity) - safe(item.discountAmount);
      if (!taxSlabs[key]) taxSlabs[key] = { taxable: 0, cgst: 0, sgst: 0 };
      taxSlabs[key].taxable += itemBase;
      taxSlabs[key].cgst    += itemBase * (cgstRate / 100);
      taxSlabs[key].sgst    += itemBase * (sgstRate / 100);
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
        {/* Critical: use <style> tag to handle print visibility reliably without Tailwind */}
        <style>{`
          @media screen { .pos-receipt { display: none !important; } }
          @media print   { .pos-receipt { display: block !important; } body > *:not(.pos-receipt-root) { display: none !important; } }
        `}</style>

        <div ref={ref} className="pos-receipt" style={S.wrap}>

          {/* ── HEADER ── */}
          <div style={{ ...S.center, fontWeight: 'bold', fontSize: '13px', marginTop: '6px' }}>{STORE.nameTamil}</div>
          <div style={{ ...S.center, fontWeight: 'bold', fontSize: '11px' }}>{STORE.nameEn}</div>
          <div style={{ ...S.center, marginTop: '3px' }}>{STORE.address}</div>
          <div style={S.center}>{STORE.city}</div>
          <div style={S.center}>Ph: {STORE.phone}</div>
          <div style={{ ...S.center, marginTop: '2px' }}>GSTIN: {STORE.gstin}</div>
          <div style={S.center}>FSSAI: {STORE.fssai}</div>
          <div style={{ ...S.center, fontWeight: 'bold', marginTop: '3px' }}>TAX INVOICE</div>

          <div style={S.hr}/>

          {/* ── INVOICE META ── */}
          <div style={S.row}><span>Bill No: {invoice.invoiceNumber || '-'}</span></div>
          <div style={S.row}>
            <span>Date: {invoice.businessDate ? new Date(invoice.businessDate).toLocaleDateString('en-IN') : '-'}</span>
            <span>Time: {invoice.businessDate ? new Date(invoice.businessDate).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }) : '-'}</span>
          </div>
          <div style={S.row}>
            <span>Cashier: {invoice.cashierName || 'Cashier'}</span>
            <span>Terminal: {terminalCode}</span>
          </div>
          {invoice.customerName && (
            <div>Customer: {invoice.customerName}{invoice.customerPhone ? ` | ${invoice.customerPhone}` : ''}</div>
          )}

          <div style={S.hr}/>

          {/* ── ITEMS ── */}
          <div style={S.tableHead}>
            <span style={S.name}>Item</span>
            <span style={S.qty}>Qty</span>
            <span style={S.rate}>Rate</span>
            <span style={S.amt}>Amt</span>
          </div>
          {(invoice.items || []).map((item: any, idx: number) => {
            const lineAmt = safe(item.unitPrice) * safe(item.quantity) - safe(item.discountAmount);
            return (
              <div key={idx}>
                <div style={S.itemRow}>
                  <span style={S.name}>{item.name || '-'}</span>
                  <span style={S.qty}>{safe(item.quantity)}</span>
                  <span style={S.rate}>{fmt(item.unitPrice)}</span>
                  <span style={S.amt}>{fmt(lineAmt)}</span>
                </div>
                {safe(item.discountAmount) > 0 && (
                  <div style={S.discLine}>Discount: -{fmt(item.discountAmount)}</div>
                )}
              </div>
            );
          })}

          <div style={S.hr}/>

          {/* ── BILL SUMMARY ── */}
          <div style={S.row}><span>Sub Total</span><span>{fmt(invoice.subTotal || invoice.totalAmount)}</span></div>
          {safe(invoice.discountAmount) > 0 && (
            <div style={S.row}><span>Discount</span><span>-{fmt(invoice.discountAmount)}</span></div>
          )}
          {safe(invoice.taxAmount) > 0 && (
            <div style={S.row}><span>GST (CGST+SGST)</span><span>+{fmt(invoice.taxAmount)}</span></div>
          )}
          {roundOffAmt !== 0 && (
            <div style={S.row}><span>Round Off</span><span>{roundOffAmt > 0 ? '+' : ''}{fmt(roundOffAmt)}</span></div>
          )}
          <div style={S.bigTotal}><span>NET PAYABLE</span><span>₹ {netPayable}</span></div>

          <div style={S.hr}/>

          {/* ── PAYMENT ── */}
          {tenderCash   > 0 && <div style={S.row}><span>Cash Tendered</span><span>{fmt(tenderCash)}</span></div>}
          {tenderUpi    > 0 && <div style={S.row}><span>UPI Paid</span><span>{fmt(tenderUpi)}</span></div>}
          {tenderCard   > 0 && <div style={S.row}><span>Card Paid</span><span>{fmt(tenderCard)}</span></div>}
          {tenderWallet > 0 && <div style={S.row}><span>Wallet Paid</span><span>{fmt(tenderWallet)}</span></div>}
          {!tenderCash && !tenderUpi && !tenderCard && !tenderWallet && (
            <div style={S.row}><span>Paid via</span><span>{invoice.paymentMode || 'CASH'}</span></div>
          )}
          {changeDue > 0 && <div style={{ ...S.row, ...S.bold }}><span>Change Due</span><span>{fmt(changeDue)}</span></div>}

          {/* ── LOYALTY ── */}
          {invoice.customerName && (safe(invoice.loyaltyPointsEarned) > 0 || safe(invoice.loyaltyPointsBalance) > 0) && (
            <>
              <div style={S.hr}/>
              {safe(invoice.loyaltyPointsEarned) > 0 && <div style={S.row}><span>Points Earned Today</span><span>+{fmt(invoice.loyaltyPointsEarned)}</span></div>}
              {safe(invoice.loyaltyPointsBalance) > 0 && <div style={S.row}><span>Total Points Balance</span><span>{fmt(invoice.loyaltyPointsBalance)}</span></div>}
            </>
          )}

          {/* ── GST SUMMARY ── */}
          {Object.keys(taxSlabs).length > 0 && (
            <>
              <div style={S.hr}/>
              <div style={{ ...S.bold, ...S.small, marginBottom: '2px' }}>GST Summary</div>
              <div style={{ ...S.tableHead, ...S.small }}>
                <span style={{ width: '40px' }}>Slab</span>
                <span style={{ flex: 1, textAlign: 'right' }}>Taxable</span>
                <span style={{ flex: 1, textAlign: 'right' }}>CGST</span>
                <span style={{ flex: 1, textAlign: 'right' }}>SGST</span>
              </div>
              {Object.entries(taxSlabs).map(([key, slab], i) => (
                <div key={i} style={{ ...S.itemRow, ...S.small }}>
                  <span style={{ width: '40px' }}>{key}</span>
                  <span style={{ flex: 1, textAlign: 'right' }}>{fmt(slab.taxable)}</span>
                  <span style={{ flex: 1, textAlign: 'right' }}>{fmt(slab.cgst)}</span>
                  <span style={{ flex: 1, textAlign: 'right' }}>{fmt(slab.sgst)}</span>
                </div>
              ))}
            </>
          )}

          {/* ── FOOTER ── */}
          <div style={S.footer}>
            <div>Tax Invoice | GSTIN: {STORE.gstin}</div>
            <div style={{ marginTop: '4px' }}>அனைத்தும் வாங்க நன்றிபிளுக்கு வாங்க</div>
            <div>Thank you for shopping with us!</div>
            <div>Visit Again!</div>
          </div>

          {/* Spacer for thermal auto-cutter */}
          <div style={{ height: '20mm' }}/>
        </div>
      </>
    );
  }
);
