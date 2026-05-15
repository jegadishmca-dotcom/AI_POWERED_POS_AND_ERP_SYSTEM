import React, { useState } from 'react';
import { X, Search, User } from 'lucide-react';

export const CustomerSearchModal = ({ isOpen, onClose, onSelectCustomer }: any) => {
  const [searchTerm, setSearchTerm] = useState('');

  if (!isOpen) return null;

  const handleSelect = (customer: any) => {
    onSelectCustomer(customer);
    onClose();
  };

  return (
    <div className="fixed inset-0 bg-black/60 z-50 flex items-center justify-center p-4">
      <div className="bg-white rounded-xl shadow-2xl w-full max-w-lg overflow-hidden flex flex-col h-[500px]">
        <div className="bg-slate-800 p-4 flex justify-between items-center text-white">
          <h2 className="text-xl font-bold flex items-center"><User className="mr-2" /> Select Customer</h2>
          <button onClick={onClose}><X /></button>
        </div>
        <div className="p-4 border-b">
          <div className="relative">
            <Search className="absolute left-3 top-3 text-gray-400" />
            <input 
              type="text" 
              placeholder="Search by Mobile No or Name..."
              value={searchTerm}
              onChange={(e) => setSearchTerm(e.target.value)}
              className="w-full pl-10 p-3 border-2 border-gray-300 rounded focus:border-blue-600 outline-none"
              autoFocus
            />
          </div>
        </div>
        <div className="flex-1 overflow-y-auto p-4">
           {/* Mock Data */}
           <div onClick={() => handleSelect({ id: '1', name: 'Rahul Sharma', phone: '9876543210' })} className="p-4 border-b hover:bg-slate-50 cursor-pointer flex justify-between items-center">
              <div>
                 <p className="font-bold text-slate-800">Rahul Sharma</p>
                 <p className="text-sm text-gray-500">9876543210</p>
              </div>
              <span className="bg-emerald-100 text-emerald-800 text-xs px-2 py-1 rounded">Loyalty: 150 pts</span>
           </div>
        </div>
      </div>
    </div>
  );
};
