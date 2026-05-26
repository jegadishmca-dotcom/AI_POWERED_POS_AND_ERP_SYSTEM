import React, { useState, useEffect } from 'react';
import { IndianRupee, Printer, Calculator, FileText, CheckCircle } from 'lucide-react';
import { posDb } from '../db/pos.db';
import { useAuthStore } from '../../auth/store/auth.store';
import { printZReport } from '../utils/printZReport';

export const ShiftReport = () => {
  const [session, setSession] = useState<any>(null);
  const [reportData, setReportData] = useState<any>(null);
  const [actualCash, setActualCash] = useState<string>('0');
  const [isClosing, setIsClosing] = useState(false);
  const [isClosed, setIsClosed] = useState(false);

  const { user } = useAuthStore();
  const terminalId = localStorage.getItem('pos_terminal_id') || '00000000-0000-0000-0000-000000000001';
  const cashierId = user?.id || '00000000-0000-0000-0000-000000000001';
  const terminalCode = localStorage.getItem('pos_terminal_code') || 'POS-01';

  useEffect(() => {
    const fetchSessionAndReport = async () => {
      try {
        const res = await fetch(`/api/pos/session/current?terminalId=${terminalId}&cashierId=${cashierId}`);
        if (res.ok) {
          const sessionData = await res.json();
          setSession(sessionData);

          // If there is an active session, fetch X-Report (which uses terminalId, cashierId, and date)
          if (sessionData && sessionData.status === 'OPEN') {
            const today = new Date().toISOString().split('T')[0];
            const zRes = await fetch(`/api/pos/z-report?terminalId=${terminalId}&businessDate=${today}&cashierId=${cashierId}`);
            if (zRes.ok) {
              setReportData(await zRes.json());
            }
          }
        }
      } catch (err) {
        console.error('Failed to fetch session or report', err);
      }
    };
    fetchSessionAndReport();
  }, []);

  const handleCloseShift = async () => {
    if (!session) return;
    setIsClosing(true);

    try {
      const payload = {
        sessionId: session.id,
        actualClosingCash: parseFloat(actualCash) || 0
      };

      const res = await fetch('/api/pos/session/close', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload)
      });

      if (res.ok) {
        setIsClosed(true);
        if (reportData) {
          printZReport(reportData, session?.openingFloatCash || 0, parseFloat(actualCash) || 0, user?.fullName || 'Cashier');
        }
        alert('Shift Closed Successfully!\nCash Discrepancy (if any) posted to financial ledger.');
      } else {
        alert('Failed to close shift.');
      }
    } catch (err) {
      console.error('Error closing shift', err);
      alert('Error closing shift');
    } finally {
      setIsClosing(false);
    }
  };

  if (isClosed) {
    return (
      <div className="flex-1 bg-slate-50 flex items-center justify-center p-8">
        <div className="bg-white p-12 rounded-3xl shadow-xl text-center max-w-lg">
          <CheckCircle className="w-24 h-24 text-emerald-500 mx-auto mb-6" />
          <h2 className="text-3xl font-black text-slate-800 mb-4">Shift Closed</h2>
          <p className="text-slate-600 font-medium">Your register has been closed and the Z-Report generated.</p>
          <div className="flex gap-4 justify-center mt-8">
            <button onClick={() => window.location.reload()} className="bg-indigo-650 text-white font-bold px-6 py-3 rounded-xl hover:bg-indigo-750">Return to Login</button>
            <button 
              onClick={() => { if (reportData) printZReport(reportData, session?.openingFloatCash || 0, parseFloat(actualCash) || 0, user?.fullName || 'Cashier'); }} 
              className="bg-white border-2 border-slate-200 text-slate-750 font-bold px-6 py-3 rounded-xl hover:bg-slate-50 transition-colors flex items-center gap-2"
            >
              <Printer className="w-5 h-5" />
              Reprint Z-Report
            </button>
          </div>
        </div>
      </div>
    );
  }

  if (!session) {
    return (
      <div className="flex-1 bg-slate-50 flex items-center justify-center p-8 text-slate-500 text-xl font-medium">
        No active shift found for this terminal.
      </div>
    );
  }

  const expectedCash = session.openingFloatCash + (reportData?.cashCollected || 0);

  return (
    <div className="flex-1 bg-slate-50 overflow-auto">
      <div className="max-w-4xl mx-auto p-8">
        <div className="flex justify-between items-center mb-8">
          <div>
            <h1 className="text-3xl font-black text-slate-800 tracking-tight">Shift & Sales Report</h1>
            <p className="text-slate-500 font-medium mt-1">Terminal: {terminalCode} • Cashier: {user?.fullName || 'Cashier'}</p>
          </div>
          <button 
            onClick={() => { if (reportData) printZReport(reportData, session?.openingFloatCash || 0, parseFloat(actualCash) || 0, user?.fullName || 'Cashier'); }}
            className="bg-white border-2 border-slate-200 text-slate-700 font-bold px-6 py-3 rounded-xl hover:bg-slate-50 transition-colors flex items-center gap-2"
          >
            <Printer className="w-5 h-5" />
            Print Z-Report
          </button>
        </div>

        <div className="grid grid-cols-1 md:grid-cols-3 gap-6 mb-8">
          <div className="bg-white p-6 rounded-2xl shadow-sm border border-slate-200">
            <h3 className="text-sm font-bold text-slate-400 uppercase tracking-wider mb-2">Total Invoices</h3>
            <div className="text-4xl font-black text-slate-800">{reportData?.totalInvoices || 0}</div>
          </div>
          <div className="bg-white p-6 rounded-2xl shadow-sm border border-slate-200">
            <h3 className="text-sm font-bold text-slate-400 uppercase tracking-wider mb-2">Net Sales</h3>
            <div className="text-4xl font-black text-emerald-600">₹{(reportData?.totalSales || 0).toFixed(2)}</div>
          </div>
          <div className="bg-white p-6 rounded-2xl shadow-sm border border-slate-200">
            <h3 className="text-sm font-bold text-slate-400 uppercase tracking-wider mb-2">Total Tax (GST)</h3>
            <div className="text-4xl font-black text-rose-500">₹{(reportData?.totalTax || 0).toFixed(2)}</div>
          </div>
        </div>

        <div className="bg-white rounded-3xl shadow-sm border border-slate-200 overflow-hidden mb-8">
          <div className="bg-indigo-600 p-6 flex justify-between items-center text-white">
            <h2 className="text-2xl font-bold flex items-center gap-3">
              <Calculator className="w-6 h-6 text-indigo-200" />
              Cash Drawer Reconciliation
            </h2>
          </div>
          <div className="p-8">
            <div className="space-y-4 mb-8">
              <div className="flex justify-between items-center py-3 border-b border-slate-100">
                <span className="text-slate-500 font-medium text-lg">Opening Float Cash</span>
                <span className="text-xl font-bold text-slate-800">₹{session.openingFloatCash.toFixed(2)}</span>
              </div>
              <div className="flex justify-between items-center py-3 border-b border-slate-100">
                <span className="text-slate-500 font-medium text-lg">Total Cash Sales</span>
                <span className="text-xl font-bold text-emerald-600">+ ₹{(reportData?.cashCollected || 0).toFixed(2)}</span>
              </div>
              <div className="flex justify-between items-center py-3 border-b border-slate-100">
                <span className="text-slate-500 font-medium text-lg">Total UPI / Card Sales</span>
                <span className="text-xl font-bold text-indigo-600">₹{((reportData?.cardCollected || 0) + (reportData?.upiCollected || 0)).toFixed(2)} (Non-Cash)</span>
              </div>
              <div className="flex justify-between items-center py-4 bg-slate-50 px-4 rounded-xl">
                <span className="text-slate-800 font-bold text-xl">Expected Cash in Drawer</span>
                <span className="text-2xl font-black text-slate-800">₹{expectedCash.toFixed(2)}</span>
              </div>
            </div>

            <div className="bg-amber-50 border border-amber-200 p-6 rounded-2xl">
              <h3 className="text-amber-800 font-bold mb-4">Blind Cash Count</h3>
              <div className="flex gap-4 items-center">
                <div className="relative flex-1">
                  <div className="absolute inset-y-0 left-0 pl-4 flex items-center pointer-events-none">
                    <IndianRupee className="h-6 w-6 text-amber-500" />
                  </div>
                  <input
                    type="number"
                    value={actualCash}
                    onChange={(e) => setActualCash(e.target.value)}
                    className="block w-full pl-12 pr-4 py-4 border-2 border-amber-200 rounded-xl leading-5 bg-white focus:outline-none focus:ring-4 focus:ring-amber-500/20 focus:border-amber-500 sm:text-2xl font-black text-slate-800 transition-all"
                    placeholder="Enter actual physical cash"
                  />
                </div>
                <button
                  onClick={handleCloseShift}
                  disabled={isClosing || parseFloat(actualCash) < 0 || isNaN(parseFloat(actualCash))}
                  className="bg-amber-600 text-white font-bold text-xl px-12 py-4 rounded-xl shadow-lg hover:bg-amber-700 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
                >
                  {isClosing ? 'Closing...' : 'CLOSE SHIFT'}
                </button>
              </div>
              <p className="mt-3 text-sm text-amber-700">
                Any discrepancy between Expected Cash and Actual Cash will be posted to the Overage/Shortage expense account.
              </p>
            </div>
          </div>
        </div>

      </div>
    </div>
  );
};
