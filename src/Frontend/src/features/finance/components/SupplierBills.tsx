import React, { useState } from 'react';
import { FileText, Search, Filter, Plus, Download } from 'lucide-react';
import { formatCurrency } from '../../../utils/formatters';

export const SupplierBills: React.FC = () => {
  return (
    <div className="p-6 max-w-7xl mx-auto">
      <div className="flex flex-col md:flex-row md:items-center justify-between mb-8 gap-4">
        <div>
          <h2 className="text-2xl font-extrabold text-slate-800 dark:text-white flex items-center gap-3">
            <FileText className="w-7 h-7 text-rose-600" />
            Supplier Bills (AP)
          </h2>
          <p className="text-slate-500 dark:text-slate-400 mt-1">Manage vendor invoices and accounts payable</p>
        </div>
        <div className="flex gap-3">
          <button className="bg-white hover:bg-slate-50 text-slate-700 border border-slate-300 px-4 py-2.5 rounded-lg font-bold flex items-center gap-2 shadow-sm transition-all">
            <Download className="w-5 h-5" />
            Export
          </button>
          <button className="bg-rose-600 hover:bg-rose-700 text-white px-4 py-2.5 rounded-lg font-bold flex items-center gap-2 shadow-lg shadow-rose-600/30 transition-all">
            <Plus className="w-5 h-5" />
            Enter Bill
          </button>
        </div>
      </div>
      
      <div className="bg-white dark:bg-slate-900 rounded-xl shadow-sm border border-slate-200 dark:border-slate-800 p-12 text-center">
        <p className="text-slate-500">Supplier Bills UI Placeholder</p>
      </div>
    </div>
  );
};
