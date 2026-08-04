import React, { useState, useEffect } from 'react';
import {
  ShieldCheck, AlertTriangle, CheckCircle2, Loader2,
  ToggleLeft, ToggleRight, Trash2, Info, Globe
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

      {/* Section Divider */}
      <div className="my-8 border-t border-slate-200" />

      {/* Multi-Language & Receipt Print Settings */}
      <div className="flex items-center gap-3 mb-6">
        <div className="p-2 bg-emerald-50 text-emerald-600 rounded-lg">
          <Globe className="w-5 h-5" />
        </div>
        <div>
          <h3 className="font-bold text-slate-800">Multi-Language & Receipt Print Configuration</h3>
          <p className="text-xs text-slate-400">
            Global ERP standard for multi-lingual store receipt printing & automatic product catalog translation
          </p>
        </div>
      </div>

      <div className="space-y-6">
        {/* Receipt Product Language */}
        <div className="p-5 rounded-xl border border-slate-200 bg-slate-50">
          <label className="block font-bold text-slate-800 text-sm mb-1">
            Receipt Product Language Mode
          </label>
          <p className="text-xs text-slate-500 mb-3">
            Choose which product name language is printed on thermal POS billing receipts
          </p>
          <div className="grid grid-cols-1 md:grid-cols-3 gap-3">
            {[
              { id: 'secondary', label: 'Secondary / Tamil (Default)', desc: 'Prints regional Tamil product name' },
              { id: 'primary', label: 'Primary / English', desc: 'Prints standard English product name' },
              { id: 'both', label: 'Dual (English + Tamil)', desc: 'Prints English / Tamil dual names' },
            ].map(opt => (
              <button
                key={opt.id}
                type="button"
                disabled={saving}
                onClick={async () => {
                  setSaving(true);
                  const updated = { ...permissions, receiptProductLanguage: opt.id as any };
                  setPermissions(updated);
                  try {
                    await updatePosPermissions(updated);
                    setMessage({ text: `Receipt language updated to ${opt.label}.`, type: 'success' });
                  } catch {
                    setPermissions(permissions);
                  } finally { setSaving(false); }
                }}
                className={`p-3 rounded-lg border text-left transition-all ${
                  (permissions.receiptProductLanguage || 'secondary') === opt.id
                    ? 'bg-emerald-50 border-emerald-500 ring-2 ring-emerald-500/20 text-emerald-900 font-bold'
                    : 'bg-white border-slate-200 text-slate-700 hover:border-slate-300'
                }`}
              >
                <p className="text-xs font-bold mb-0.5">{opt.label}</p>
                <p className="text-[11px] text-slate-500 font-normal">{opt.desc}</p>
              </button>
            ))}
          </div>
        </div>

        {/* Product Catalog Auto-Translation Settings */}
        <div className="p-5 rounded-xl border border-slate-200 bg-slate-50 space-y-4">
          <div className="flex items-center justify-between">
            <div>
              <p className="font-bold text-slate-800 text-sm">Product Catalog Auto-Translation Engine</p>
              <p className="text-xs text-slate-500">
                Zoho AI-Level Smart ERP auto-translation engine for Product Catalog creation
              </p>
            </div>
            <button
              type="button"
              disabled={saving}
              onClick={async () => {
                setSaving(true);
                const val = !permissions.enableCatalogAutoTranslation;
                const updated = { ...permissions, enableCatalogAutoTranslation: val };
                setPermissions(updated);
                try {
                  await updatePosPermissions(updated);
                  setMessage({ text: `Catalog auto-translation ${val ? 'enabled' : 'disabled'}.`, type: 'success' });
                } catch { setPermissions(permissions); } finally { setSaving(false); }
              }}
              className="focus:outline-none"
            >
              {permissions.enableCatalogAutoTranslation !== false ? (
                <ToggleRight className="w-10 h-10 text-emerald-500" />
              ) : (
                <ToggleLeft className="w-10 h-10 text-slate-300" />
              )}
            </button>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-2 gap-4 pt-2 border-t border-slate-200">
            <div>
              <label className="block text-xs font-bold text-slate-700 mb-1">
                Target Translation Language
              </label>
              <select
                disabled={saving || permissions.enableCatalogAutoTranslation === false}
                className="w-full text-xs font-bold p-2.5 bg-white border border-slate-300 rounded-lg outline-none focus:ring-2 focus:ring-emerald-500"
                value={permissions.catalogTargetLanguage || 'ta'}
                onChange={async (e) => {
                  const val = e.target.value as any;
                  setSaving(true);
                  const updated = { ...permissions, catalogTargetLanguage: val };
                  setPermissions(updated);
                  try {
                    await updatePosPermissions(updated);
                    setMessage({ text: `Target catalog language updated.`, type: 'success' });
                  } catch { setPermissions(permissions); } finally { setSaving(false); }
                }}
              >
                <option value="ta">Tamil (தமிழ்) — Default (Tamil Nadu)</option>
                <option value="hi">Hindi (हिन्दी) — North India</option>
                <option value="ar">Arabic (العربية) — Middle East / GCC</option>
                <option value="ms">Malay (Bahasa Melayu) — SE Asia</option>
                <option value="es">Spanish (Español) — Global</option>
              </select>
            </div>
            <div className="text-xs text-slate-500 flex items-center bg-blue-50/60 p-3 rounded-lg border border-blue-100">
              <Info className="w-4 h-4 text-blue-600 mr-2 shrink-0" />
              As you type English product names (e.g. Salt 1Kg), the engine tokenizes unit shorthand and populates meaningful secondary names (e.g. உப்பு 1 கிலோ).
            </div>
          </div>
        </div>
      </div>

      {saving && (
        <div className="mt-4 flex items-center gap-2 text-xs text-indigo-600 font-semibold">
          <Loader2 className="w-4 h-4 animate-spin" />
          Saving settings...
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
