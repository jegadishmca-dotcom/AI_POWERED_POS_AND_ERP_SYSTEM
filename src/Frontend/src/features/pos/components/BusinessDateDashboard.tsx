import React, { useState, useEffect } from 'react';
import { 
  getActiveBusinessDate, 
  openBusinessDate, 
  closeBusinessDate, 
  getBusinessDateMetrics, 
  getSessionsSummary,
  ActiveBusinessDateResponse,
  BusinessDateMetricsDto,
  SessionSummaryDto
} from '../api/pos.api';
import { useAuthStore } from '../../auth/store/auth.store';
import { 
  Calendar, 
  Clock, 
  AlertTriangle, 
  Lock, 
  Unlock, 
  FileText, 
  IndianRupee, 
  CreditCard, 
  Smartphone, 
  Wallet,
  Activity,
  User as UserIcon,
  CheckCircle,
  RefreshCw,
  Mail
} from 'lucide-react';

export const BusinessDateDashboard: React.FC = () => {
  const { user } = useAuthStore();

  /**
   * M5: Safe date-only parser.
   * When the backend returns a DateTime like "2026-06-06T00:00:00" (midnight UTC),
   * the browser converts it to the local timezone. In IST (+5:30) that means
   * midnight UTC = 5:30 AM IST on June 6, but in timezones behind UTC (e.g. UTC-6)
   * it becomes June 5! We parse only the date portion to avoid this entirely.
   */
  const formatBusinessDate = (isoString: string): string => {
    // Split on 'T' to get just the date portion "YYYY-MM-DD"
    const datePart = isoString.split('T')[0];
    const [year, month, day] = datePart.split('-').map(Number);
    // Construct using local-time components (avoids timezone conversion)
    const localDate = new Date(year, month - 1, day);
    return localDate.toLocaleDateString('en-IN', { dateStyle: 'long' });
  };

  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [activeDateInfo, setActiveDateInfo] = useState<ActiveBusinessDateResponse | null>(null);
  const [metrics, setMetrics] = useState<BusinessDateMetricsDto | null>(null);
  const [sessions, setSessions] = useState<SessionSummaryDto[]>([]);
  const [selectedOpenDate, setSelectedOpenDate] = useState(() => {
    const today = new Date();
    // format as YYYY-MM-DD in local time
    const yyyy = today.getFullYear();
    const mm = String(today.getMonth() + 1).padStart(2, '0');
    const dd = String(today.getDate()).padStart(2, '0');
    return `${yyyy}-${mm}-${dd}`;
  });

  const [showConfirmModal, setShowConfirmModal] = useState(false);
  const [alertInfo, setAlertInfo] = useState<{ title: string; message: string; type: 'success' | 'error' | 'warning' } | null>(null);

  const loadData = async () => {
    try {
      setLoading(true);
      const activeState = await getActiveBusinessDate();
      setActiveDateInfo(activeState);

      if (activeState.isOpen && activeState.businessDate) {
        // Fetch metrics for active date
        const mData = await getBusinessDateMetrics(activeState.businessDate);
        setMetrics(mData);
      } else {
        setMetrics(null);
      }

      // Fetch all sessions (shifts)
      const sData = await getSessionsSummary();
      setSessions(sData);
    } catch (err) {
      console.error('Failed to load business date dashboard data', err);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadData();
  }, []);

  const handleOpenDay = async () => {
    if (!selectedOpenDate) return;
    try {
      setSubmitting(true);
      const success = await openBusinessDate({
        businessDate: selectedOpenDate,
        openedBy: user?.id
      });
      if (success) {
        setAlertInfo({
          title: 'Day Opened Successfully',
          message: `Business Date ${selectedOpenDate} is now open and operational.`,
          type: 'success'
        });
        await loadData();
      }
    } catch (err: any) {
      console.error(err);
      const msg = err.response?.data ? (typeof err.response.data === 'string' ? err.response.data : JSON.stringify(err.response.data)) : err.message;
      setAlertInfo({
        title: 'Failed to Open Day',
        message: `Error detail: ${msg}`,
        type: 'error'
      });
    } finally {
      setSubmitting(false);
    }
  };

  const handleCloseDay = async () => {
    if (hasOpenShifts) {
      setAlertInfo({
        title: 'Cashier Shifts Still Open',
        message: 'All cashiers must close their shifts and reconcile registers before End-of-Day can be processed.',
        type: 'warning'
      });
      return;
    }
    setShowConfirmModal(true);
  };

  const handleCloseDayConfirm = async () => {
    setShowConfirmModal(false);
    try {
      setSubmitting(true);
      const result = await closeBusinessDate({
        closedBy: user?.id
      });
      if (result.success) {
        setAlertInfo({
          title: 'End-of-Day Processed',
          message: `Business Date closed successfully!\nThe daily sales email report has been compiled and queued for dispatch to the owner list.`,
          type: 'success'
        });
        await loadData();
      }
    } catch (err: any) {
      console.error(err);
      const msg = err.response?.data ? (typeof err.response.data === 'string' ? err.response.data : JSON.stringify(err.response.data)) : err.message;
      setAlertInfo({
        title: 'End-of-Day Failed',
        message: `Error detail: ${msg}`,
        type: 'error'
      });
    } finally {
      setSubmitting(false);
    }
  };

  const hasOpenShifts = sessions.some(s => s.status === 'OPEN');

  if (loading) {
    return (
      <div className="min-h-[60vh] flex items-center justify-center">
        <div className="flex flex-col items-center space-y-4">
          <RefreshCw className="w-10 h-10 text-indigo-600 animate-spin" />
          <p className="text-slate-500 font-medium">Loading Business Date state...</p>
        </div>
      </div>
    );
  }

  return (
    <div className="p-6 max-w-7xl mx-auto space-y-6">
      {/* Header section */}
      <div className="flex flex-col md:flex-row md:items-center md:justify-between gap-4">
        <div>
          <h1 className="text-3xl font-extrabold text-slate-800 dark:text-white flex items-center gap-2">
            <Activity className="w-8 h-8 text-indigo-600" />
            Business Date & EOD Operations
          </h1>
          <p className="text-sm text-slate-500 dark:text-slate-400 mt-1">
            Manage operational calendar days, review cashier shift statuses, and execute store-wide End-of-Day locks.
          </p>
        </div>
        <button 
          onClick={loadData} 
          disabled={submitting}
          className="flex items-center justify-center px-4 py-2 bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 hover:bg-slate-50 dark:hover:bg-slate-700 rounded-lg text-sm font-semibold text-slate-700 dark:text-slate-200 shadow-sm transition-all duration-200 gap-1.5"
        >
          <RefreshCw className={`w-4 h-4 ${submitting ? 'animate-spin' : ''}`} />
          Refresh Dashboard
        </button>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        
        {/* Left column: Operational Status & Controls */}
        <div className="lg:col-span-1 space-y-6">
          
          {/* Status Card */}
          <div className="bg-white dark:bg-slate-800 rounded-2xl border border-slate-100 dark:border-slate-700 p-6 shadow-sm overflow-hidden relative">
            <div className="absolute top-0 right-0 w-24 h-24 bg-indigo-500/5 rounded-full blur-xl"></div>
            
            <h2 className="text-lg font-bold text-slate-800 dark:text-white mb-4 flex items-center gap-2">
              <CheckCircle className="w-5 h-5 text-indigo-600" />
              Operational Status
            </h2>

            {activeDateInfo?.isOpen ? (
              <div className="space-y-4">
                <div className="p-4 rounded-xl bg-emerald-50 dark:bg-emerald-950/30 border border-emerald-100 dark:border-emerald-900/40 flex items-center gap-4">
                  <div className="w-10 h-10 rounded-full bg-emerald-100 dark:bg-emerald-900 flex items-center justify-center text-emerald-600 dark:text-emerald-300 shadow-sm">
                    <Unlock className="w-5 h-5 animate-pulse" />
                  </div>
                  <div>
                    <p className="text-[11px] font-bold text-emerald-600 dark:text-emerald-400 uppercase tracking-wider">Store Day Open</p>
                    <p className="text-xl font-black text-emerald-800 dark:text-emerald-200">
                      {formatBusinessDate(activeDateInfo.businessDate!)}
                    </p>
                  </div>
                </div>

                <div className="text-xs text-slate-400 space-y-1">
                  <p className="flex items-center gap-1">
                    <Clock className="w-3.5 h-3.5" /> Opened At: {new Date(activeDateInfo.openedAt!).toLocaleString()}
                  </p>
                </div>

                <hr className="border-slate-100 dark:border-slate-700" />

                {/* Close day button */}
                <div className="space-y-3">
                  <div className="p-3 bg-amber-50 dark:bg-amber-950/20 border border-amber-100 dark:border-amber-900/30 rounded-xl flex items-start gap-2.5">
                    <AlertTriangle className="w-4.5 h-4.5 text-amber-500 shrink-0 mt-0.5" />
                    <p className="text-xs text-amber-700 dark:text-amber-300 leading-relaxed">
                      <strong>EOD Caution:</strong> Performing End-of-Day locks all invoices for this date. Ensure all cashiers have closed their registers before finalizing.
                    </p>
                  </div>

                  {hasOpenShifts && (
                    <div className="p-3 bg-red-50 dark:bg-red-950/20 border border-red-100 dark:border-red-900/30 rounded-xl flex items-start gap-2.5">
                      <AlertTriangle className="w-4.5 h-4.5 text-red-500 shrink-0 mt-0.5" />
                      <p className="text-xs text-red-700 dark:text-red-300 font-semibold leading-relaxed">
                        Cannot close day! There are active open cashier shifts. Please close them first.
                      </p>
                    </div>
                  )}

                  <button
                    onClick={handleCloseDay}
                    disabled={submitting || hasOpenShifts}
                    className={`w-full py-3 px-4 rounded-xl font-bold text-white shadow-lg transition-all duration-200 flex items-center justify-center gap-2 ${
                      hasOpenShifts 
                        ? 'bg-slate-300 dark:bg-slate-700 text-slate-400 dark:text-slate-500 cursor-not-allowed shadow-none' 
                        : 'bg-gradient-to-r from-red-600 to-rose-600 hover:from-red-500 hover:to-rose-500 shadow-red-500/20 hover:scale-[1.01]'
                    }`}
                  >
                    <Lock className="w-4 h-4" />
                    {submitting ? 'Executing EOD...' : 'Close Business Day (EOD)'}
                  </button>
                </div>
              </div>
            ) : (
              <div className="space-y-4">
                <div className="p-4 rounded-xl bg-slate-100 dark:bg-slate-800/60 border border-slate-200 dark:border-slate-700 flex items-center gap-4">
                  <div className="w-10 h-10 rounded-full bg-slate-200 dark:bg-slate-700 flex items-center justify-center text-slate-500 dark:text-slate-400 shadow-sm">
                    <Lock className="w-5 h-5" />
                  </div>
                  <div>
                    <p className="text-[11px] font-bold text-slate-500 dark:text-slate-400 uppercase tracking-wider">Store Closed</p>
                    <p className="text-xl font-black text-slate-800 dark:text-slate-200">Day Is Closed</p>
                  </div>
                </div>

                <hr className="border-slate-100 dark:border-slate-700" />

                {/* Open day form */}
                <div className="space-y-3">
                  <label className="block text-xs font-bold text-slate-500 dark:text-slate-400 uppercase tracking-wider">
                    Select Business Date to Open
                  </label>
                  <div className="relative">
                    <Calendar className="w-4 h-4 text-slate-400 absolute left-3 top-3.5" />
                    <input 
                      type="date" 
                      value={selectedOpenDate}
                      onChange={(e) => setSelectedOpenDate(e.target.value)}
                      className="w-full pl-10 pr-3 py-2.5 bg-slate-50 dark:bg-slate-900 border border-slate-200 dark:border-slate-700 rounded-xl text-slate-800 dark:text-white text-sm font-semibold focus:outline-none focus:ring-2 focus:ring-indigo-500"
                    />
                  </div>

                  <button
                    onClick={handleOpenDay}
                    disabled={submitting || !selectedOpenDate}
                    className="w-full py-3 px-4 bg-gradient-to-r from-emerald-600 to-teal-600 hover:from-emerald-500 hover:to-teal-500 text-white font-bold rounded-xl shadow-lg shadow-emerald-500/20 hover:scale-[1.01] transition-all duration-200 flex items-center justify-center gap-2"
                  >
                    <Unlock className="w-4 h-4" />
                    {submitting ? 'Opening Day...' : 'Open Business Day'}
                  </button>
                </div>
              </div>
            )}
          </div>
        </div>

        {/* Right column: Sales Summary KPI & Shift monitor */}
        <div className="lg:col-span-2 space-y-6">
          
          {/* Active Day Sales Summary */}
          {activeDateInfo?.isOpen && metrics && (
            <div className="bg-white dark:bg-slate-800 rounded-2xl border border-slate-100 dark:border-slate-700 p-6 shadow-sm">
              <h2 className="text-lg font-bold text-slate-800 dark:text-white mb-4 flex items-center gap-2">
                <FileText className="w-5 h-5 text-indigo-600" />
                Sales Summary ({new Date(activeDateInfo.businessDate!).toLocaleDateString('en-IN')})
              </h2>

              <div className="grid grid-cols-2 sm:grid-cols-4 gap-4 mb-6">
                <div className="p-4 rounded-xl bg-slate-50 dark:bg-slate-900 border border-slate-100 dark:border-slate-800">
                  <p className="text-xs font-semibold text-slate-400">Total Invoices</p>
                  <p className="text-2xl font-black text-slate-800 dark:text-white mt-1">{metrics.totalInvoices}</p>
                </div>
                <div className="p-4 rounded-xl bg-slate-50 dark:bg-slate-900 border border-slate-100 dark:border-slate-800">
                  <p className="text-xs font-semibold text-slate-400">Gross Revenue</p>
                  <p className="text-2xl font-black text-indigo-600 dark:text-indigo-400 mt-1">₹{metrics.totalSales.toLocaleString('en-IN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}</p>
                </div>
                <div className="p-4 rounded-xl bg-slate-50 dark:bg-slate-900 border border-slate-100 dark:border-slate-800">
                  <p className="text-xs font-semibold text-slate-400">Tax Collected</p>
                  <p className="text-2xl font-black text-slate-800 dark:text-white mt-1">₹{metrics.totalTax.toLocaleString('en-IN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}</p>
                </div>
                <div className="p-4 rounded-xl bg-slate-50 dark:bg-slate-900 border border-slate-100 dark:border-slate-800">
                  <p className="text-xs font-semibold text-slate-400">Discounts Allowed</p>
                  <p className="text-2xl font-black text-red-500 mt-1">₹{metrics.totalDiscount.toLocaleString('en-IN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}</p>
                </div>
              </div>

              <h3 className="text-xs font-bold text-slate-500 dark:text-slate-400 uppercase tracking-wider mb-3">Tender Breakdown</h3>
              <div className="grid grid-cols-1 sm:grid-cols-4 gap-4">
                
                {/* Cash */}
                <div className="p-3 bg-slate-50 dark:bg-slate-900 border border-slate-100 dark:border-slate-800 rounded-xl space-y-2">
                  <div className="flex items-center justify-between text-slate-500">
                    <span className="text-xs font-semibold flex items-center gap-1"><IndianRupee className="w-3.5 h-3.5" /> Cash</span>
                    <span className="text-xs font-bold text-slate-800 dark:text-white">₹{metrics.cashCollected.toFixed(2)}</span>
                  </div>
                  <div className="w-full bg-slate-200 dark:bg-slate-800 rounded-full h-1.5 overflow-hidden">
                    <div className="bg-emerald-500 h-1.5 rounded-full" style={{ width: `${metrics.totalSales > 0 ? (metrics.cashCollected / metrics.totalSales) * 100 : 0}%` }}></div>
                  </div>
                </div>

                {/* UPI */}
                <div className="p-3 bg-slate-50 dark:bg-slate-900 border border-slate-100 dark:border-slate-800 rounded-xl space-y-2">
                  <div className="flex items-center justify-between text-slate-500">
                    <span className="text-xs font-semibold flex items-center gap-1"><Smartphone className="w-3.5 h-3.5" /> UPI</span>
                    <span className="text-xs font-bold text-slate-800 dark:text-white">₹{metrics.upiCollected.toFixed(2)}</span>
                  </div>
                  <div className="w-full bg-slate-200 dark:bg-slate-800 rounded-full h-1.5 overflow-hidden">
                    <div className="bg-indigo-500 h-1.5 rounded-full" style={{ width: `${metrics.totalSales > 0 ? (metrics.upiCollected / metrics.totalSales) * 100 : 0}%` }}></div>
                  </div>
                </div>

                {/* Card */}
                <div className="p-3 bg-slate-50 dark:bg-slate-900 border border-slate-100 dark:border-slate-800 rounded-xl space-y-2">
                  <div className="flex items-center justify-between text-slate-500">
                    <span className="text-xs font-semibold flex items-center gap-1"><CreditCard className="w-3.5 h-3.5" /> Card</span>
                    <span className="text-xs font-bold text-slate-800 dark:text-white">₹{metrics.cardCollected.toFixed(2)}</span>
                  </div>
                  <div className="w-full bg-slate-200 dark:bg-slate-800 rounded-full h-1.5 overflow-hidden">
                    <div className="bg-amber-500 h-1.5 rounded-full" style={{ width: `${metrics.totalSales > 0 ? (metrics.cardCollected / metrics.totalSales) * 100 : 0}%` }}></div>
                  </div>
                </div>

                {/* Wallet */}
                <div className="p-3 bg-slate-50 dark:bg-slate-900 border border-slate-100 dark:border-slate-800 rounded-xl space-y-2">
                  <div className="flex items-center justify-between text-slate-500">
                    <span className="text-xs font-semibold flex items-center gap-1"><Wallet className="w-3.5 h-3.5" /> Wallet</span>
                    <span className="text-xs font-bold text-slate-800 dark:text-white">₹{metrics.walletCollected.toFixed(2)}</span>
                  </div>
                  <div className="w-full bg-slate-200 dark:bg-slate-800 rounded-full h-1.5 overflow-hidden">
                    <div className="bg-violet-500 h-1.5 rounded-full" style={{ width: `${metrics.totalSales > 0 ? (metrics.walletCollected / metrics.totalSales) * 100 : 0}%` }}></div>
                  </div>
                </div>

              </div>
            </div>
          )}

          {/* Shift Session Monitor */}
          <div className="bg-white dark:bg-slate-800 rounded-2xl border border-slate-100 dark:border-slate-700 p-6 shadow-sm">
            <h2 className="text-lg font-bold text-slate-800 dark:text-white mb-4 flex items-center gap-2">
              <Clock className="w-5 h-5 text-indigo-600" />
              Terminal Shifts Monitor
            </h2>

            {sessions.length === 0 ? (
              <div className="p-8 text-center text-slate-400">
                No shift sessions recorded for the active operating cycles.
              </div>
            ) : (
              <div className="overflow-x-auto">
                <table className="w-full text-left border-collapse">
                  <thead>
                    <tr className="border-b border-slate-100 dark:border-slate-700 text-xs font-bold text-slate-400 uppercase tracking-wider">
                      <th className="py-3 px-4">Terminal</th>
                      <th className="py-3 px-4">Cashier</th>
                      <th className="py-3 px-4">Opened</th>
                      <th className="py-3 px-4">Closed</th>
                      <th className="py-3 px-4 text-right">Float</th>
                      <th className="py-3 px-4 text-right">Variance</th>
                      <th className="py-3 px-4 text-center">Status</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-slate-100 dark:divide-slate-700/50">
                    {sessions.map((s) => (
                      <tr key={s.id} className="text-sm hover:bg-slate-50/50 dark:hover:bg-slate-700/20 transition-all duration-150">
                        <td className="py-3.5 px-4 font-bold text-slate-800 dark:text-slate-200">{s.terminalCode}</td>
                        <td className="py-3.5 px-4 text-slate-600 dark:text-slate-400 flex items-center gap-1.5">
                          <UserIcon className="w-3.5 h-3.5 text-slate-400" />
                          {s.cashierName}
                        </td>
                        <td className="py-3.5 px-4 text-slate-500 text-xs">
                          {new Date(s.startTime).toLocaleString('en-IN', { hour: 'numeric', minute: '2-digit', hour12: true })}
                        </td>
                        <td className="py-3.5 px-4 text-slate-500 text-xs">
                          {s.endTime ? new Date(s.endTime).toLocaleString('en-IN', { hour: 'numeric', minute: '2-digit', hour12: true }) : '-'}
                        </td>
                        <td className="py-3.5 px-4 text-right font-medium text-slate-800 dark:text-slate-300">
                          ₹{s.openingFloatCash.toFixed(2)}
                        </td>
                        <td className={`py-3.5 px-4 text-right font-semibold ${s.difference < 0 ? 'text-red-500' : s.difference > 0 ? 'text-emerald-500' : 'text-slate-500'}`}>
                          {s.difference === 0 ? '₹0.00' : `${s.difference > 0 ? '+' : ''}₹${s.difference.toFixed(2)}`}
                        </td>
                        <td className="py-3.5 px-4 text-center">
                          <span className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-bold leading-5 shadow-sm ${
                            s.status === 'OPEN'
                              ? 'bg-amber-100 text-amber-800 dark:bg-amber-950 dark:text-amber-300 animate-pulse'
                              : 'bg-emerald-100 text-emerald-800 dark:bg-emerald-950 dark:text-emerald-300'
                          }`}>
                            {s.status}
                          </span>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </div>

        </div>

      </div>

      {showConfirmModal && (
        <div className="fixed inset-0 bg-slate-900/80 z-[100] flex items-center justify-center p-4 backdrop-blur-sm">
          <div className="bg-white dark:bg-slate-800 rounded-2xl shadow-2xl w-full max-w-lg overflow-hidden transform transition-all border border-slate-100 dark:border-slate-700">
            <div className="bg-gradient-to-r from-red-600 to-rose-600 p-6 text-white">
              <div className="flex items-center gap-3">
                <Lock className="w-6 h-6 text-white shrink-0" />
                <h2 className="text-xl font-bold">Confirm End-of-Day (EOD)</h2>
              </div>
              <p className="text-red-100 text-xs mt-1">
                Are you sure you want to close and lock the operational business date?
              </p>
            </div>
            
            <div className="p-6 space-y-4">
              <div className="p-4 rounded-xl bg-slate-50 dark:bg-slate-900 border border-slate-100 dark:border-slate-800 flex items-center justify-between">
                <span className="text-sm font-semibold text-slate-500">Target Business Date:</span>
                <span className="text-lg font-extrabold text-slate-800 dark:text-white">
                  {activeDateInfo?.businessDate ? new Date(activeDateInfo.businessDate).toLocaleDateString('en-IN', { dateStyle: 'long' }) : ''}
                </span>
              </div>

              <div className="space-y-3">
                <p className="text-xs font-bold text-slate-400 uppercase tracking-wider">Operational Impacts:</p>
                
                <div className="space-y-2">
                  <div className="flex items-start gap-2.5 text-slate-600 dark:text-slate-300 text-sm">
                    <CheckCircle className="w-4 h-4 text-emerald-500 shrink-0 mt-0.5" />
                    <span><strong>Lock Invoices:</strong> All transactions for this business date will be sealed and locked against modifications or new invoices.</span>
                  </div>
                  <div className="flex items-start gap-2.5 text-slate-600 dark:text-slate-300 text-sm">
                    <CheckCircle className="w-4 h-4 text-emerald-500 shrink-0 mt-0.5" />
                    <span><strong>Reconcile Ledgers:</strong> Registers and drawer counts will be reconciled and locked.</span>
                  </div>
                  <div className="flex items-start gap-2.5 text-slate-600 dark:text-slate-300 text-sm">
                    <CheckCircle className="w-4 h-4 text-emerald-500 shrink-0 mt-0.5" />
                    <span><strong>Daily Report Email:</strong> A full sales report will be automatically compiled and emailed to the owner/manager list.</span>
                  </div>
                </div>
              </div>

              <div className="grid grid-cols-2 gap-3 pt-2">
                <button
                  onClick={() => setShowConfirmModal(false)}
                  className="py-3 px-4 bg-slate-100 hover:bg-slate-200 dark:bg-slate-700 dark:hover:bg-slate-600 text-slate-700 dark:text-slate-200 font-bold rounded-xl transition-colors text-sm"
                >
                  Cancel
                </button>
                <button
                  onClick={handleCloseDayConfirm}
                  disabled={submitting}
                  className="py-3 px-4 bg-gradient-to-r from-red-600 to-rose-600 hover:from-red-500 hover:to-rose-500 text-white font-bold rounded-xl transition-all shadow-md shadow-red-500/10 text-sm"
                >
                  {submitting ? 'Executing EOD...' : 'Confirm & Close Day'}
                </button>
              </div>
            </div>
          </div>
        </div>
      )}

      {alertInfo && (
        <div className="fixed inset-0 bg-slate-900/80 z-[110] flex items-center justify-center p-4 backdrop-blur-sm animate-fade-in">
          <div className="bg-white dark:bg-slate-800 rounded-2xl shadow-2xl w-full max-w-md overflow-hidden transform transition-all border border-slate-100 dark:border-slate-700">
            <div className={`p-6 flex flex-col items-center justify-center text-white text-center ${
              alertInfo.type === 'success' ? 'bg-emerald-600' : alertInfo.type === 'error' ? 'bg-red-600' : 'bg-amber-600'
            }`}>
              {alertInfo.type === 'success' ? (
                <CheckCircle className="w-12 h-12 mb-2" />
              ) : alertInfo.type === 'error' ? (
                <AlertTriangle className="w-12 h-12 mb-2" />
              ) : (
                <AlertTriangle className="w-12 h-12 mb-2" />
              )}
              <h2 className="text-xl font-bold">{alertInfo.title}</h2>
            </div>
            <div className="p-6 text-center space-y-4">
              <p className="text-slate-600 dark:text-slate-300 text-sm whitespace-pre-line leading-relaxed">
                {alertInfo.message}
              </p>
              <button
                onClick={() => setAlertInfo(null)}
                className={`w-full py-2.5 px-4 text-white font-bold rounded-xl shadow-md transition-colors ${
                  alertInfo.type === 'success' 
                    ? 'bg-emerald-600 hover:bg-emerald-700' 
                    : alertInfo.type === 'error' 
                      ? 'bg-red-600 hover:bg-red-700' 
                      : 'bg-amber-600 hover:bg-amber-700'
                }`}
              >
                OK
              </button>
            </div>
          </div>
        </div>
      )}

    </div>
  );
};
