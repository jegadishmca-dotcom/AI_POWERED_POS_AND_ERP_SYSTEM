import React, { useState, useEffect } from 'react';
import { Database, ShieldCheck, Key, RefreshCw, AlertTriangle, Eye, EyeOff, Lock, HelpCircle } from 'lucide-react';
import { api } from '../../../utils/api';
import { useAuthStore } from '../../auth/store/auth.store';

export const EnvironmentConfig: React.FC = () => {
  const [activeMode, setActiveMode] = useState('LIVE');
  const [deploymentMode, setDeploymentMode] = useState('SelfHosted');
  const [tenantName, setTenantName] = useState<string | null>(null);
  
  // UI States
  const [loading, setLoading] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [successMsg, setSuccessMsg] = useState<string | null>(null);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);
  
  // Confirmation and Password flow
  const [showConfirm, setShowConfirm] = useState(false);
  const [showPasswordModal, setShowPasswordModal] = useState(false);
  const [developerPassword, setDeveloperPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  
  // Lockout info
  const [lockoutUntil, setLockoutUntil] = useState<string | null>(null);

  const { user, clearAuth } = useAuthStore();
  const hasAccess = user?.role === 'Owner' || user?.role === 'Manager' || user?.role === 'Developer';

  const fetchEnvironmentMode = async () => {
    try {
      setLoading(true);
      setErrorMsg(null);
      const res = await api.get('/api/environment/mode');
      setActiveMode(res.data.activeMode || 'LIVE');
      setDeploymentMode(res.data.deploymentMode || 'SelfHosted');
      setTenantName(res.data.tenantName || null);
    } catch (err: any) {
      console.error('Failed to load environment mode', err);
      setErrorMsg('Failed to load environment configuration from server.');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchEnvironmentMode();
  }, []);

  const handleToggleClick = () => {
    setErrorMsg(null);
    setSuccessMsg(null);
    setDeveloperPassword('');
    setShowConfirm(true);
  };

  const confirmSwitch = () => {
    setShowConfirm(false);
    setShowPasswordModal(true);
  };

  const handleToggleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!developerPassword.trim()) {
      setErrorMsg('Developer password is required.');
      return;
    }

    try {
      setSubmitting(true);
      setErrorMsg(null);
      setSuccessMsg(null);

      const targetMode = activeMode === 'LIVE' ? 'UAT' : 'LIVE';

      const res = await api.post('/api/environment/toggle', {
        developerPassword,
        targetMode
      });

      setSuccessMsg(res.data.message || `Switched to ${targetMode} mode successfully. Logging out...`);
      setShowPasswordModal(false);
      
      // Auto logout and redirect after 3 seconds for a clean session restart
      setTimeout(() => {
        clearAuth();
        window.location.href = '/login';
      }, 3000);
    } catch (err: any) {
      console.error('Environment toggle failed', err);
      if (err.response?.status === 429) {
        const lockoutTime = err.response.data.lockoutUntil;
        setLockoutUntil(lockoutTime);
        setErrorMsg(err.response.data.error || 'Maximum attempts exceeded. Toggle locked for 15 minutes.');
      } else if (err.response?.status === 401) {
        const remaining = err.response.data.attemptsRemaining;
        setErrorMsg(`Developer password incorrect. Attempts remaining: ${remaining ?? 'unknown'}`);
      } else {
        setErrorMsg(err.response?.data?.error || 'Failed to switch environment mode. Check server logs.');
      }
    } finally {
      setSubmitting(false);
    }
  };

  if (!hasAccess) {
    return (
      <div className="bg-white p-8 rounded-2xl border border-slate-100 shadow-lg max-w-2xl text-center">
        <AlertTriangle className="w-12 h-12 text-amber-500 mx-auto mb-4" />
        <h3 className="text-xl font-black text-slate-800 mb-2">Access Denied</h3>
        <p className="text-sm text-slate-500 font-medium">You do not have permissions to modify the active database environment mode.</p>
      </div>
    );
  }

  const targetMode = activeMode === 'LIVE' ? 'UAT' : 'LIVE';

  return (
    <div className="bg-white p-8 rounded-2xl border border-slate-100 shadow-lg max-w-2xl relative">
      {/* Header */}
      <div className="flex items-center gap-3 mb-6 pb-4 border-b border-slate-100">
        <Database className="w-7 h-7 text-indigo-600" />
        <div>
          <h3 className="text-xl font-black text-slate-800">Database Environment Mode</h3>
          <p className="text-xs text-slate-500 font-medium font-sans">
            Switch between UAT sandbox (mock operations) and LIVE production ledger
          </p>
        </div>
      </div>

      {loading ? (
        <div className="py-12 flex flex-col items-center justify-center space-y-2">
          <RefreshCw className="w-8 h-8 text-indigo-600 animate-spin" />
          <span className="text-xs text-slate-500 font-semibold">Retrieving environment configuration...</span>
        </div>
      ) : (
        <div className="space-y-6 font-sans">
          {errorMsg && (
            <div className="p-3 bg-red-50 text-red-700 rounded-xl text-xs font-bold border border-red-100 flex items-start space-x-2 animate-pulse">
              <AlertTriangle className="w-4 h-4 text-red-500 shrink-0 mt-0.5" />
              <div>
                <div>{errorMsg}</div>
                {lockoutUntil && (
                  <div className="text-[10px] text-red-500 mt-1">
                    Lockout active until: {new Date(lockoutUntil).toLocaleTimeString()}
                  </div>
                )}
              </div>
            </div>
          )}

          {successMsg && (
            <div className="p-3 bg-green-50 text-green-700 rounded-xl text-xs font-bold border border-green-100 flex items-start space-x-2">
              <ShieldCheck className="w-4 h-4 text-green-500 shrink-0 mt-0.5" />
              <span>{successMsg}</span>
            </div>
          )}

          {/* Current Status Panel */}
          <div className="p-6 bg-slate-50 rounded-2xl border border-slate-100 flex flex-col md:flex-row md:items-center justify-between gap-4">
            <div>
              <span className="text-[10px] text-slate-400 font-bold uppercase tracking-wider block mb-1">Current Active Environment</span>
              <div className="flex items-center gap-2">
                <span className={`px-3 py-1 rounded-full text-xs font-black shadow-sm tracking-wide ${
                  activeMode === 'LIVE' ? 'bg-emerald-100 text-emerald-800' : 'bg-amber-100 text-amber-800'
                }`}>
                  {activeMode}
                </span>
                {tenantName && (
                  <span className="text-xs text-slate-600 font-semibold">
                    ({tenantName})
                  </span>
                )}
              </div>
              <p className="text-xs text-slate-500 mt-2 max-w-sm">
                {activeMode === 'LIVE'
                  ? 'All sales, payments, and batch movements are final and post directly to the general ledger.'
                  : 'Operating in sandbox mode. Transactions are isolated from the production ledger.'}
              </p>
            </div>

            <button
              onClick={handleToggleClick}
              disabled={submitting}
              className={`px-5 py-3 font-bold rounded-xl text-sm shadow-md transition-all flex items-center gap-2 shrink-0 ${
                activeMode === 'LIVE'
                  ? 'bg-amber-600 hover:bg-amber-700 text-white'
                  : 'bg-emerald-600 hover:bg-emerald-700 text-white'
              }`}
            >
              <RefreshCw className={`w-4 h-4 ${submitting ? 'animate-spin' : ''}`} />
              Switch to {targetMode}
            </button>
          </div>

          {/* Info Details */}
          <div className="p-4 bg-indigo-50/50 rounded-xl border border-indigo-50 text-xs text-indigo-800 space-y-2">
            <h4 className="font-bold flex items-center gap-1"><HelpCircle className="w-3.5 h-3.5" /> Deployment Details</h4>
            <ul className="list-disc pl-4 space-y-1 font-medium">
              <li>Deployment Architecture: <strong>{deploymentMode}</strong></li>
              {deploymentMode === 'SelfHosted' ? (
                <li>Switching modes triggers a container restart (takes 5-10 seconds to reload).</li>
              ) : (
                <li>SaaS tenant mode switches instantly without restarting the service container.</li>
              )}
              <li>Requires <strong>System Developer password</strong> approval to switch environments.</li>
            </ul>
          </div>
        </div>
      )}

      {/* ─── Step 1: Confirmation Dialog ─── */}
      {showConfirm && (
        <div className="fixed inset-0 bg-slate-900/60 backdrop-blur-sm flex items-center justify-center p-4 z-50 animate-fadeIn">
          <div className="bg-white rounded-2xl p-6 border border-slate-100 shadow-2xl max-w-md w-full text-center space-y-4">
            <AlertTriangle className="w-12 h-12 text-amber-500 mx-auto" />
            <h4 className="text-lg font-black text-slate-800">Confirm Environment Switch</h4>
            <p className="text-sm text-slate-500 font-medium">
              Switching to <strong>{targetMode}</strong> requires a session restart. All active checkout sessions will be logged out and you will be forced to re-authenticate. Continue?
            </p>
            <div className="flex gap-3 justify-center pt-2">
              <button
                onClick={() => setShowConfirm(false)}
                className="px-4 py-2 bg-slate-100 hover:bg-slate-200 text-slate-700 font-bold rounded-xl text-sm"
              >
                No, Cancel
              </button>
              <button
                onClick={confirmSwitch}
                className="px-4 py-2 bg-amber-600 hover:bg-amber-700 text-white font-bold rounded-xl text-sm"
              >
                Yes, Continue
              </button>
            </div>
          </div>
        </div>
      )}

      {/* ─── Step 2: Developer Password Modal ─── */}
      {showPasswordModal && (
        <div className="fixed inset-0 bg-slate-900/60 backdrop-blur-sm flex items-center justify-center p-4 z-50 animate-fadeIn">
          <div className="bg-white rounded-2xl p-6 border border-slate-100 shadow-2xl max-w-md w-full space-y-4">
            <div className="flex items-center gap-3">
              <div className="p-2 bg-indigo-50 text-indigo-600 rounded-lg">
                <Lock className="w-5 h-5" />
              </div>
              <div>
                <h4 className="font-black text-slate-850">Developer Approval Required</h4>
                <p className="text-xs text-slate-400 font-semibold">Input password to authorize {targetMode} mode</p>
              </div>
            </div>

            <form onSubmit={handleToggleSubmit} className="space-y-4">
              <div className="space-y-1">
                <label className="text-xs font-black text-slate-500 uppercase flex items-center gap-1.5">
                  <Key className="w-3.5 h-3.5" /> Developer Password
                </label>
                <div className="relative">
                  <input
                    type={showPassword ? 'text' : 'password'}
                    placeholder="Enter Developer Account Password"
                    value={developerPassword}
                    onChange={(e) => setDeveloperPassword(e.target.value)}
                    required
                    className="w-full px-4 py-2.5 border border-slate-200 rounded-xl text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 pr-10"
                  />
                  <button
                    type="button"
                    onClick={() => setShowPassword(!showPassword)}
                    className="absolute inset-y-0 right-0 pr-3 flex items-center text-slate-400 hover:text-slate-600"
                  >
                    {showPassword ? <EyeOff className="w-4 h-4" /> : <Eye className="w-4 h-4" />}
                  </button>
                </div>
              </div>

              <div className="flex gap-3 justify-end pt-2">
                <button
                  type="button"
                  onClick={() => setShowPasswordModal(false)}
                  disabled={submitting}
                  className="px-4 py-2 bg-slate-100 hover:bg-slate-200 text-slate-700 font-bold rounded-xl text-sm disabled:opacity-50"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={submitting}
                  className="px-4 py-2 bg-indigo-600 hover:bg-indigo-700 text-white font-bold rounded-xl text-sm flex items-center gap-1.5 shadow-md disabled:opacity-50"
                >
                  {submitting ? (
                    <>
                      <RefreshCw className="w-4 h-4 animate-spin" /> Authorizing...
                    </>
                  ) : (
                    'Approve Switch'
                  )}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
};
