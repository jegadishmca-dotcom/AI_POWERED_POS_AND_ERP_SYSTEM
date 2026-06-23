import React from 'react';
import { Banknote, Plus, Download } from 'lucide-react';

export const SupplierPayments: React.FC = () => {
  return (
    <div className="p-6 max-w-7xl mx-auto">
      <div className="flex flex-col md:flex-row md:items-center justify-between mb-8 gap-4">
        <div>
          <h2 className="text-2xl font-extrabold text-slate-800 dark:text-white flex items-center gap-3">
            <Banknote className="w-7 h-7 text-rose-600" />
            Supplier Payments
          </h2>
          <p className="text-slate-500 dark:text-slate-400 mt-1">Record and allocate vendor payments</p>
        </div>
        <div className="flex gap-3">
          <button className="bg-white hover:bg-slate-50 text-slate-700 border border-slate-300 px-4 py-2.5 rounded-lg font-bold flex items-center gap-2 shadow-sm transition-all">
            <Download className="w-5 h-5" />
            Export
          </button>
          <button className="bg-rose-600 hover:bg-rose-700 text-white px-4 py-2.5 rounded-lg font-bold flex items-center gap-2 shadow-lg shadow-rose-600/30 transition-all">
            <Plus className="w-5 h-5" />
            Record Payment
          </button>
        </div>
      </div>
      <div className="bg-white dark:bg-slate-900 rounded-xl shadow-sm border border-slate-200 dark:border-slate-800 p-12 text-center">
        <p className="text-slate-500">Supplier Payments UI Placeholder</p>
      </div>
    </div>
  );
};
