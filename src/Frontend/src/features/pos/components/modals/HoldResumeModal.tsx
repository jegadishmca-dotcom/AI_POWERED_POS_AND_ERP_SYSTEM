import React, { useState, useEffect } from 'react';
import { X, Clock, Play } from 'lucide-react';
import { posDb } from '../../db/pos.db';
import { Invoice } from '../../types';

export const HoldResumeModal = ({ isOpen, onClose, onResume }: any) => {
  const [heldInvoices, setHeldInvoices] = useState<Invoice[]>([]);

  useEffect(() => {
    if (isOpen) {
      posDb.held_invoices.toArray().then(setHeldInvoices);
    }
  }, [isOpen]);

  if (!isOpen) return null;

  const handleResume = async (invoice: Invoice) => {
    await posDb.held_invoices.delete(invoice.id);
    onResume(invoice);
  };

  return (
    <div className="fixed inset-0 bg-black/60 z-50 flex items-center justify-center p-4">
      <div className="bg-white rounded-xl shadow-2xl w-full max-w-2xl overflow-hidden flex flex-col h-[600px]">
        <div className="bg-orange-600 p-4 flex justify-between items-center text-white">
          <h2 className="text-xl font-bold flex items-center"><Clock className="mr-2" /> Held Invoices</h2>
          <button onClick={onClose}><X /></button>
        </div>
        <div className="flex-1 overflow-y-auto p-4">
           {heldInvoices.length === 0 ? (
             <div className="text-center text-gray-500 mt-20">No held invoices found.</div>
           ) : (
             heldInvoices.map((inv) => (
               <div key={inv.id} className="p-4 border rounded-lg mb-4 flex justify-between items-center hover:shadow-md transition bg-orange-50">
                  <div>
                     <div className="flex items-center gap-2">
                       <p className="font-bold text-slate-800">{inv.invoiceNumber}</p>
                       {inv.customer && (
                         <span className="px-2 py-0.5 text-xs bg-orange-200 text-orange-900 rounded font-medium">
                           {inv.customer.name}
                         </span>
                       )}
                     </div>
                     <p className="text-sm text-gray-500">{new Date(inv.businessDate).toLocaleString()}</p>
                     <p className="text-sm text-gray-700 mt-1">{inv.items.length} items | Total: ₹{inv.totalAmount.toFixed(2)}</p>
                  </div>
                  <button onClick={() => handleResume(inv)} className="bg-emerald-600 hover:bg-emerald-700 text-white p-3 rounded-full shadow-lg">
                    <Play fill="white" className="w-5 h-5" />
                  </button>
               </div>
             ))
           )}
        </div>
      </div>
    </div>
  );
};
