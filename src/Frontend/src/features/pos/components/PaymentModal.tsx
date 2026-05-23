import React, { useState } from 'react';
import { CreditCard, Wallet, Banknote, QrCode, CheckCircle } from 'lucide-react';

export const PaymentModal = ({ isOpen, onClose, cartTotal, customer, onCompletePayment, isProcessing = false }: any) => {
  const [tenders, setTenders] = useState({ cash: '', upi: '', card: '', wallet: '' });
  const [useWalletMax, setUseWalletMax] = useState(false);

  React.useEffect(() => {
    if (isOpen) {
      setTenders({ cash: '', upi: '', card: '', wallet: '' });
      setUseWalletMax(false);
    }
  }, [isOpen]);

  if (!isOpen) return null;

  const cashNum = parseFloat(tenders.cash) || 0;
  const upiNum = parseFloat(tenders.upi) || 0;
  const cardNum = parseFloat(tenders.card) || 0;
  const walletNum = parseFloat(tenders.wallet) || 0;

  const totalTendered = cashNum + upiNum + cardNum + walletNum;
  const balanceDue = Math.max(0, cartTotal - totalTendered);
  const changeDue = Math.max(0, totalTendered - cartTotal);

  const handleWalletToggle = () => {
    if (!customer) return;
    if (useWalletMax) {
      setTenders({ ...tenders, wallet: '' });
      setUseWalletMax(false);
    } else {
      const walletToUse = Math.min(customer.walletBalance, cartTotal);
      setTenders({ ...tenders, wallet: walletToUse.toString() });
      setUseWalletMax(true);
    }
  };

  const handleComplete = () => {
    if (balanceDue > 0) {
      alert("Payment incomplete!");
      return;
    }
    onCompletePayment({
      cash: cashNum,
      upi: upiNum,
      card: cardNum,
      wallet: walletNum
    });
  };

  return (
    <div className="fixed inset-0 bg-black/60 z-[60] flex items-center justify-center p-4">
      <div className="bg-white rounded-xl shadow-2xl w-full max-w-2xl overflow-hidden flex flex-col">
        <div className="bg-slate-800 p-4 flex justify-between items-center text-white">
          <h2 className="text-xl font-bold">Complete Payment</h2>
          <button onClick={onClose} className="font-bold text-xl">&times;</button>
        </div>
        
        <div className="flex p-6">
          {/* Tender Inputs */}
          <div className="w-1/2 pr-6 border-r space-y-4">
             <div>
              <label className="flex items-center text-sm font-bold text-gray-700 mb-1"><Banknote className="w-4 h-4 mr-1 text-emerald-600"/> Cash Amount</label>
              <input type="text" inputMode="decimal" className="w-full p-2 border rounded focus:ring-2 outline-none text-lg font-bold" 
                     value={tenders.cash} onChange={e => setTenders({...tenders, cash: e.target.value})} />
            </div>
            <div>
              <label className="flex items-center text-sm font-bold text-gray-700 mb-1"><QrCode className="w-4 h-4 mr-1 text-blue-600"/> UPI / QR Amount</label>
              <input type="text" inputMode="decimal" className="w-full p-2 border rounded focus:ring-2 outline-none text-lg font-bold" 
                     value={tenders.upi} onChange={e => setTenders({...tenders, upi: e.target.value})} />
            </div>
            <div>
              <label className="flex items-center text-sm font-bold text-gray-700 mb-1"><CreditCard className="w-4 h-4 mr-1 text-slate-800"/> Card Amount</label>
              <input type="text" inputMode="decimal" className="w-full p-2 border rounded focus:ring-2 outline-none text-lg font-bold" 
                     value={tenders.card} onChange={e => setTenders({...tenders, card: e.target.value})} />
            </div>
          </div>

          {/* Totals & Wallet */}
          <div className="w-1/2 pl-6 flex flex-col">
            <div className="bg-slate-100 p-4 rounded-lg mb-4 text-center">
              <p className="text-gray-500 font-bold uppercase text-sm">Total Due</p>
              <p className="text-4xl font-black text-indigo-700">₹{cartTotal.toFixed(2)}</p>
            </div>

            {customer && customer.walletBalance > 0 && (
              <div className="border border-indigo-200 bg-indigo-50 p-3 rounded-lg mb-4 cursor-pointer hover:bg-indigo-100" onClick={handleWalletToggle}>
                <div className="flex justify-between items-center mb-1">
                  <p className="font-bold flex items-center text-indigo-900"><Wallet className="w-4 h-4 mr-1"/> Pay via Wallet</p>
                  <input type="checkbox" checked={useWalletMax} onChange={() => {}} className="w-4 h-4" />
                </div>
                <p className="text-sm text-indigo-700 font-bold">Balance: ₹{customer.walletBalance.toFixed(2)}</p>
                {useWalletMax && <p className="text-xs font-bold text-emerald-600 mt-1">Applying ₹{tenders.wallet.toFixed(2)}</p>}
              </div>
            )}

            <div className="mt-auto space-y-2">
              <div className="flex justify-between text-lg">
                <span className="text-gray-600">Remaining</span>
                <span className="font-bold text-red-500">₹{balanceDue.toFixed(2)}</span>
              </div>
              <div className="flex justify-between text-lg border-t pt-2 border-slate-300">
                <span className="text-gray-600">Change Due</span>
                <span className="font-bold text-emerald-600">₹{changeDue.toFixed(2)}</span>
              </div>
            </div>
          </div>
        </div>

        <div className="p-4 bg-gray-50 border-t flex justify-end">
          <button 
            onClick={handleComplete}
            disabled={balanceDue > 0 || isProcessing}
            className={`px-8 py-3 rounded text-white font-black text-xl flex items-center shadow ${
              balanceDue > 0 || isProcessing ? 'bg-gray-400 cursor-not-allowed' : 'bg-emerald-600 hover:bg-emerald-700'
            }`}
          >
            {isProcessing ? (
              <><span className="animate-spin mr-2 border-2 border-white border-t-transparent rounded-full w-5 h-5 inline-block"></span> PROCESSING...</>
            ) : (
              <><CheckCircle className="w-6 h-6 mr-2" /> COMPLETE INVOICE</>
            )}
          </button>
        </div>
      </div>
    </div>
  );
};
