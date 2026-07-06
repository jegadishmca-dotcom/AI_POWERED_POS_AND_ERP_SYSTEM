import React, { useState, useEffect } from 'react';
import { FileCheck, AlertTriangle, CheckCircle2, Info, Loader2, ToggleLeft, ToggleRight } from 'lucide-react';
import { getComplianceFeatures, updateComplianceFeatures, ComplianceFeaturesDto } from '../api/settings.api';

export const ComplianceFeatures: React.FC = () => {
  const [features, setFeatures] = useState<ComplianceFeaturesDto>({
    eInvoiceEnabled: false,
    eWayBillEnabled: false,
  });
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [message, setMessage] = useState<{ text: string; type: 'success' | 'error' | 'info' } | null>(null);

  useEffect(() => {
    fetchFeatures();
  }, []);

  const fetchFeatures = async () => {
    setLoading(true);
    try {
      const data = await getComplianceFeatures();
      setFeatures(data);
    } catch {
      // If endpoint not yet wired, default to off (matches database default)
      setFeatures({ eInvoiceEnabled: false, eWayBillEnabled: false });
      setMessage({ text: 'Could not load settings from server — showing defaults (both OFF).', type: 'info' });
    } finally {
      setLoading(false);
    }
  };

  const handleSave = async () => {
    setSaving(true);
    setMessage(null);
    try {
      await updateComplianceFeatures(features);
      setMessage({ text: 'Compliance settings saved. Changes take effect immediately — no restart required.', type: 'success' });
    } catch (err: any) {
      setMessage({ text: err?.response?.data?.message || 'Failed to save settings.', type: 'error' });
    } finally {
      setSaving(false);
    }
  };

  const Toggle: React.FC<{
    id: string;
    label: string;
    description: string;
    warning: string;
    value: boolean;
    onChange: (v: boolean) => void;
  }> = ({ id, label, description, warning, value, onChange }) => (
    <div className="flex items-start justify-between gap-6 p-5 rounded-xl border border-slate-200 bg-slate-50 hover:bg-white transition-colors">
      <div className="flex-1">
        <div className="flex items-center gap-2 mb-1">
          <span className="font-bold text-slate-800 text-sm">{label}</span>
          <span className={`text-xs font-bold px-2 py-0.5 rounded-full ${value ? 'bg-green-100 text-green-700' : 'bg-slate-200 text-slate-500'}`}>
            {value ? 'ENABLED' : 'DISABLED'}
          </span>
        </div>
        <p className="text-xs text-slate-500 mb-2">{description}</p>
        {value && (
          <div className="flex items-start gap-1.5 text-xs text-amber-700 bg-amber-50 border border-amber-200 rounded-lg px-3 py-2">
            <AlertTriangle className="w-3.5 h-3.5 mt-0.5 flex-shrink-0" />
            <span>{warning}</span>
          </div>
        )}
      </div>
      <button
        id={id}
        onClick={() => onChange(!value)}
        className={`flex-shrink-0 transition-colors ${value ? 'text-green-600 hover:text-green-700' : 'text-slate-400 hover:text-slate-600'}`}
        title={value ? 'Click to disable' : 'Click to enable'}
      >
        {value ? <ToggleRight className="w-10 h-10" /> : <ToggleLeft className="w-10 h-10" />}
      </button>
    </div>
  );

  if (loading) {
    return (
      <div className="bg-white rounded-2xl border border-slate-100 shadow-sm p-8 flex items-center gap-3 text-slate-500">
        <Loader2 className="w-5 h-5 animate-spin" />
        <span className="text-sm">Loading compliance settings…</span>
      </div>
    );
  }

  return (
    <div className="bg-white rounded-2xl border border-slate-100 shadow-sm overflow-hidden">
      {/* Header */}
      <div className="px-6 py-5 border-b border-slate-100 flex items-center gap-3">
        <div className="bg-violet-100 p-2 rounded-xl text-violet-600">
          <FileCheck className="w-5 h-5" />
        </div>
        <div>
          <h3 className="font-black text-slate-800">GST Compliance Features</h3>
          <p className="text-xs text-slate-500 mt-0.5">
            Control e-Invoice (IRN) and e-Way Bill generation. Keep both OFF until your GST registration and GSP credentials are configured.
          </p>
        </div>
      </div>

      {/* Notice */}
      <div className="mx-6 mt-5 flex items-start gap-2 bg-blue-50 border border-blue-200 rounded-xl px-4 py-3 text-xs text-blue-800">
        <Info className="w-4 h-4 mt-0.5 flex-shrink-0 text-blue-500" />
        <span>
          These toggles control whether the backend <strong>calls the IRP / GSP API</strong> when generating invoices.
          Settings are stored in the database and take effect immediately — no container restart is required.
          Enabling without a valid GSP credential configuration will cause invoice creation errors.
        </span>
      </div>

      {/* Toggles */}
      <div className="p-6 flex flex-col gap-4">
        <Toggle
          id="toggle-einvoice-enabled"
          label="E-Invoice (IRN Generation)"
          description="When enabled, the system submits invoice data to the IRP (Invoice Registration Portal) via a GSP and stores the IRN, Ack No., and QR code on each invoice."
          warning="Ensure GSP credentials are configured in appsettings.json before enabling. All invoices will trigger a live IRP API call."
          value={features.eInvoiceEnabled}
          onChange={(v) => setFeatures(f => ({ ...f, eInvoiceEnabled: v }))}
        />
        <Toggle
          id="toggle-ewaybill-enabled"
          label="E-Way Bill Generation"
          description="When enabled, the system generates e-Way Bills for eligible invoices (typically goods above ₹50,000 in transit) via the NIC e-Way Bill portal."
          warning="Ensure transporter and vehicle details are captured at invoice level before enabling. Incorrect e-Way Bills require portal cancellation within 24 hours."
          value={features.eWayBillEnabled}
          onChange={(v) => setFeatures(f => ({ ...f, eWayBillEnabled: v }))}
        />
      </div>

      {/* Status message */}
      {message && (
        <div className={`mx-6 mb-4 flex items-center gap-2 text-xs px-4 py-3 rounded-xl border ${
          message.type === 'success' ? 'bg-green-50 border-green-200 text-green-800' :
          message.type === 'error'   ? 'bg-red-50 border-red-200 text-red-800' :
                                       'bg-blue-50 border-blue-200 text-blue-800'
        }`}>
          {message.type === 'success' ? <CheckCircle2 className="w-4 h-4 flex-shrink-0" /> :
           message.type === 'error'   ? <AlertTriangle className="w-4 h-4 flex-shrink-0" /> :
                                        <Info className="w-4 h-4 flex-shrink-0" />}
          {message.text}
        </div>
      )}

      {/* Save button */}
      <div className="px-6 pb-6">
        <button
          id="btn-save-compliance-features"
          onClick={handleSave}
          disabled={saving}
          className="flex items-center gap-2 bg-violet-600 hover:bg-violet-700 disabled:opacity-60 text-white font-bold text-sm px-5 py-2.5 rounded-xl transition-colors"
        >
          {saving ? <Loader2 className="w-4 h-4 animate-spin" /> : <CheckCircle2 className="w-4 h-4" />}
          {saving ? 'Saving…' : 'Save Compliance Settings'}
        </button>
      </div>
    </div>
  );
};
