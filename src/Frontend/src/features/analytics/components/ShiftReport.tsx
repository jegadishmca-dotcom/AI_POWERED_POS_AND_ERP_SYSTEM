import React, { useState } from 'react';
import { useAuthStore } from '../../auth/store/auth.store';
import { Printer, Coins, CreditCard, Wallet, TrendingUp, CheckCircle } from 'lucide-react';

export const ShiftReport: React.FC = () => {
  const { user } = useAuthStore();
  const terminalCode = localStorage.getItem('pos_terminal_code') || 'POS-01';
  const businessDate = new Date().toLocaleDateString('en-IN', { dateStyle: 'medium' });

  // Mock sales data for the current cashier's shift
  const salesSummary = {
    cash: 45500.00,
    card: 52400.00,
    wallet: 26600.00,
    total: 124500.00,
    invoiceCount: 342
  };

  const [physicalCash, setPhysicalCash] = useState<string>('');
  const [isClosed, setIsClosed] = useState<boolean>(false);

  const parsedPhysicalCash = parseFloat(physicalCash) || 0;
  const discrepancy = parsedPhysicalCash - salesSummary.cash;

  const handleCloseShift = () => {
    setIsClosed(true);
    alert(`Shift Closed for Terminal ${terminalCode}.\nPrinting Z-Report summary...`);
  };

  return (
    <div className="p-8 max-w-4xl mx-auto space-y-8">
      {/* Header */}
      <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between bg-white dark:bg-slate-800 p-6 rounded-xl border border-slate-200 dark:border-slate-700 shadow-sm gap-4">
        <div>
          <h2 className="text-2xl font-black text-slate-800 dark:text-white">Shift & Daily Sales Report</h2>
          <p className="text-sm text-slate-500 dark:text-slate-400">Terminal Shift Closing & Reconciliation</p>
        </div>
        <div className="flex gap-3">
          <button 
            onClick={() => window.print()} 
            className="px-4 py-2 border border-slate-300 dark:border-slate-600 rounded-lg text-slate-700 dark:text-slate-300 hover:bg-slate-50 dark:hover:bg-slate-700 font-bold flex items-center gap-2 transition"
          >
            <Printer className="w-4 h-4" /> Print X-Report
          </button>
          {!isClosed && (
            <button 
              onClick={handleCloseShift}
              className="px-4 py-2 bg-indigo-600 hover:bg-indigo-700 text-white rounded-lg font-bold flex items-center gap-2 shadow-md shadow-indigo-600/20 transition"
            >
              <CheckCircle className="w-4 h-4" /> Close Active Shift
            </button>
          )}
        </div>
      </div>

      {/* Meta Grid */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
        <div className="bg-white dark:bg-slate-800 p-5 rounded-xl border border-slate-200 dark:border-slate-700 shadow-sm">
          <p className="text-xs font-semibold text-slate-400 uppercase tracking-wider">Active Cashier</p>
          <p className="text-lg font-extrabold text-slate-800 dark:text-white mt-1">{user?.fullName || 'Terminal Cashier'}</p>
        </div>
        <div className="bg-white dark:bg-slate-800 p-5 rounded-xl border border-slate-200 dark:border-slate-700 shadow-sm">
          <p className="text-xs font-semibold text-slate-400 uppercase tracking-wider">Active Terminal</p>
          <p className="text-lg font-extrabold text-indigo-600 dark:text-indigo-400 mt-1">Terminal {terminalCode}</p>
        </div>
        <div className="bg-white dark:bg-slate-800 p-5 rounded-xl border border-slate-200 dark:border-slate-700 shadow-sm">
          <p className="text-xs font-semibold text-slate-400 uppercase tracking-wider">Business Date</p>
          <p className="text-lg font-extrabold text-slate-800 dark:text-white mt-1">{businessDate}</p>
        </div>
      </div>

      {/* Shift Closing Summary */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-8">
        
        {/* Sales by Tender */}
        <div className="bg-white dark:bg-slate-800 p-6 rounded-xl border border-slate-200 dark:border-slate-700 shadow-sm space-y-6">
          <h3 className="text-lg font-extrabold text-slate-800 dark:text-white border-b pb-4 flex items-center gap-2">
            <TrendingUp className="w-5 h-5 text-indigo-500" /> Shift Sales Summary
          </h3>
          
          <div className="space-y-4">
            <div className="flex items-center justify-between p-3 bg-slate-50 dark:bg-slate-900/50 rounded-lg">
              <div className="flex items-center gap-3">
                <div className="w-8 h-8 rounded-full bg-emerald-100 dark:bg-emerald-950/30 flex items-center justify-center text-emerald-600 dark:text-emerald-400">
                  <Coins className="w-4 h-4" />
                </div>
                <span className="font-bold text-slate-700 dark:text-slate-300">Cash Sales</span>
              </div>
              <span className="font-extrabold text-slate-800 dark:text-white">₹{salesSummary.cash.toLocaleString()}</span>
            </div>

            <div className="flex items-center justify-between p-3 bg-slate-50 dark:bg-slate-900/50 rounded-lg">
              <div className="flex items-center gap-3">
                <div className="w-8 h-8 rounded-full bg-blue-100 dark:bg-blue-950/30 flex items-center justify-center text-blue-600 dark:text-blue-400">
                  <CreditCard className="w-4 h-4" />
                </div>
                <span className="font-bold text-slate-700 dark:text-slate-300">Card/UPI Sales</span>
              </div>
              <span className="font-extrabold text-slate-800 dark:text-white">₹{salesSummary.card.toLocaleString()}</span>
            </div>

            <div className="flex items-center justify-between p-3 bg-slate-50 dark:bg-slate-900/50 rounded-lg">
              <div className="flex items-center gap-3">
                <div className="w-8 h-8 rounded-full bg-purple-100 dark:bg-purple-950/30 flex items-center justify-center text-purple-600 dark:text-purple-400">
                  <Wallet className="w-4 h-4" />
                </div>
                <span className="font-bold text-slate-700 dark:text-slate-300">CRM Wallet Splits</span>
              </div>
              <span className="font-extrabold text-slate-800 dark:text-white">₹{salesSummary.wallet.toLocaleString()}</span>
            </div>

            <div className="border-t pt-4 flex items-center justify-between">
              <span className="font-black text-slate-800 dark:text-white text-lg">Total Shift Sales</span>
              <span className="font-black text-2xl text-indigo-600 dark:text-indigo-400">₹{salesSummary.total.toLocaleString()}</span>
            </div>
            
            <div className="flex items-center justify-between text-xs text-slate-500 dark:text-slate-400">
              <span>Total Transactions Served</span>
              <span className="font-bold">{salesSummary.invoiceCount} Bills</span>
            </div>
          </div>
        </div>

        {/* Cash Reconciliation */}
        <div className="bg-white dark:bg-slate-800 p-6 rounded-xl border border-slate-200 dark:border-slate-700 shadow-sm space-y-6">
          <h3 className="text-lg font-extrabold text-slate-800 dark:text-white border-b pb-4 flex items-center gap-2">
            <Coins className="w-5 h-5 text-emerald-500" /> Cash Reconciliation
          </h3>

          <div className="space-y-4">
            <div>
              <label className="text-sm font-bold text-slate-600 dark:text-slate-400 block mb-2">
                Expected Cash in Drawer
              </label>
              <div className="w-full bg-slate-100 dark:bg-slate-900 p-3 rounded-lg font-black text-slate-800 dark:text-white text-lg">
                ₹{salesSummary.cash.toLocaleString()}
              </div>
            </div>

            <div>
              <label className="text-sm font-bold text-slate-600 dark:text-slate-400 block mb-2">
                Counted Cash (Physical)
              </label>
              <div className="relative flex items-center">
                <span className="absolute left-3 font-bold text-slate-400">₹</span>
                <input 
                  type="number" 
                  value={physicalCash} 
                  onChange={(e) => setPhysicalCash(e.target.value)}
                  disabled={isClosed}
                  placeholder="Enter physical cash amount counted" 
                  className="w-full pl-8 p-3 rounded-lg border border-slate-300 dark:border-slate-600 bg-white dark:bg-slate-700 text-slate-800 dark:text-white outline-none focus:ring-2 ring-indigo-500 font-extrabold"
                />
              </div>
            </div>

            {physicalCash && (
              <div className={`p-4 rounded-lg flex items-center justify-between ${
                discrepancy === 0 
                  ? 'bg-emerald-50 dark:bg-emerald-950/20 text-emerald-800 dark:text-emerald-400' 
                  : discrepancy > 0 
                  ? 'bg-blue-50 dark:bg-blue-950/20 text-blue-800 dark:text-blue-400'
                  : 'bg-rose-50 dark:bg-rose-950/20 text-rose-800 dark:text-rose-400'
              }`}>
                <div>
                  <p className="text-xs font-semibold uppercase tracking-wider">Drawer Discrepancy</p>
                  <p className="text-lg font-black">
                    {discrepancy === 0 
                      ? 'Balanced (No Shortage)' 
                      : discrepancy > 0 
                      ? `Surplus: +₹${discrepancy.toFixed(2)}` 
                      : `Shortage: -₹${Math.abs(discrepancy).toFixed(2)}`}
                  </p>
                </div>
              </div>
            )}

            {isClosed && (
              <div className="p-4 bg-slate-100 dark:bg-slate-900 rounded-lg flex items-center gap-3 text-slate-700 dark:text-slate-300 text-sm">
                <CheckCircle className="w-5 h-5 text-emerald-500 shrink-0" />
                <div>
                  <p className="font-extrabold">Shift closed successfully.</p>
                  <p className="text-xs text-slate-500">Z-Report generated. POS Terminal is locked.</p>
                </div>
              </div>
            )}
          </div>
        </div>
      </div>
    </div>
  );
};
