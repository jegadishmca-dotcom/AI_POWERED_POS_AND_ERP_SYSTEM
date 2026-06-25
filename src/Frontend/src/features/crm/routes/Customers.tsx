import React from 'react';
import { Users, Search, Plus, Filter } from 'lucide-react';

export const Customers = () => {
  return (
    <div className="p-6 max-w-7xl mx-auto space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-slate-900 dark:text-white flex items-center gap-2">
            <Users className="w-6 h-6 text-rose-600" />
            CRM Master
          </h1>
          <p className="text-slate-500 dark:text-slate-400 mt-1">Manage customers, loyalty points, and CRM data</p>
        </div>
        <button className="bg-rose-600 hover:bg-rose-700 text-white px-4 py-2 rounded-lg font-medium flex items-center transition-colors">
          <Plus className="w-4 h-4 mr-2" />
          Add Customer
        </button>
      </div>

      <div className="bg-white dark:bg-slate-900 rounded-xl shadow-sm border border-slate-200 dark:border-slate-800 p-6 flex flex-col items-center justify-center text-center min-h-[400px]">
        <Users className="w-16 h-16 text-slate-300 dark:text-slate-700 mb-4" />
        <h3 className="text-lg font-bold text-slate-700 dark:text-slate-300 mb-2">Customer Management</h3>
        <p className="text-slate-500 dark:text-slate-500 max-w-md">
          The full CRM master view for managing customer profiles, viewing purchase history, adjusting loyalty points, and segmenting customers is currently under development.
        </p>
        <p className="text-sm text-slate-400 mt-4">
          In the meantime, you can continue registering and searching for customers directly from the POS Billing screen.
        </p>
      </div>
    </div>
  );
};
