import React, { useState, useEffect } from 'react';
import { Search, ShoppingCart, User, Plus, X, CreditCard, Wallet, Award, Tag } from 'lucide-react';
import { CustomerRegistrationModal } from '../../crm/components/CustomerRegistrationModal';
import { PaymentModal } from './PaymentModal';

export const PosTerminal = () => {
  const [customer, setCustomer] = useState<any>(null);
  const [isCustomerModalOpen, setCustomerModalOpen] = useState(false);
  const [promoCode, setPromoCode] = useState('');
  const [isPaymentModalOpen, setPaymentModalOpen] = useState(false);
  
  // Mock Cart State mimicking CartEvaluationDto
  const [cart, setCart] = useState<any>({
    items: [
      { id: '1', productId: 'p1', name: 'Aashirvaad Atta 5kg', qty: 2, unitPrice: 200, lineTotal: 400, discountAmount: 0, finalLineTotal: 400, appliedOfferName: null },
      { id: '2', productId: 'p2', name: 'Tata Salt 1kg', qty: 1, unitPrice: 20, lineTotal: 20, discountAmount: 0, finalLineTotal: 20, appliedOfferName: null }
    ],
    subtotal: 420,
    totalDiscount: 0,
    taxTotal: 21,
    finalTotal: 441,
    appliedOfferNames: []
  });

  // Mock Offer Engine Evaluation
  const evaluateCart = () => {
    let newCart = { ...cart };
    let subtotal = newCart.items.reduce((sum: number, item: any) => sum + (item.qty * item.unitPrice), 0);
    let totalDiscount = 0;
    let appliedOffers: string[] = [];

    // Mock logic: 10% off Atta (Line Level)
    newCart.items = newCart.items.map((item: any) => {
      let discount = 0;
      let offerName = null;
      if (item.productId === 'p1') {
        discount = item.lineTotal * 0.10; // 10%
        offerName = '10% OFF Staples';
        if (!appliedOffers.includes(offerName)) appliedOffers.push(offerName);
      }
      return { ...item, discountAmount: discount, finalLineTotal: item.lineTotal - discount, appliedOfferName: offerName };
    });

    totalDiscount += newCart.items.reduce((sum: number, item: any) => sum + item.discountAmount, 0);

    // Mock logic: Flat 50 off if promo code applied (Bill Level)
    if (promoCode === 'SAVE50') {
      totalDiscount += 50;
      appliedOffers.push('SAVE50 Promo');
    }

    newCart.subtotal = subtotal;
    newCart.totalDiscount = totalDiscount;
    newCart.finalTotal = subtotal - totalDiscount + newCart.taxTotal;
    newCart.appliedOfferNames = appliedOffers;
    
    setCart(newCart);
  };

  // Evaluate whenever promo code changes
  useEffect(() => {
    evaluateCart();
  }, [promoCode]);


  const handleCustomerSearch = (e: React.KeyboardEvent<HTMLInputElement>) => {
    if (e.key === 'Enter') {
      const val = e.currentTarget.value;
      if (val === '9988776655') {
        setCustomer({ id: '1', name: 'Rahul Sharma', phone: '9988776655', walletBalance: 500, points: 120, tier: 'Gold' });
      }
    }
  };

  return (
    <div className="flex h-screen bg-slate-100">
      {/* Left: Product/Cart Panel */}
      <div className="w-2/3 flex flex-col border-r border-slate-300 bg-white">
        
        {/* CRM Top Bar */}
        <div className="p-4 bg-indigo-50 border-b border-indigo-100 flex items-center justify-between">
          <div className="flex items-center w-1/2 relative">
            <User className="absolute left-3 text-indigo-400" />
            <input 
              type="text" 
              placeholder="F3: Search Customer (Phone/Name)..." 
              className="w-full pl-10 p-2 rounded-l border border-indigo-200 outline-none focus:ring-2 ring-indigo-500 font-bold"
              onKeyDown={handleCustomerSearch}
            />
            <button 
              onClick={() => setCustomerModalOpen(true)}
              className="bg-indigo-600 text-white p-2 rounded-r hover:bg-indigo-700 flex items-center"
            >
              <Plus className="w-5 h-5" />
            </button>
          </div>

          {customer && (
            <div className="flex items-center gap-4 bg-white p-2 rounded shadow-sm border border-indigo-200">
              <div>
                <p className="font-bold text-slate-800 text-sm">{customer.name} <span className="bg-yellow-100 text-yellow-800 text-xs px-1 rounded ml-1">{customer.tier}</span></p>
                <p className="text-xs text-gray-500">{customer.phone}</p>
              </div>
              <div className="text-right border-l pl-3">
                <p className="text-xs text-gray-600 flex items-center justify-end"><Wallet className="w-3 h-3 mr-1 text-blue-500"/> â‚¹{customer.walletBalance}</p>
                <p className="text-xs text-gray-600 flex items-center justify-end"><Award className="w-3 h-3 mr-1 text-orange-500"/> {customer.points} Pts</p>
              </div>
              <button onClick={() => setCustomer(null)} className="text-gray-400 hover:text-red-500 ml-2"><X className="w-5 h-5"/></button>
            </div>
          )}
        </div>

        {/* Cart Table with Offers */}
        <div className="p-0 flex-1 overflow-y-auto">
          <table className="w-full text-left">
            <thead className="bg-slate-100 sticky top-0 border-b">
              <tr>
                <th className="p-3">Item</th>
                <th className="p-3 text-center">Qty</th>
                <th className="p-3 text-right">Price</th>
                <th className="p-3 text-right">Total</th>
              </tr>
            </thead>
            <tbody>
              {cart.items.map((item: any) => (
                <tr key={item.id} className="border-b">
                  <td className="p-3">
                    <p className="font-bold text-lg">{item.name}</p>
                    {item.appliedOfferName && (
                      <p className="text-xs text-emerald-600 flex items-center font-bold bg-emerald-50 w-max px-2 py-0.5 rounded mt-1">
                        <Tag className="w-3 h-3 mr-1" /> {item.appliedOfferName}
                      </p>
                    )}
                  </td>
                  <td className="p-3 text-center font-bold text-xl">{item.qty}</td>
                  <td className="p-3 text-right">â‚¹{item.unitPrice}</td>
                  <td className="p-3 text-right">
                    {item.discountAmount > 0 && <p className="text-sm text-gray-400 line-through">â‚¹{item.lineTotal}</p>}
                    <p className="font-black text-xl text-slate-800">â‚¹{item.finalLineTotal}</p>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>

      {/* Right: Payment Panel */}
      <div className="w-1/3 flex flex-col bg-slate-50 p-6">
        <h2 className="text-2xl font-black text-slate-800 mb-6 border-b pb-2">Payment</h2>
        
        <div className="flex-1">
          {/* Promo Code Input */}
          <div className="flex mb-6">
            <input 
              type="text" 
              placeholder="Promo Code" 
              className="w-full p-2 border border-r-0 rounded-l outline-none focus:border-indigo-500 font-bold uppercase"
              value={promoCode}
              onChange={(e) => setPromoCode(e.target.value.toUpperCase())}
            />
            <button className="bg-slate-800 text-white px-4 rounded-r font-bold hover:bg-slate-700">Apply</button>
          </div>

          <div className="flex justify-between text-lg mb-2"><span>Subtotal</span><span className="font-bold text-slate-700">â‚¹{cart.subtotal.toFixed(2)}</span></div>
          <div className="flex justify-between text-lg mb-2 text-emerald-600">
            <span>Discounts</span>
            <span className="font-bold">-â‚¹{cart.totalDiscount.toFixed(2)}</span>
          </div>
          {cart.appliedOfferNames.length > 0 && (
             <div className="text-xs text-emerald-600 mb-2 italic">Applied: {cart.appliedOfferNames.join(', ')}</div>
          )}
          
          <div className="flex justify-between text-lg mb-6"><span>Tax (GST)</span><span>â‚¹{cart.taxTotal.toFixed(2)}</span></div>
          
          <div className="flex justify-between text-4xl font-black text-indigo-700 mb-8 border-t pt-4">
            <span>Total</span><span>â‚¹{cart.finalTotal.toFixed(2)}</span>
          </div>

          {/* Payment Methods */}
          <div className="grid grid-cols-2 gap-4">
            <button className="bg-emerald-600 text-white p-4 rounded-lg font-bold text-xl shadow hover:bg-emerald-700" onClick={() => setPaymentModalOpen(true)}>CASH</button>
            <button className="bg-blue-600 text-white p-4 rounded-lg font-bold text-xl shadow hover:bg-blue-700">UPI / QR</button>
            <button className="bg-slate-800 text-white p-4 rounded-lg font-bold text-xl shadow hover:bg-slate-900">CARD</button>
            
            <button 
              className={`p-4 rounded-lg font-bold text-xl shadow flex flex-col items-center justify-center ${!customer || customer.walletBalance <= 0 ? 'bg-gray-200 text-gray-400 cursor-not-allowed' : 'bg-indigo-600 text-white hover:bg-indigo-700'}`}
              disabled={!customer || customer.walletBalance <= 0}
              onClick={() => setPaymentModalOpen(true)}
            >
              WALLET
              {customer && <span className="text-sm">Bal: â‚¹{customer.walletBalance}</span>}
            </button>
          </div>
        </div>
      <PaymentModal 
        isOpen={isPaymentModalOpen} 
        onClose={() => setPaymentModalOpen(false)} 
        cartTotal={cart.finalTotal}
        customer={customer}
        onCompletePayment={(tenders: any) => {
          setPaymentModalOpen(false);
          alert('Invoice Created! Backend hit: OfferEngine -> LoyaltyService -> WalletService -> DB. Points Awarded!');
          setCart({...cart, items: [], subtotal: 0, totalDiscount: 0, taxTotal: 0, finalTotal: 0});
          setCustomer(null);
        }} 
      />

      </div>

      <CustomerRegistrationModal 
        isOpen={isCustomerModalOpen} 
        onClose={() => setCustomerModalOpen(false)} 
        onRegister={() => {}} 
      />
    </div>
  );
};

