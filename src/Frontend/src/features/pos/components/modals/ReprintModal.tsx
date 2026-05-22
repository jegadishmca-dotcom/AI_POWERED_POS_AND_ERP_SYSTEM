import React, { useState, useEffect } from 'react';
import { Printer, X, History } from 'lucide-react';
import { posDb } from '../../db/pos.db';

export const ReprintModal = ({ isOpen, onClose, onReprint }: any) => {
  const [recentInvoices, setRecentInvoices] = useState<any[]>([]);

  useEffect(() => {
    if (isOpen) {
      loadRecentInvoices();
    }
  }, [isOpen]);

  const loadRecentInvoices = async () => {
    try {
      // Get the last 10 completed invoices sorted by ID (which acts as a timeline proxy or we can sort manually)
      const invoices = await posDb.invoices.toArray();
      // Sort by newest first based on businessDate
      invoices.sort((a, b) => new Date(b.businessDate).getTime() - new Date(a.businessDate).getTime());
      setRecentInvoices(invoices.slice(0, 10));
    } catch (err) {
      console.error("Failed to load recent invoices", err);
    }
  };

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 bg-black/60 z-[60] flex items-center justify-center p-4 backdrop-blur-sm">
      <div className="bg-white rounded-xl shadow-2xl w-full max-w-2xl overflow-hidden flex flex-col">
        <div className="bg-slate-800 p-4 flex justify-between items-center text-white">
          <h2 className="text-xl font-bold flex items-center"><History className="mr-2" /> Reprint Recent Invoices</h2>
          <button onClick={onClose} className="hover:text-slate-300"><X className="w-6 h-6" /></button>
        </div>
        
        <div className="p-0 max-h-[60vh] overflow-y-auto">
          {recentInvoices.length === 0 ? (
            <div className="p-8 text-center text-slate-500">
              <Printer className="w-12 h-12 mx-auto mb-3 opacity-20" />
              <p>No recent invoices found on this terminal.</p>
            </div>
          ) : (
            <table className="w-full text-left border-collapse">
              <thead className="bg-slate-100 sticky top-0">
                <tr>
                  <th className="p-3 border-b">Invoice No</th>
                  <th className="p-3 border-b">Time</th>
                  <th className="p-3 border-b text-right">Amount</th>
                  <th className="p-3 border-b text-center">Action</th>
                </tr>
              </thead>
              <tbody>
                {recentInvoices.map((inv) => (
                  <tr key={inv.id} className="hover:bg-slate-50 border-b last:border-0">
                    <td className="p-3 font-mono text-sm font-bold text-slate-700">{inv.invoiceNumber}</td>
                    <td className="p-3 text-sm text-slate-600">
                      {new Date(inv.businessDate).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}
                    </td>
                    <td className="p-3 text-right font-bold text-emerald-600">
                      ₹{inv.netPayable.toFixed(2)}
                    </td>
                    <td className="p-3 text-center">
                      <button 
                        onClick={() => {
                          onReprint(inv);
                          onClose();
                        }}
                        className="bg-indigo-100 text-indigo-700 px-3 py-1.5 rounded text-sm font-bold hover:bg-indigo-200 transition-colors inline-flex items-center"
                      >
                        <Printer className="w-4 h-4 mr-1" /> Reprint
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      </div>
    </div>
  );
};
