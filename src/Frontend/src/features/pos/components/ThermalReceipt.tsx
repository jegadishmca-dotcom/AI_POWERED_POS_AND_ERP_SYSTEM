import React from 'react';

// Store constants extracted from actual invoice header
const STORE = {
  name: 'ஆப்பிள் சூப்பர் மார்க்கெட்',
  nameEn: 'Apple Super Market',
  gstin: '33ABTFA7190F1Z7',
  fssai: '12421019000047',
  address: '1E-16, Matha Kovil Street,',
  city: 'Ilayankudi - 630702',
  phone: '7339056767 / 04564-221190',
};

// Helper: Format currency
const fmt = (n: number) => n.toFixed(2);

// Helper: Round off to nearest rupee
const roundOff = (amount: number): { rounded: number; diff: number } => {
  const rounded = Math.round(amount);
  return { rounded, diff: +(rounded - amount).toFixed(2) };
};

export const ThermalReceipt = React.forwardRef<HTMLDivElement, { invoice: any }>(({ invoice }, ref) => {
  if (!invoice) return null;

  const { rounded: netPayable, diff: roundOffAmt } = roundOff(invoice.totalAmount);
  const tenderCash = invoice.cashAmount || 0;
  const tenderUpi = invoice.upiAmount || 0;
  const tenderCard = invoice.cardAmount || 0;
  const totalTendered = tenderCash + tenderUpi + tenderCard + (invoice.walletAmountUsed || 0);
  const changeDue = Math.max(0, totalTendered - netPayable);

  // Build per-slab tax summary from items
  const taxSummary: Record<string, { taxableValue: number; cgst: number; sgst: number; rate: string }> = {};
  (invoice.items || []).forEach((item: any) => {
    const cgstRate = item.cgstRate || 0;
    const sgstRate = item.sgstRate || 0;
    const totalRate = cgstRate + sgstRate;
    if (totalRate === 0) return;
    const slabKey = `${totalRate}%`;
    const itemBase = (item.unitPrice * item.quantity) - (item.discountAmount || 0);
    const cgstAmt = itemBase * (cgstRate / 100);
    const sgstAmt = itemBase * (sgstRate / 100);
    if (!taxSummary[slabKey]) taxSummary[slabKey] = { taxableValue: 0, cgst: 0, sgst: 0, rate: slabKey };
    taxSummary[slabKey].taxableValue += itemBase;
    taxSummary[slabKey].cgst += cgstAmt;
    taxSummary[slabKey].sgst += sgstAmt;
  });

  return (
    <div ref={ref} className="hidden print:block text-black bg-white" style={{ width: '80mm', fontFamily: 'monospace', fontSize: '11px', padding: '0 3mm' }}>

      {/* ── HEADER ── */}
      <div className="text-center font-bold text-[13px] mt-2">{STORE.name}</div>
      <div className="text-center font-bold text-[11px]">{STORE.nameEn}</div>
      <div className="text-center text-[10px] mt-1">{STORE.address}</div>
      <div className="text-center text-[10px]">{STORE.city}</div>
      <div className="text-center text-[10px]">Ph: {STORE.phone}</div>
      <div className="text-center text-[10px] mt-1">GSTIN: {STORE.gstin}</div>
      <div className="text-center text-[10px]">FSSAI: {STORE.fssai}</div>

      <div className="border-t border-dashed border-black mt-2 mb-1"/>

      {/* ── INVOICE META ── */}
      <div className="text-[10px]">
        <div className="flex justify-between"><span>Bill No: {invoice.invoiceNumber}</span></div>
        <div className="flex justify-between">
          <span>Date: {new Date(invoice.businessDate).toLocaleDateString('en-IN')}</span>
          <span>Time: {new Date(invoice.businessDate).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}</span>
        </div>
        <div className="flex justify-between">
          <span>Cashier: {invoice.cashierName || invoice.cashierId?.slice(0, 8) || 'N/A'}</span>
          <span>Terminal: {localStorage.getItem('pos_terminal_code') || 'POS-01'}</span>
        </div>
        {invoice.customerName && (
          <div>Customer: {invoice.customerName} | {invoice.customerPhone || ''}</div>
        )}
      </div>

      <div className="border-t border-dashed border-black my-1"/>

      {/* ── ITEMS ── */}
      <div className="text-[10px]">
        <div className="flex font-bold border-b border-dashed border-black pb-0.5 mb-0.5">
          <span className="flex-1">Item</span>
          <span className="w-8 text-center">Qty</span>
          <span className="w-10 text-right">Rate</span>
          <span className="w-12 text-right">Amt</span>
        </div>
        {(invoice.items || []).map((item: any, idx: number) => (
          <div key={idx}>
            <div className="flex">
              <span className="flex-1 truncate">{item.name}</span>
              <span className="w-8 text-center">{item.quantity}</span>
              <span className="w-10 text-right">{fmt(item.unitPrice)}</span>
              <span className="w-12 text-right">{fmt((item.unitPrice * item.quantity) - (item.discountAmount || 0))}</span>
            </div>
            {item.discountAmount > 0 && (
              <div className="text-emerald-700 pl-2 text-[9px]">Discount: -{fmt(item.discountAmount)}</div>
            )}
          </div>
        ))}
      </div>

      <div className="border-t border-dashed border-black my-1"/>

      {/* ── BILL SUMMARY ── */}
      <div className="text-[10px] space-y-0.5">
        <div className="flex justify-between"><span>Sub Total</span><span>{fmt(invoice.subTotal || 0)}</span></div>
        {(invoice.discountAmount || 0) > 0 && (
          <div className="flex justify-between"><span>Discount</span><span>-{fmt(invoice.discountAmount)}</span></div>
        )}
        {(invoice.taxAmount || 0) > 0 && (
          <div className="flex justify-between"><span>GST (CGST+SGST)</span><span>+{fmt(invoice.taxAmount)}</span></div>
        )}
        {roundOffAmt !== 0 && (
          <div className="flex justify-between"><span>Round Off</span><span>{roundOffAmt > 0 ? '+' : ''}{fmt(roundOffAmt)}</span></div>
        )}
        <div className="flex justify-between font-bold text-[13px] border-t border-black border-dashed pt-1 mt-1">
          <span>NET PAYABLE</span><span>₹{netPayable}</span>
        </div>
      </div>

      <div className="border-t border-dashed border-black my-1"/>

      {/* ── PAYMENT DETAILS ── */}
      <div className="text-[10px] space-y-0.5">
        {tenderCash > 0 && <div className="flex justify-between"><span>Cash Tendered</span><span>{fmt(tenderCash)}</span></div>}
        {tenderUpi > 0 && <div className="flex justify-between"><span>UPI Paid</span><span>{fmt(tenderUpi)}</span></div>}
        {tenderCard > 0 && <div className="flex justify-between"><span>Card Paid</span><span>{fmt(tenderCard)}</span></div>}
        {(invoice.walletAmountUsed || 0) > 0 && <div className="flex justify-between"><span>Wallet Paid</span><span>{fmt(invoice.walletAmountUsed)}</span></div>}
        {changeDue > 0 && <div className="flex justify-between font-bold"><span>Change Due</span><span>{fmt(changeDue)}</span></div>}
      </div>

      {/* ── LOYALTY ── */}
      {invoice.customerName && (
        <>
          <div className="border-t border-dashed border-black my-1"/>
          <div className="text-[10px] space-y-0.5">
            {invoice.loyaltyPointsEarned > 0 && <div className="flex justify-between"><span>Today's Points Earned</span><span>+{fmt(invoice.loyaltyPointsEarned)}</span></div>}
            {invoice.loyaltyPointsBalance !== undefined && <div className="flex justify-between"><span>Total Points Balance</span><span>{fmt(invoice.loyaltyPointsBalance)}</span></div>}
          </div>
        </>
      )}

      {/* ── GST TAX SUMMARY ── */}
      {Object.keys(taxSummary).length > 0 && (
        <>
          <div className="border-t border-dashed border-black my-1"/>
          <div className="text-[9px]">
            <div className="font-bold mb-0.5">GST Summary</div>
            <div className="flex font-bold border-b border-dashed border-black pb-0.5">
              <span className="w-10">Slab</span>
              <span className="flex-1 text-right">Taxable Val</span>
              <span className="flex-1 text-right">CGST</span>
              <span className="flex-1 text-right">SGST</span>
            </div>
            {Object.values(taxSummary).map((slab, i) => (
              <div key={i} className="flex">
                <span className="w-10">{slab.rate}</span>
                <span className="flex-1 text-right">{fmt(slab.taxableValue)}</span>
                <span className="flex-1 text-right">{fmt(slab.cgst)}</span>
                <span className="flex-1 text-right">{fmt(slab.sgst)}</span>
              </div>
            ))}
          </div>
        </>
      )}

      {/* ── FOOTER ── */}
      <div className="border-t border-dashed border-black mt-2 mb-1"/>
      <div className="text-center text-[10px] pb-2">
        <p>Tax Invoice | GSTIN: {STORE.gstin}</p>
        <p className="mt-1">அனைத்தும் வாங்க நன்றிபிளுக்கு வாங்க</p>
        <p>Thank you for shopping with us!</p>
        <p>Visit Again!</p>
      </div>

      {/* Spacer for thermal cutter */}
      <div style={{ height: '20mm' }}/>
    </div>
  );
});
