import React, { useState, useEffect } from 'react';
import { Search, ShoppingCart, User, Plus, X, CreditCard, Wallet, Award, Tag, Trash2, PlusCircle, MinusCircle } from 'lucide-react';
import { CustomerRegistrationModal } from '../../crm/components/CustomerRegistrationModal';
import { PaymentModal } from './PaymentModal';
import { searchProducts } from '../../catalog/api/catalog.api';
import { searchCustomers, registerCustomer } from '../../crm/api/crm.api';
import { createInvoice } from '../api/pos.api';

export const PosTerminal = () => {
  const [customer, setCustomer] = useState<any>(null);
  const [isCustomerModalOpen, setCustomerModalOpen] = useState(false);
  const [promoCode, setPromoCode] = useState('');
  const [isPaymentModalOpen, setPaymentModalOpen] = useState(false);
  
  // Product Search State
  const [productQuery, setProductQuery] = useState('');
  const [searchResults, setSearchResults] = useState<any[]>([]);
  const [showProductDropdown, setShowProductDropdown] = useState(false);

  // Dynamic Cart State initializing empty
  const [cart, setCart] = useState<any>({
    items: [],
    subtotal: 0,
    totalDiscount: 0,
    taxTotal: 0,
    finalTotal: 0,
    appliedOfferNames: []
  });

  const recalculateCart = (items: any[]) => {
    let subtotal = items.reduce((sum: number, item: any) => sum + (item.qty * item.unitPrice), 0);
    let totalDiscount = 0;
    let appliedOffers: string[] = [];

    // Mock/Demo offer logic: 10% off Atta (Line Level)
    const evaluatedItems = items.map((item: any) => {
      let discount = 0;
      let offerName = null;
      if (item.name.toLowerCase().includes('atta')) {
        discount = item.lineTotal * 0.10; // 10%
        offerName = '10% OFF Staples';
        if (!appliedOffers.includes(offerName)) appliedOffers.push(offerName);
      }
      return { ...item, discountAmount: discount, finalLineTotal: item.lineTotal - discount, appliedOfferName: offerName };
    });

    totalDiscount += evaluatedItems.reduce((sum: number, item: any) => sum + item.discountAmount, 0);

    // Mock logic: Flat 50 off if promo code applied (Bill Level)
    if (promoCode === 'SAVE50') {
      totalDiscount += 50;
      appliedOffers.push('SAVE50 Promo');
    }

    let taxTotal = subtotal * 0.05; // 5% GST tax rate
    let finalTotal = Math.max(0, subtotal - totalDiscount + taxTotal);

    setCart({
      items: evaluatedItems,
      subtotal,
      totalDiscount,
      taxTotal,
      finalTotal,
      appliedOfferNames: appliedOffers
    });
  };

  // Evaluate whenever promo code changes
  useEffect(() => {
    recalculateCart(cart.items);
  }, [promoCode]);

  const addProductToCart = (product: any) => {
    const existing = cart.items.find((item: any) => item.productId === product.id);
    let updatedItems = [];

    if (existing) {
      updatedItems = cart.items.map((item: any) =>
        item.productId === product.id 
          ? { ...item, qty: item.qty + 1, lineTotal: (item.qty + 1) * item.unitPrice } 
          : item
      );
    } else {
      updatedItems = [
        ...cart.items,
        {
          id: Math.random().toString(),
          productId: product.id,
          name: product.name,
          qty: 1,
          unitPrice: product.sellingPrice,
          lineTotal: product.sellingPrice,
          discountAmount: 0,
          finalLineTotal: product.sellingPrice,
          appliedOfferName: null
        }
      ];
    }

    recalculateCart(updatedItems);
  };

  const updateItemQty = (productId: string, delta: number) => {
    const updatedItems = cart.items.map((item: any) => {
      if (item.productId === productId) {
        const newQty = Math.max(1, item.qty + delta);
        return { ...item, qty: newQty, lineTotal: newQty * item.unitPrice };
      }
      return item;
    });
    recalculateCart(updatedItems);
  };

  const removeItem = (productId: string) => {
    const updatedItems = cart.items.filter((item: any) => item.productId !== productId);
    recalculateCart(updatedItems);
  };

  const handleProductSearch = async (e: React.KeyboardEvent<HTMLInputElement>) => {
    if (e.key === 'Enter') {
      const val = productQuery.trim();
      if (!val) return;

      try {
        const results = await searchProducts(val);
        if (results.length === 1) {
          addProductToCart(results[0]);
          setProductQuery('');
          setSearchResults([]);
          setShowProductDropdown(false);
        } else if (results.length > 1) {
          setSearchResults(results);
          setShowProductDropdown(true);
        } else {
          alert('Product not found.');
        }
      } catch (err) {
        console.error('Error searching products:', err);
      }
    }
  };

  const handleCustomerSearch = async (e: React.KeyboardEvent<HTMLInputElement>) => {
    if (e.key === 'Enter') {
      const val = e.currentTarget.value.trim();
      if (!val) return;
      try {
        const results = await searchCustomers(val);
        if (results.length > 0) {
          const cust = results[0];
          setCustomer({
            id: cust.id,
            name: cust.name,
            phone: cust.phone,
            walletBalance: cust.walletBalance,
            points: cust.loyaltyPoints,
            tier: cust.tierName
          });
        } else {
          alert('Customer not found. Click "+" to register a new customer!');
        }
      } catch (err) {
        console.error('Error searching customer:', err);
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
                <p className="text-xs text-gray-600 flex items-center justify-end"><Wallet className="w-3 h-3 mr-1 text-blue-500"/> ₹{customer.walletBalance}</p>
                <p className="text-xs text-gray-600 flex items-center justify-end"><Award className="w-3 h-3 mr-1 text-orange-500"/> {customer.points} Pts</p>
              </div>
              <button onClick={() => setCustomer(null)} className="text-gray-400 hover:text-red-500 ml-2"><X className="w-5 h-5"/></button>
            </div>
          )}
        </div>

        {/* Product Search / Barcode Input Bar */}
        <div className="p-4 bg-slate-50 border-b border-slate-200 relative">
          <div className="relative">
            <Search className="absolute left-3 top-2.5 text-slate-400 w-5 h-5" />
            <input 
              type="text"
              placeholder="Scan Barcode or Type Product Name (Press Enter)..."
              className="w-full pl-10 pr-4 py-2 border border-slate-300 rounded-lg outline-none focus:ring-2 focus:ring-blue-500 font-bold text-slate-850"
              value={productQuery}
              onChange={(e) => setProductQuery(e.target.value)}
              onKeyDown={handleProductSearch}
            />
          </div>

          {/* Search Dropdown Overlay */}
          {showProductDropdown && searchResults.length > 0 && (
            <div className="absolute left-4 right-4 mt-1 bg-white border border-slate-200 rounded-lg shadow-xl z-50 max-h-60 overflow-y-auto">
              <div className="p-2 border-b border-slate-100 flex justify-between items-center bg-slate-50">
                <span className="text-xs font-semibold text-slate-500">Multiple items found. Select one:</span>
                <button onClick={() => setShowProductDropdown(false)} className="text-slate-400 hover:text-slate-600 text-xs font-bold">Close</button>
              </div>
              {searchResults.map((p) => (
                <div 
                  key={p.id}
                  onClick={() => {
                    addProductToCart(p);
                    setProductQuery('');
                    setSearchResults([]);
                    setShowProductDropdown(false);
                  }}
                  className="px-4 py-2.5 hover:bg-blue-50 cursor-pointer flex justify-between items-center border-b border-slate-100 transition"
                >
                  <div>
                    <p className="font-bold text-slate-800">{p.name}</p>
                    <p className="text-xs text-slate-400">Code: {p.productCode} | Barcode: {p.primaryBarcode || 'N/A'}</p>
                  </div>
                  <span className="font-extrabold text-blue-600">₹{p.sellingPrice.toFixed(2)}</span>
                </div>
              ))}
            </div>
          )}
        </div>

        {/* Cart Table with Dynamic Controls */}
        <div className="p-0 flex-1 overflow-y-auto">
          {cart.items.length === 0 ? (
            <div className="h-full flex flex-col items-center justify-center text-slate-400">
              <ShoppingCart className="w-16 h-16 mb-2 stroke-1" />
              <p className="font-semibold">Billing cart is empty</p>
              <p className="text-xs">Scan items or search products above to start checkout</p>
            </div>
          ) : (
            <table className="w-full text-left">
              <thead className="bg-slate-100 sticky top-0 border-b">
                <tr>
                  <th className="p-3">Item</th>
                  <th className="p-3 text-center">Qty</th>
                  <th className="p-3 text-right">Price</th>
                  <th className="p-3 text-right">Total</th>
                  <th className="p-3 text-center w-16"></th>
                </tr>
              </thead>
              <tbody>
                {cart.items.map((item: any) => (
                  <tr key={item.id} className="border-b hover:bg-slate-50/50">
                    <td className="p-3">
                      <p className="font-bold text-slate-800">{item.name}</p>
                      {item.appliedOfferName && (
                        <p className="text-xs text-emerald-600 flex items-center font-bold bg-emerald-50 w-max px-2 py-0.5 rounded mt-1">
                          <Tag className="w-3 h-3 mr-1" /> {item.appliedOfferName}
                        </p>
                      )}
                    </td>
                    <td className="p-3">
                      <div className="flex items-center justify-center gap-2">
                        <button onClick={() => updateItemQty(item.productId, -1)} className="text-slate-400 hover:text-indigo-600 transition">
                          <MinusCircle className="w-6 h-6" />
                        </button>
                        <span className="font-black text-xl w-8 text-center">{item.qty}</span>
                        <button onClick={() => updateItemQty(item.productId, 1)} className="text-slate-400 hover:text-indigo-600 transition">
                          <PlusCircle className="w-6 h-6" />
                        </button>
                      </div>
                    </td>
                    <td className="p-3 text-right font-medium">₹{item.unitPrice.toFixed(2)}</td>
                    <td className="p-3 text-right">
                      {item.discountAmount > 0 && <p className="text-xs text-slate-400 line-through">₹{item.lineTotal.toFixed(2)}</p>}
                      <p className="font-black text-xl text-slate-800">₹{item.finalLineTotal.toFixed(2)}</p>
                    </td>
                    <td className="p-3 text-center">
                      <button onClick={() => removeItem(item.productId)} className="text-slate-300 hover:text-red-500 transition">
                        <Trash2 className="w-5 h-5" />
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
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

          <div className="flex justify-between text-lg mb-2"><span>Subtotal</span><span className="font-bold text-slate-700">₹{cart.subtotal.toFixed(2)}</span></div>
          <div className="flex justify-between text-lg mb-2 text-emerald-600">
            <span>Discounts</span>
            <span className="font-bold">-₹{cart.totalDiscount.toFixed(2)}</span>
          </div>
          {cart.appliedOfferNames.length > 0 && (
             <div className="text-xs text-emerald-600 mb-2 italic">Applied: {cart.appliedOfferNames.join(', ')}</div>
          )}
          
          <div className="flex justify-between text-lg mb-6"><span>Tax (GST)</span><span>₹{cart.taxTotal.toFixed(2)}</span></div>
          
          <div className="flex justify-between text-4xl font-black text-indigo-700 mb-8 border-t pt-4">
            <span>Total</span><span>₹{cart.finalTotal.toFixed(2)}</span>
          </div>

          {/* Payment Methods */}
          <div className="grid grid-cols-2 gap-4">
            <button 
              disabled={cart.items.length === 0}
              className="bg-emerald-600 text-white p-4 rounded-lg font-bold text-xl shadow hover:bg-emerald-700 disabled:opacity-50 disabled:cursor-not-allowed" 
              onClick={() => setPaymentModalOpen(true)}
            >
              CASH
            </button>
            <button 
              disabled={cart.items.length === 0}
              className="bg-blue-600 text-white p-4 rounded-lg font-bold text-xl shadow hover:bg-blue-700 disabled:opacity-50 disabled:cursor-not-allowed"
            >
              UPI / QR
            </button>
            <button 
              disabled={cart.items.length === 0}
              className="bg-slate-800 text-white p-4 rounded-lg font-bold text-xl shadow hover:bg-slate-900 disabled:opacity-50 disabled:cursor-not-allowed"
            >
              CARD
            </button>
            
            <button 
              className={`p-4 rounded-lg font-bold text-xl shadow flex flex-col items-center justify-center ${!customer || customer.walletBalance <= 0 || cart.items.length === 0 ? 'bg-gray-200 text-gray-400 cursor-not-allowed' : 'bg-indigo-600 text-white hover:bg-indigo-700'}`}
              disabled={!customer || customer.walletBalance <= 0 || cart.items.length === 0}
              onClick={() => setPaymentModalOpen(true)}
            >
              WALLET
              {customer && <span className="text-sm">Bal: ₹{customer.walletBalance}</span>}
            </button>
          </div>
        </div>
      <PaymentModal 
        isOpen={isPaymentModalOpen} 
        onClose={() => setPaymentModalOpen(false)} 
        cartTotal={cart.finalTotal}
        customer={customer}
        onCompletePayment={async (tenders: any) => {
          try {
            // Generate dynamic invoice payload matching CreateInvoiceCommand
            const payload = {
              invoiceNumber: `INV-${localStorage.getItem('pos_terminal_code') || 'POS-01'}-${Date.now().toString().slice(-6)}`,
              terminalId: '00000000-0000-0000-0000-000000000001', // Default Logical Terminal Ref GUID
              customerId: customer?.id || undefined,
              promoCode: promoCode || undefined,
              walletAmountUsed: tenders.wallet || 0,
              cashAmount: tenders.cash || 0,
              upiAmount: tenders.upi || 0,
              cardAmount: tenders.card || 0,
              items: cart.items.map((item: any) => ({
                productId: item.productId,
                quantity: item.qty,
                unitPrice: item.unitPrice
              }))
            };

            await createInvoice(payload);

            setPaymentModalOpen(false);
            alert(`Invoice ${payload.invoiceNumber} Created Successfully in Database!\nFinancial journals, tax lines & loyalty ledger recorded!`);
            setCart({ items: [], subtotal: 0, totalDiscount: 0, taxTotal: 0, finalTotal: 0, appliedOfferNames: [] });
            setCustomer(null);
            setPromoCode('');
          } catch (err: any) {
            console.error('Checkout error:', err);
            alert('Failed to complete invoice in DB: ' + (err.response?.data?.message || err.response?.data?.Message || err.message));
          }
        }} 
      />

      </div>

      <CustomerRegistrationModal 
        isOpen={isCustomerModalOpen} 
        onClose={() => setCustomerModalOpen(false)} 
        onRegister={async (newCust: any) => {
          try {
            const customerId = await registerCustomer({
              phone: newCust.phone,
              name: newCust.name,
              tamilName: newCust.tamilName || undefined,
              dob: newCust.dob || undefined,
              marketingConsent: newCust.marketingConsent
            });
            // Automatically select registered customer
            setCustomer({
              id: customerId,
              name: newCust.name,
              phone: newCust.phone,
              walletBalance: 0,
              points: 0,
              tier: 'Base'
            });
            alert('Customer registered successfully!');
          } catch (err) {
            console.error('Error registering customer:', err);
            throw err;
          }
        }} 
      />
    </div>
  );
};
