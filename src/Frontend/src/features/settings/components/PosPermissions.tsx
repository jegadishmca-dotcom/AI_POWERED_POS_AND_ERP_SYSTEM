import React, { useState, useEffect } from 'react';
import {
  ShieldCheck, AlertTriangle, CheckCircle2, Loader2,
  ToggleLeft, ToggleRight, Trash2, Info
} from 'lucide-react';
import { getPosPermissions, updatePosPermissions, PosPermissionsDto } from '../api/settings.api';

export const PosPermissions: React.FC = () => {
  const [permissions, setPermissions] = useState<PosPermissionsDto>({
    cashierCanDeleteLineItem: false,
  });
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [message, setMessage] = useState<{ text: string; type: 'success' | 'error' | 'info' } | null>(null);

  useEffect(() => {
    fetchPermissions();
  }, []);

  const fetchPermissions = async () => {
    setLoading(true);
    try {
      const data = await getPosPermissions();
      setPermissions(data);
    } catch {
      setPermissions({ cashierCanDeleteLineItem: false });
      setMessage({
        text: 'Could not load settings from server — showing defaults (all OFF).',
        type: 'info',
      });
    } finally {
      setLoading(false);
    }
  };

  const handleToggle = async (key: keyof PosPermissionsDto, newValue: boolean) => {
    setSaving(true);
    setMessage(null);
    const updated = { ...permissions, [key]: newValue };
    setPermissions(updated); // optimistic update
    try {
      await updatePosPermissions(updated);
      setMessage({
        text: 'POS permission settings saved. Changes take effect immediately.',
        type: 'success',
      });
    } catch (err: any) {
      // Roll back on failure
      setPermissions(permissions);
      setMessage({
        text: err?.response?.data?.message || 'Failed to save settings.',
        type: 'error',
      });
    } finally {
      setSaving(false);
    }
  };

  if (loading) {
    return (
      <div className="bg-white p-8 rounded-xl border border-slate-100 shadow-sm flex flex-col justify-center items-center h-48">
        <Loader2 className="w-7 h-7 animate-spin text-indigo-600 mb-2" />
        <p className="text-slate-400 text-sm font-medium">Loading POS permissions...</p>
      </div>
    );
  }

  return (
    <div className="bg-white p-6 rounded-xl border border-slate-100 shadow-sm">
      {/* Header */}
      <div className="flex items-center gap-3 mb-6">
        <div className="p-2 bg-indigo-50 text-indigo-600 rounded-lg">
          <ShieldCheck className="w-5 h-5" />
        </div>
        <div>
          <h3 className="font-bold text-slate-800">POS Cashier Permissions</h3>
          <p className="text-xs text-slate-400">
            Control what actions cashiers can perform without a Manager Override PIN
          </p>
        </div>
      </div>

      {/* Status message */}
      {message && (
        <div
          className={`mb-5 flex items-start gap-3 p-4 rounded-xl border text-sm font-medium ${
            message.type === 'success'
              ? 'bg-emerald-50 border-emerald-200 text-emerald-800'
              : message.type === 'error'
              ? 'bg-red-50 border-red-200 text-red-700'
              : 'bg-blue-50 border-blue-200 text-blue-700'
          }`}
        >
          {message.type === 'success' ? (
            <CheckCircle2 className="w-5 h-5 mt-0.5 shrink-0" />
          ) : (
            <Info className="w-5 h-5 mt-0.5 shrink-0" />
          )}
          <span>{message.text}</span>
        </div>
      )}

      {/* Toggle: Cashier can delete line items */}
      <PermissionToggle
        id="cashierCanDeleteLineItem"
        icon={<Trash2 className="w-5 h-5" />}
        iconBg="bg-rose-100 text-rose-600"
        label="Allow Cashier to Delete Cart Line Items"
        description="When enabled, cashiers can remove items from the billing cart without requiring a Manager Override PIN. The action is still audit-logged with cashier ID, item details, and timestamp for every deletion."
        warning="This reduces Manager Override control on cart deletions. Only enable when cashier accountability is high (e.g. CCTV-monitored counters)."
        value={permissions.cashierCanDeleteLineItem}
        disabled={saving}
        onChange={(v) => handleToggle('cashierCanDeleteLineItem', v)}
      />

      {saving && (
        <div className="mt-4 flex items-center gap-2 text-xs text-indigo-600 font-semibold">
          <Loader2 className="w-4 h-4 animate-spin" />
          Saving...
        </div>
      )}
    </div>
  );
};

/* ── Sub-component: reusable permission toggle row ── */

interface PermissionToggleProps {
  id: string;
  icon: React.ReactNode;
  iconBg: string;
  label: string;
  description: string;
  warning: string;
  value: boolean;
  disabled?: boolean;
  onChange: (v: boolean) => void;
}

const PermissionToggle: React.FC<PermissionToggleProps> = ({
  id, icon, iconBg, label, description, warning, value, disabled, onChange
}) => (
  <div className="flex items-start justify-between gap-6 p-5 rounded-xl border border-slate-200 bg-slate-50 hover:bg-white transition-colors">
    <div className="flex items-start gap-4 flex-1">
      <div className={`p-2.5 rounded-lg ${iconBg} shrink-0 mt-0.5`}>
        {icon}
      </div>
      <div className="flex-1">
        <div className="flex items-center gap-2 mb-1">
          <span className="font-bold text-slate-800 text-sm">{label}</span>
          <span
            className={`text-xs font-bold px-2 py-0.5 rounded-full ${
              value ? 'bg-emerald-100 text-emerald-700' : 'bg-slate-200 text-slate-500'
            }`}
          >
            {value ? 'ENABLED' : 'DISABLED'}
          </span>
        </div>
        <p className="text-xs text-slate-500 leading-relaxed mb-2">{description}</p>
        <div className="flex items-start gap-2 mt-2 bg-amber-50 border border-amber-200 rounded-lg p-2.5">
          <AlertTriangle className="w-3.5 h-3.5 text-amber-600 mt-0.5 shrink-0" />
          <p className="text-xs text-amber-700 font-medium">{warning}</p>
        </div>
      </div>
    </div>

    {/* Toggle switch */}
    <button
      id={id}
      type="button"
      disabled={disabled}
      onClick={() => onChange(!value)}
      title={value ? 'Click to disable' : 'Click to enable'}
      className={`shrink-0 mt-1 focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:ring-offset-2 rounded-full transition-colors disabled:opacity-50 disabled:cursor-not-allowed`}
    >
      {value ? (
        <ToggleRight className="w-10 h-10 text-emerald-500 hover:text-emerald-600 transition-colors" />
      ) : (
        <ToggleLeft className="w-10 h-10 text-slate-300 hover:text-slate-400 transition-colors" />
      )}
    </button>
  </div>
);
