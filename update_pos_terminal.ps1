$frontendDir = "d:\JEGADISH\AI_POWERED_POS_AND_ERP_SYSTEM\src\Frontend"

# Replace PosTerminal.tsx with CRM integration
@"
import React, { useState } from 'react';
import { Search, ShoppingCart, User, Plus, X, CreditCard, Wallet, Award } from 'lucide-react';
import { CustomerRegistrationModal } from '../../crm/components/CustomerRegistrationModal';

export const PosTerminal = () => {
  const [cart, setCart] = useState<any[]>([]);
  const [customer, setCustomer] = useState<any>(null);
  const [isCustomerModalOpen, setCustomerModalOpen] = useState(false);
  
  // Mock customer search
  const handleCustomerSearch = (e: React.KeyboardEvent<HTMLInputElement>) => {
    if (e.key === 'Enter') {
      const val = e.currentTarget.value;
      if (val === '9988776655') {
        setCustomer({ id: '1', name: 'Rahul Sharma', phone: '9988776655', walletBalance: 500, points: 120, tier: 'Gold' });
      }
    }
  };

  const handleRegisterCustomer = (newCust: any) => {
    setCustomer({ ...newCust, id: '2', walletBalance: 0, points: 0, tier: 'Base' });
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
              title="Add New Customer"
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

        {/* Existing Cart Search & Table goes here... */}
        <div className="p-4 flex-1">
          <h2 className="text-slate-400 flex items-center justify-center h-full text-xl font-bold">
            <ShoppingCart className="mr-2 w-8 h-8" /> Empty Cart
          </h2>
        </div>
      </div>

      {/* Right: Payment Panel */}
      <div className="w-1/3 flex flex-col bg-slate-50 p-6">
        <h2 className="text-2xl font-black text-slate-800 mb-6 border-b pb-2">Payment</h2>
        
        <div className="flex-1">
          <div className="flex justify-between text-lg mb-2"><span>Subtotal</span><span>₹0.00</span></div>
          <div className="flex justify-between text-lg mb-2 text-emerald-600"><span>Discounts</span><span>-₹0.00</span></div>
          <div className="flex justify-between text-lg mb-6"><span>Tax (GST)</span><span>₹0.00</span></div>
          
          <div className="flex justify-between text-4xl font-black text-indigo-700 mb-8 border-t pt-4">
            <span>Total</span><span>₹0.00</span>
          </div>

          {/* Payment Methods */}
          <div className="grid grid-cols-2 gap-4">
            <button className="bg-emerald-600 text-white p-4 rounded-lg font-bold text-xl shadow hover:bg-emerald-700">CASH</button>
            <button className="bg-blue-600 text-white p-4 rounded-lg font-bold text-xl shadow hover:bg-blue-700">UPI / QR</button>
            <button className="bg-slate-800 text-white p-4 rounded-lg font-bold text-xl shadow hover:bg-slate-900">CARD</button>
            
            <button 
              className={`p-4 rounded-lg font-bold text-xl shadow flex flex-col items-center justify-center \${customer && customer.walletBalance > 0 ? 'bg-indigo-600 text-white hover:bg-indigo-700' : 'bg-gray-200 text-gray-400 cursor-not-allowed'}`}
              disabled={!customer || customer.walletBalance <= 0}
            >
              WALLET
              {customer && <span className="text-sm">Bal: ₹{customer.walletBalance}</span>}
            </button>
          </div>
        </div>
      </div>

      <CustomerRegistrationModal 
        isOpen={isCustomerModalOpen} 
        onClose={() => setCustomerModalOpen(false)} 
        onRegister={handleRegisterCustomer} 
      />
    </div>
  );
};
"@ | Out-File -FilePath "$frontendDir\src\features\pos\components\PosTerminal.tsx" -Encoding utf8

Write-Host "POS Terminal updated with CRM"
