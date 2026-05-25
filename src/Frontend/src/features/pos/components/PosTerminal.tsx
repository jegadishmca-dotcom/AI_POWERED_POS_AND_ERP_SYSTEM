import React, { useState, useEffect, useRef } from 'react';
import { Search, ShoppingCart, User, Plus, X, CreditCard, Wallet, Award, Tag, Trash2, PlusCircle, MinusCircle, Hand, ShieldAlert, Printer } from 'lucide-react';
import { CustomerRegistrationModal } from '../../crm/components/CustomerRegistrationModal';
import { PaymentModal } from './PaymentModal';
import { searchProducts } from '../../catalog/api/catalog.api';
import { searchCustomers, registerCustomer } from '../../crm/api/crm.api';
import { createInvoice, closeShift, getZReport } from '../api/pos.api';
import { printReceipt } from '../utils/printReceipt';
import { printZReport } from '../utils/printZReport';
import { useBarcodeScanner } from '../hooks/useBarcodeScanner';
import { usePosKeyboardShortcuts } from '../hooks/usePosKeyboardShortcuts';
import { HoldResumeModal } from './modals/HoldResumeModal';
import { ManagerPinModal } from './modals/ManagerPinModal';
import { ReprintModal } from './modals/ReprintModal';
import { OpenShiftModal } from './modals/OpenShiftModal';
import { CloseShiftModal } from './modals/CloseShiftModal';
import { posDb } from '../db/pos.db';
import { useAuthStore } from '../../auth/store/auth.store';

