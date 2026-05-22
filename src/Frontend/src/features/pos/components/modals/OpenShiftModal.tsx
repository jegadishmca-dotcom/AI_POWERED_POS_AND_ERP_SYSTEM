import React, { useState } from 'react';
import { Lock, IndianRupee } from 'lucide-react';

interface OpenShiftModalProps {
  isOpen: boolean;
  onOpenShift: (openingCash: number) => void;
}

export const OpenShiftModal = ({ isOpen, onOpenShift }: OpenShiftModalProps) => {
  const [openingCash, setOpeningCash] = useState<string>('0');

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 bg-slate-900/95 z-[100] flex items-center justify-center p-4 backdrop-blur-md">
      <div className="bg-white rounded-2xl shadow-2xl w-full max-w-md overflow-hidden transform transition-all">
        <div className="bg-emerald-600 p-6 flex flex-col items-center justify-center text-white text-center">
          <div className="bg-white/20 p-4 rounded-full mb-4">
            <Lock className="w-12 h-12" />
          </div>
          <h2 className="text-2xl font-black mb-1">Terminal Locked</h2>
          <p className="text-emerald-100 font-medium">Please open a new shift to start billing.</p>
        </div>
        
        <div className="p-8">
          <div className="mb-6">
            <label className="block text-sm font-bold text-slate-700 mb-2">
              Opening Float Cash (₹)
            </label>
            <div className="relative">
              <div className="absolute inset-y-0 left-0 pl-4 flex items-center pointer-events-none">
                <IndianRupee className="h-6 w-6 text-slate-400" />
              </div>
              <input
                type="number"
                value={openingCash}
                onChange={(e) => setOpeningCash(e.target.value)}
                className="block w-full pl-12 pr-4 py-4 border-2 border-slate-200 rounded-xl leading-5 bg-white placeholder-slate-400 focus:outline-none focus:ring-4 focus:ring-emerald-500/20 focus:border-emerald-500 sm:text-2xl font-black text-slate-800 transition-all"
                placeholder="0.00"
                min="0"
                autoFocus
                onFocus={(e) => e.target.select()}
                onKeyDown={(e) => {
                    if (e.key === 'Enter' && parseFloat(openingCash) >= 0) {
                        onOpenShift(parseFloat(openingCash) || 0);
                    }
                }}
              />
            </div>
            <p className="mt-2 text-sm text-slate-500">
              Enter the amount of cash currently in the drawer to start the day.
            </p>
          </div>

          <button
            onClick={() => onOpenShift(parseFloat(openingCash) || 0)}
            disabled={parseFloat(openingCash) < 0 || isNaN(parseFloat(openingCash))}
            className="w-full bg-emerald-600 text-white font-bold text-xl py-4 rounded-xl shadow-lg hover:bg-emerald-700 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
          >
            OPEN SHIFT
          </button>
        </div>
      </div>
    </div>
  );
};
