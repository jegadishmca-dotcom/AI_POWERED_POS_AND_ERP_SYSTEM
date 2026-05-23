import React, { useState } from 'react';
import { LogOut, IndianRupee } from 'lucide-react';

interface CloseShiftModalProps {
  isOpen: boolean;
  onClose: () => void;
  onCloseShift: (closingCash: number) => void;
}

export const CloseShiftModal = ({ isOpen, onClose, onCloseShift }: CloseShiftModalProps) => {
  const [closingCash, setClosingCash] = useState<string>('');

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 bg-slate-900/80 z-[100] flex items-center justify-center p-4 backdrop-blur-sm">
      <div className="bg-white rounded-2xl shadow-2xl w-full max-w-md overflow-hidden transform transition-all">
        <div className="bg-red-600 p-6 flex flex-col items-center justify-center text-white text-center relative">
          <button 
            onClick={onClose}
            className="absolute top-4 right-4 text-red-200 hover:text-white transition-colors"
          >
             <span className="text-xl font-bold">×</span>
          </button>
          <div className="bg-white/20 p-4 rounded-full mb-4">
            <LogOut className="w-12 h-12" />
          </div>
          <h2 className="text-2xl font-black mb-1">Close Shift</h2>
          <p className="text-red-100 font-medium">Declare physical cash in the drawer</p>
        </div>
        
        <div className="p-8">
          <div className="mb-6">
            <label className="block text-sm font-bold text-slate-700 mb-2">
              Physical Cash Count (₹)
            </label>
            <div className="relative">
              <div className="absolute inset-y-0 left-0 pl-4 flex items-center pointer-events-none">
                <IndianRupee className="h-6 w-6 text-slate-400" />
              </div>
              <input
                type="number"
                value={closingCash}
                onChange={(e) => setClosingCash(e.target.value)}
                className="block w-full pl-12 pr-4 py-4 border-2 border-slate-200 rounded-xl leading-5 bg-white placeholder-slate-400 focus:outline-none focus:ring-4 focus:ring-red-500/20 focus:border-red-500 sm:text-2xl font-black text-slate-800 transition-all"
                placeholder="0.00"
                min="0"
                autoFocus
                onFocus={(e) => e.target.select()}
                onKeyDown={(e) => {
                    if (e.key === 'Enter' && closingCash !== '') {
                        onCloseShift(parseFloat(closingCash) || 0);
                    }
                }}
              />
            </div>
            <p className="mt-2 text-sm text-slate-500">
              Enter the exact physical cash amount present in the drawer. This will be verified against the Z-Report.
            </p>
          </div>

          <div className="flex gap-4">
            <button
              onClick={onClose}
              className="flex-1 bg-slate-100 text-slate-700 font-bold text-lg py-4 rounded-xl hover:bg-slate-200 transition-colors"
            >
              Cancel
            </button>
            <button
              onClick={() => onCloseShift(parseFloat(closingCash) || 0)}
              disabled={closingCash === '' || parseFloat(closingCash) < 0 || isNaN(parseFloat(closingCash))}
              className="flex-1 bg-red-600 text-white font-bold text-lg py-4 rounded-xl shadow-lg hover:bg-red-700 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
            >
              CLOSE SHIFT
            </button>
          </div>
        </div>
      </div>
    </div>
  );
};