export const PosTerminal = () => {
  const [customer, setCustomer] = useState<any>(null);
  const [customerQuery, setCustomerQuery] = useState('');
  const [isCustomerModalOpen, setCustomerModalOpen] = useState(false);
  const [promoCode, setPromoCode] = useState('');
  const [isPaymentModalOpen, setPaymentModalOpen] = useState(false);
  const [completedInvoice, setCompletedInvoice] = useState<any>(null);
  
  const [isProcessing, setIsProcessing] = useState(false);
  
  // Modals & Hooks State
  const [isHoldModalOpen, setHoldModalOpen] = useState(false);
  const [isReprintModalOpen, setReprintModalOpen] = useState(false);
  const [isManagerModalOpen, setManagerModalOpen] = useState(false);
  const [managerAction, setManagerAction] = useState<any>(null);
  const customerInputRef = useRef<HTMLInputElement>(null);
  const productInputRef = useRef<HTMLInputElement>(null);

  // Shift Management State
  const [activeSession, setActiveSession] = useState<any>(null);
  const [isOpenShiftModalOpen, setOpenShiftModalOpen] = useState(false);
  const [isCloseShiftModalOpen, setCloseShiftModalOpen] = useState(false);
  const { user } = useAuthStore();
  const terminalId = localStorage.getItem('pos_terminal_id') || '00000000-0000-0000-0000-000000000001';
  const cashierId = user?.id || '00000000-0000-0000-0000-000000000001';

  useEffect(() => {
    // Focus barcode scanner input on mount
    productInputRef.current?.focus();
    
    // Check for active shift
    const fetchSession = async () => {
      try {
        const res = await fetch(`/api/pos/session/current?terminalId=${terminalId}&cashierId=${cashierId}`);
        if (res.ok) {
          const sessionData = await res.json();
          if (sessionData && sessionData.status === 'OPEN') {
            setActiveSession(sessionData);
          } else {
            setOpenShiftModalOpen(true);
          }
        } else {
          setOpenShiftModalOpen(true);
        }
      } catch (err) {
        console.error('Failed to fetch session', err);
        setOpenShiftModalOpen(true); // Default to forcing open shift on network error
      }
    };
    fetchSession();
  }, []);
  
  const handleOpenShift = async (openingCash: number) => {
    try {
      const payload = {
        terminalId,
        cashierId,
        openingFloatCash: openingCash
      };
      const res = await fetch('/api/pos/session/open', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload)
      });
      if (res.ok) {
        const sessionId = await res.json();
        setActiveSession({ id: sessionId, terminalId, cashierId, openingFloatCash: openingCash, status: 'OPEN' });
        setOpenShiftModalOpen(false);
      } else {
        alert('Failed to open shift.');
      }
    } catch (err) {
      console.error('Error opening shift', err);
      alert('Error opening shift');
    }
  };

  const handleCloseShift = async (closingCash: number) => {
    try {
      await closeShift({
        terminalId,
        cashierId,
        closingFloatCash: closingCash,
        status: 'CLOSED'
      });
      
      const report = await getZReport(terminalId, new Date().toISOString(), cashierId);
      printZReport(report, activeSession?.openingFloatCash || 0, closingCash, user?.fullName || 'Cashier');
      
      setActiveSession(null);
      setCloseShiftModalOpen(false);
      setOpenShiftModalOpen(true);
    } catch (err) {
      console.error('Error closing shift:', err);
      alert('Failed to close shift. Please check network.');
    }
  };

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

  const recalculateCart = async (items: any[]) => {
    if (items.length === 0) {
      setCart({ items: [], subtotal: 0, totalDiscount: 0, taxTotal: 0, finalTotal: 0, appliedOfferNames: [] });
      return;
    }

    try {
      const payload = {
        items: items.map(i => ({ productId: i.productId, quantity: i.qty })),
        promoCode: promoCode,
        customerId: customer?.id
      };

      const res = await fetch('/api/pos/calculate-cart', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload)
      });

      if (!res.ok) throw new Error('API failed');

      const data = await res.json();
      
      // Map API response back to UI cart format
      const evaluatedItems = items.map((origItem: any) => {
        const calcItem = data.items.find((i: any) => i.productId === origItem.productId);
        if (!calcItem) return origItem;
        return {
          ...origItem,
          discountAmount: calcItem.discountAmount,
          finalLineTotal: calcItem.finalLineTotal,
          appliedOfferName: calcItem.appliedOfferName,
          cgstRate: calcItem.cgstRate,
          sgstRate: calcItem.sgstRate
        };
      });

      setCart({
        items: evaluatedItems,
        subtotal: data.subTotal,
        totalDiscount: data.totalDiscount,
        taxTotal: data.taxTotal,
        finalTotal: data.finalTotal,
        appliedOfferNames: data.appliedOfferNames
      });

    } catch (err) {
      console.warn('Backend calculation failed, falling back to basic offline calculation', err);
      // Basic offline fallback (no promos, just basic tax)
      let subtotal = items.reduce((sum: number, item: any) => sum + (item.qty * item.unitPrice), 0);
      let taxTotal = items.reduce((sum: number, item: any) => {
          const itemTaxRate = (item.cgstRate || 0) + (item.sgstRate || 0);
          return sum + ((item.qty * item.unitPrice) * (itemTaxRate / 100));
      }, 0);

      setCart({
        items: items.map(i => ({ ...i, finalLineTotal: i.qty * i.unitPrice, discountAmount: 0, appliedOfferName: null })),
        subtotal,
        totalDiscount: 0,
        taxTotal,
        finalTotal: subtotal + taxTotal,
        appliedOfferNames: []
      });
    }
  };

  // Evaluate whenever promo code changes
  useEffect(() => {
    recalculateCart(cart.items);
  }, [promoCode]);

  const addProductToCart = (product: any, overrideQty?: number) => {
    const existing = cart.items.find((item: any) => item.productId === product.id);
    let updatedItems = [];
    const qtyToAdd = overrideQty !== undefined ? overrideQty : 1;

    if (existing) {
      updatedItems = cart.items.map((item: any) =>
        item.productId === product.id 
          ? { ...item, qty: item.qty + qtyToAdd, lineTotal: (item.qty + qtyToAdd) * item.unitPrice } 
          : item
      );
    } else {
      updatedItems = [
        ...cart.items,
        {
          id: Math.random().toString(),
          productId: product.id,
          name: product.name,
          qty: qtyToAdd,
          unitPrice: product.sellingPrice,
          lineTotal: product.sellingPrice * qtyToAdd,
          discountAmount: 0,
          finalLineTotal: product.sellingPrice * qtyToAdd,
          appliedOfferName: null,
          cgstRate: product.cgstRate || 0,
          sgstRate: product.sgstRate || 0,
          isWeighable: product.isWeighable || false
        }
      ];
    }

    recalculateCart(updatedItems);
  };

  const updateItemQtyExact = (productId: string, newQty: number) => {
    const updatedItems = cart.items.map((item: any) => {
      if (item.productId === productId) {
        return { ...item, qty: newQty, lineTotal: newQty * item.unitPrice };
      }
      return item;
    });
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

  // Barcode Scanner Integration
  useBarcodeScanner(async (barcode: string, weight?: number) => {
    try {
        const results = await searchProducts(barcode);
        const product = results.find(p => p.primaryBarcode === barcode || p.productCode === barcode);
        if (product) {
            addProductToCart(product, weight);
        } else {
            alert('Barcode not found: ' + barcode);
        }
    } catch (err) {
        console.error('Barcode lookup failed', err);
    }
  });

  // Keyboard Shortcuts
  usePosKeyboardShortcuts({
    onF1Search: () => customerInputRef.current?.focus(),
    onF2Product: () => productInputRef.current?.focus(),
    onF11Payment: () => {
      if (cart.items.length > 0) setPaymentModalOpen(true);
    },
    onF9Park: () => {
      if (cart.items.length > 0) setHoldModalOpen(true);
    },
    onF10Reprint: () => setReprintModalOpen(true)
  });

  const handleHoldCart = async () => {
    if (cart.items.length === 0) return;
    const holdInvoice = {
        id: Math.random().toString(),
        invoiceNumber: `HOLD-${Date.now()}`,
        businessDate: new Date().toISOString(),
        items: cart.items,
        totalAmount: cart.finalTotal
    };
    await posDb.held_invoices.put(holdInvoice as any);
    setCart({ items: [], subtotal: 0, totalDiscount: 0, taxTotal: 0, finalTotal: 0, appliedOfferNames: [] });
    setCustomer(null);
    setCustomerQuery('');
    setTimeout(() => {
        alert('Cart parked successfully (F9).');
    }, 100);
  };

  const handleResumeCart = (invoice: any) => {
      setCart({
          items: invoice.items,
          subtotal: 0, 
          totalDiscount: 0,
          taxTotal: 0,
          finalTotal: 0,
          appliedOfferNames: []
      });
      recalculateCart(invoice.items);
      setHoldModalOpen(false);
  };

  const requestManagerOverride = (action: string, onSuccess: () => void) => {
      setManagerAction({ name: action, callback: onSuccess });
      setManagerModalOpen(true);
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
      const val = customerQuery.trim();
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
              ref={customerInputRef}
              type="text" 
              placeholder="F1: Search Customer (Phone/Name)..." 
              className="w-full pl-10 p-2 rounded-l border border-indigo-200 outline-none focus:ring-2 ring-indigo-500 font-bold"
              value={customerQuery}
              onChange={(e) => setCustomerQuery(e.target.value)}
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
              <button onClick={() => { setCustomer(null); setCustomerQuery(''); }} className="text-gray-400 hover:text-red-500 ml-2"><X className="w-5 h-5"/></button>
            </div>
          )}
        </div>

        {/* Product Search / Barcode Input Bar */}
        <div className="p-4 bg-slate-50 border-b border-slate-200 relative">
          <div className="relative">
            <Search className="absolute left-3 top-2.5 text-slate-400 w-5 h-5" />
            <input 
              ref={productInputRef}
              type="text"
              placeholder="F2: Scan Barcode or Type Product Name (Press Enter)..."
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
                        <input 
                          type="number"
                          min="1"
                          className="font-black text-lg w-16 text-center border border-slate-200 rounded focus:outline-none focus:border-indigo-500 bg-white"
                          value={item.qty}
                          onChange={(e) => {
                            const val = parseInt(e.target.value, 10);
                            if (!isNaN(val) && val > 0) {
                              updateItemQtyExact(item.productId, val);
                            }
                          }}
                        />
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
                      <button onClick={() => requestManagerOverride('Void Item', () => removeItem(item.productId))} className="text-slate-300 hover:text-red-500 transition" title="Void Item (Manager)">
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
        <div className="flex justify-between items-center mb-6 border-b pb-2">
          <h2 className="text-2xl font-black text-slate-800">Payment</h2>
          {activeSession && (
            <button 
              onClick={() => setCloseShiftModalOpen(true)}
              className="bg-red-100 text-red-600 px-4 py-2 rounded-lg font-bold hover:bg-red-200 transition-colors shadow-sm"
            >
              Close Shift
            </button>
          )}
        </div>
        
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
            <button className="bg-slate-800 text-white px-4 rounded-r font-bold hover:bg-slate-700" onClick={() => recalculateCart(cart.items)}>Apply</button>
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
          <div className="mt-4">
            <button 
              disabled={cart.items.length === 0}
              className="w-full bg-emerald-600 text-white p-4 rounded-lg font-black text-2xl shadow-lg hover:bg-emerald-700 disabled:opacity-50 disabled:cursor-not-allowed flex items-center justify-center transition-colors mb-4" 
              onClick={() => setPaymentModalOpen(true)}
            >
              <CreditCard className="w-8 h-8 mr-3" /> PAYMENT (F11)
            </button>
            <div className="flex gap-2">
               <button onClick={() => setHoldModalOpen(true)} className="flex-1 bg-orange-600 text-white p-3 rounded-lg shadow-md flex flex-col items-center justify-center text-sm font-bold hover:bg-orange-700 transition-colors">
                  <span className="text-xs opacity-80 mb-1">F9:</span>
                  <span className="font-bold">Hold/Resume</span>
               </button>
               <button onClick={() => setReprintModalOpen(true)} className="flex-1 bg-slate-700 text-white p-3 rounded-lg shadow-md flex flex-col items-center justify-center text-sm font-bold hover:bg-slate-800 transition-colors">
                  <Printer className="w-5 h-5 mb-1" />
                  <span className="font-bold">F10: Reprint</span>
               </button>
            </div>
          </div>
        </div>
      <PaymentModal 
        isOpen={isPaymentModalOpen} 
        onClose={() => !isProcessing && setPaymentModalOpen(false)} 
        cartTotal={cart.finalTotal}
        isProcessing={isProcessing}
        customer={customer}
        onCompletePayment={async (tenders: any) => {
          try {
            setIsProcessing(true);
            // Generate dynamic invoice payload matching CreateInvoiceCommand
            const roundOffVal = +(Math.round(cart.finalTotal) - cart.finalTotal).toFixed(2);
            const netPayableVal = Math.round(cart.finalTotal);
            const paymentModeVal = tenders.cash > 0 && (tenders.upi > 0 || tenders.card > 0) ? 'SPLIT'
                                 : tenders.cash > 0 ? 'CASH'
                                 : tenders.upi > 0  ? 'UPI'
                                 : tenders.card > 0 ? 'CARD'
                                 : 'WALLET';
            const payload = {
              invoiceNumber: `INV-${localStorage.getItem('pos_terminal_code') || 'POS-01'}-${Date.now().toString().slice(-6)}`,
              terminalId: terminalId,
              cashierId: cashierId,
              customerId: customer?.id || undefined,
              promoCode: promoCode || undefined,
              walletAmountUsed: tenders.wallet || 0,
              cashAmount: tenders.cash || 0,
              upiAmount: tenders.upi || 0,
              cardAmount: tenders.card || 0,
              roundOff: roundOffVal,
              netPayable: netPayableVal,
              paymentMode: paymentModeVal,
              items: cart.items.map((item: any) => ({
                productId: item.productId,
                quantity: item.qty,
                unitPrice: item.unitPrice
              }))
            };

            // Track loyalty points before/after
            const oldLoyaltyPoints = customer ? (customer.points || 0) : 0;
            let newLoyaltyBalance  = oldLoyaltyPoints;
            let loyaltyEarned      = 0;

            try {
              await createInvoice(payload);
              // Re-fetch customer to get updated loyalty balance from backend
              if (customer?.phone) {
                try {
                  const freshCustomers = await searchCustomers(customer.phone);
                  if (freshCustomers.length > 0) {
                    newLoyaltyBalance = freshCustomers[0].loyaltyPoints || 0;
                    loyaltyEarned     = Math.max(0, newLoyaltyBalance - oldLoyaltyPoints);
                    // Update customer state so next transaction shows correct balance
                    setCustomer((prev: any) => prev ? { ...prev, points: newLoyaltyBalance } : prev);
                  }
                } catch { /* non-critical — skip */ }
              }
            } catch (err: any) {
              console.warn('Network issue during checkout, saving offline...', err);
              await posDb.invoices.put({ ...payload, id: payload.invoiceNumber, businessDate: new Date().toISOString(), status: 'COMPLETED' } as any);
              await posDb.sync_queue.put({ ...payload, id: payload.invoiceNumber, businessDate: new Date().toISOString() } as any);
              const errorDetail = err?.response?.data?.Detailed || err?.response?.data?.Message || err?.message || JSON.stringify(err);
              alert(`Saved Offline: Invoice ${payload.invoiceNumber} queued for sync.\n\nERROR DETAIL:\n${errorDetail}`);
            }

            const invoiceToPrint = {
              id: payload.invoiceNumber,
              invoiceNumber: payload.invoiceNumber,
              businessDate: new Date().toISOString(),
              terminalId: payload.terminalId,
              terminalSequence: 1,
              cashierId: cashierId,
              cashierName: user?.fullName || user?.username || 'Cashier',
              customerName: customer?.name || undefined,
              customerPhone: customer?.phone || undefined,
              loyaltyPointsEarned: loyaltyEarned,
              loyaltyPointsBalance: newLoyaltyBalance,
              subTotal: cart.subtotal,
              discountAmount: cart.totalDiscount,
              taxAmount: cart.taxTotal,
              totalAmount: cart.finalTotal,
              cashAmount: tenders.cash,
              upiAmount: tenders.upi,
              cardAmount: tenders.card,
              walletAmountUsed: tenders.wallet,
              roundOff: Math.round(cart.finalTotal) - cart.finalTotal,
              netPayable: Math.round(cart.finalTotal),
              paymentMode: tenders.cash > 0 ? 'CASH' : tenders.upi > 0 ? 'UPI' : tenders.card > 0 ? 'CARD' : 'WALLET',
              status: 'COMPLETED',
              items: cart.items.map((item: any) => ({
                id: item.id || '',
                productId: item.productId,
                name: item.name,
                quantity: item.qty,
                unitPrice: item.unitPrice,
                cgstRate: item.cgstRate || 0,
                sgstRate: item.sgstRate || 0,
                discountAmount: item.discountAmount || 0,
                totalAmount: item.finalLineTotal || item.lineTotal
              }))
            };

            await posDb.invoices.put(invoiceToPrint as any);

            setCompletedInvoice(invoiceToPrint);
            setPaymentModalOpen(false);
            printReceipt(invoiceToPrint);

            setCart({ items: [], subtotal: 0, totalDiscount: 0, taxTotal: 0, finalTotal: 0, appliedOfferNames: [] });
            setCustomer(null);
            setCustomerQuery('');
            setPromoCode('');
          } catch (err: any) {
            console.error('Checkout error:', err);
            alert('Failed to process checkout: ' + (err.message));
          } finally {
            setIsProcessing(false);
          }
        }} 
      />

      </div>

      <CustomerRegistrationModal 
        isOpen={isCustomerModalOpen} 
        initialPhone={customerQuery}
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
            setCustomerQuery('');
            alert('Customer registered successfully!');
          } catch (err) {
            console.error('Error registering customer:', err);
            throw err;
          }
        }} 
      />

      {/* Modals */}

      <HoldResumeModal 
        isOpen={isHoldModalOpen} 
        onClose={() => setHoldModalOpen(false)} 
        onResume={handleResumeCart} 
      />

      <ReprintModal
        isOpen={isReprintModalOpen}
        onClose={() => setReprintModalOpen(false)}
        onReprint={(inv: any) => {
          printReceipt(inv);
        }}
      />

      <ManagerPinModal
        isOpen={isManagerModalOpen}
        onClose={() => setManagerModalOpen(false)}
        actionName={managerAction?.name}
        onSuccess={() => {
            setManagerModalOpen(false);
            managerAction?.callback();
            setManagerAction(null);
        }}
      />

      <OpenShiftModal
        isOpen={isOpenShiftModalOpen}
        onOpenShift={handleOpenShift}
      />

      <CloseShiftModal
        isOpen={isCloseShiftModalOpen}
        onClose={() => setCloseShiftModalOpen(false)}
        onCloseShift={handleCloseShift}
      />

    </div>
  );
};
