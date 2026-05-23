import React, { useState } from 'react';
import { ShieldCheck, KeyRound, Eye, EyeOff, CheckCircle2, AlertCircle } from 'lucide-react';
import { api } from '../../../services/api';

/**
 * ChangeOverridePinPanel
 * Shown in the user profile / settings page.
 * Allows the logged-in Owner/Manager to change their Manager Override PIN.
 */
export const ChangeOverridePinPanel = () => {
  const [newPin,     setNewPin]     = useState('');
  const [confirmPin, setConfirmPin] = useState('');
  const [showNew,    setShowNew]    = useState(false);
  const [showConf,   setShowConf]   = useState(false);
  const [loading,    setLoading]    = useState(false);
  const [result,     setResult]     = useState<{ ok: boolean; msg: string } | null>(null);

  const handleSave = async () => {
    setResult(null);
    if (!newPin || newPin.length < 4) {
      setResult({ ok: false, msg: 'PIN must be at least 4 digits.' });
      return;
    }
    if (!/^\d+$/.test(newPin)) {
      setResult({ ok: false, msg: 'PIN must contain only digits (0–9).' });
      return;
    }
    if (newPin !== confirmPin) {
      setResult({ ok: false, msg: 'PINs do not match. Please re-enter.' });
      return;
    }

    setLoading(true);
    try {
      await api.post('/auth/set-override-pin', { newPin, confirmPin });
      setResult({ ok: true, msg: 'Override PIN changed successfully!' });
      setNewPin('');
      setConfirmPin('');
    } catch (err: any) {
      const msg = err?.response?.data?.detail || err?.response?.data?.message || 'Failed to update PIN. Please try again.';
      setResult({ ok: false, msg });
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="bg-white rounded-2xl border border-gray-200 shadow-sm p-6 max-w-md">
      {/* Header */}
      <div className="flex items-center gap-3 mb-6">
        <div className="bg-red-100 p-2.5 rounded-xl">
          <ShieldCheck className="w-6 h-6 text-red-600" />
        </div>
        <div>
          <h3 className="font-bold text-gray-800 text-lg">Manager Override PIN</h3>
          <p className="text-sm text-gray-500">Used to authorise void items and manager actions at POS</p>
        </div>
      </div>

      {/* Security note */}
      <div className="bg-amber-50 border border-amber-200 rounded-xl p-3 mb-5 text-sm text-amber-800 flex gap-2">
        <KeyRound className="w-4 h-4 mt-0.5 shrink-0" />
        <span>
          Choose a <strong>4–8 digit PIN</strong> that only you know.
          Do not share it with cashiers. The PIN is stored securely (hashed) in the database.
        </span>
      </div>

      {/* New PIN */}
      <div className="mb-4">
        <label className="block text-sm font-semibold text-gray-700 mb-1">New PIN</label>
        <div className="relative">
          <input
            type={showNew ? 'text' : 'password'}
            value={newPin}
            onChange={e => setNewPin(e.target.value.replace(/\D/g, '').slice(0, 8))}
            placeholder="Enter 4–8 digit PIN"
            inputMode="numeric"
            maxLength={8}
            className="w-full border-2 border-gray-300 rounded-xl px-4 py-3 text-2xl tracking-widest focus:border-red-500 outline-none pr-12"
          />
          <button
            type="button"
            className="absolute right-3 top-1/2 -translate-y-1/2 text-gray-400 hover:text-gray-600"
            onClick={() => setShowNew(v => !v)}
          >
            {showNew ? <EyeOff className="w-5 h-5" /> : <Eye className="w-5 h-5" />}
          </button>
        </div>
        <p className="text-xs text-gray-400 mt-1">{newPin.length}/8 digits</p>
      </div>

      {/* Confirm PIN */}
      <div className="mb-5">
        <label className="block text-sm font-semibold text-gray-700 mb-1">Confirm PIN</label>
        <div className="relative">
          <input
            type={showConf ? 'text' : 'password'}
            value={confirmPin}
            onChange={e => setConfirmPin(e.target.value.replace(/\D/g, '').slice(0, 8))}
            placeholder="Re-enter PIN to confirm"
            inputMode="numeric"
            maxLength={8}
            className="w-full border-2 border-gray-300 rounded-xl px-4 py-3 text-2xl tracking-widest focus:border-red-500 outline-none pr-12"
          />
          <button
            type="button"
            className="absolute right-3 top-1/2 -translate-y-1/2 text-gray-400 hover:text-gray-600"
            onClick={() => setShowConf(v => !v)}
          >
            {showConf ? <EyeOff className="w-5 h-5" /> : <Eye className="w-5 h-5" />}
          </button>
        </div>
        {newPin && confirmPin && (
          <p className={`text-xs mt-1 ${newPin === confirmPin ? 'text-green-600' : 'text-red-500'}`}>
            {newPin === confirmPin ? '✓ PINs match' : '✗ PINs do not match'}
          </p>
        )}
      </div>

      {/* Result message */}
      {result && (
        <div className={`flex items-start gap-2 rounded-xl px-4 py-3 mb-4 text-sm font-medium
          ${result.ok ? 'bg-green-50 border border-green-200 text-green-700' : 'bg-red-50 border border-red-200 text-red-700'}`}>
          {result.ok
            ? <CheckCircle2 className="w-5 h-5 shrink-0 mt-0.5" />
            : <AlertCircle  className="w-5 h-5 shrink-0 mt-0.5" />}
          {result.msg}
        </div>
      )}

      {/* Save Button */}
      <button
        onClick={handleSave}
        disabled={loading || newPin.length < 4 || newPin !== confirmPin}
        className="w-full py-3 bg-red-600 text-white font-bold rounded-xl hover:bg-red-700 transition
                   disabled:opacity-40 disabled:cursor-not-allowed flex items-center justify-center gap-2"
      >
        {loading ? (
          <><span className="w-5 h-5 border-2 border-white border-t-transparent rounded-full animate-spin" />
            Saving...</>
        ) : (
          <><ShieldCheck className="w-5 h-5" /> Save Override PIN</>
        )}
      </button>
    </div>
  );
};
