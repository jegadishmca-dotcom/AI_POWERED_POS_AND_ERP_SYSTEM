import React, { useState, useEffect } from 'react';
import { ShieldCheck, Database, Ban, Save, RefreshCw, AlertTriangle } from 'lucide-react';
import { api } from '../../../utils/api';

export const InventorySafeguards = () => {
  const [preventNegativeStock, setPreventNegativeStock] = useState(true);
  const [mandatoryBatchTracking, setMandatoryBatchTracking] = useState(true);
  const [rowLevelLocking, setRowLevelLocking] = useState(true);
  
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [successMsg, setSuccessMsg] = useState<string | null>(null);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);

  const fetchRules = async () => {
    try {
      setLoading(true);
      setErrorMsg(null);
      const res = await api.get('/api/settings/inventory-rules');
      setPreventNegativeStock(res.data.preventNegativeStock);
      setMandatoryBatchTracking(res.data.mandatoryBatchTracking);
      setRowLevelLocking(res.data.rowLevelLocking);
    } catch (err: any) {
      console.error('Failed to load inventory safeguards configuration', err);
      setErrorMsg('Failed to load safeguards config from server.');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchRules();
  }, []);

  const handleSave = async () => {
    try {
      setSaving(true);
      setSuccessMsg(null);
      setErrorMsg(null);

      const payload = {
        preventNegativeStock,
        mandatoryBatchTracking,
        rowLevelLocking
      };

      await api.post('/api/settings/inventory-rules', payload);
      setSuccessMsg('Inventory rules and controls updated successfully.');
      setTimeout(() => setSuccessMsg(null), 5000);
    } catch (err: any) {
      console.error('Failed to update inventory safeguards', err);
      setErrorMsg('Failed to save safeguards. Please retry.');
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="bg-white p-8 rounded-2xl border border-slate-100 shadow-lg max-w-2xl">
      <div className="flex items-center gap-3 mb-6 pb-4 border-b border-slate-100">
        <ShieldCheck className="w-7 h-7 text-indigo-600" />
        <div>
          <h3 className="text-xl font-black text-slate-800">Inventory Safeguards & Rules</h3>
          <p className="text-xs text-slate-500 font-medium">Define automated system-driven controls to achieve 99.9% stock accuracy</p>
        </div>
      </div>

      {loading ? (
        <div className="py-12 flex flex-col items-center justify-center space-y-2">
          <RefreshCw className="w-8 h-8 text-indigo-600 animate-spin" />
          <span className="text-xs text-slate-500 font-semibold">Retrieving config...</span>
        </div>
      ) : (
        <div className="space-y-6">
          {errorMsg && (
            <div className="p-3 bg-red-50 text-red-700 rounded-xl text-xs font-bold border border-red-100 flex items-start space-x-2">
              <AlertTriangle className="w-4 h-4 text-red-500 shrink-0 mt-0.5" />
              <span>{errorMsg}</span>
            </div>
          )}

          {successMsg && (
            <div className="p-3 bg-green-50 text-green-700 rounded-xl text-xs font-bold border border-green-100 flex items-start space-x-2">
              <ShieldCheck className="w-4 h-4 text-green-500 shrink-0 mt-0.5" />
              <span>{successMsg}</span>
            </div>
          )}

          {/* Toggle 1: Negative Stock */}
          <div className="flex items-start justify-between p-4 rounded-xl hover:bg-slate-50 transition border border-slate-50">
            <div className="space-y-1 pr-6">
              <div className="text-sm font-bold text-slate-800 flex items-center gap-2">
                <Ban className="w-4 h-4 text-slate-500" /> Prevent Negative Stock
              </div>
              <p className="text-xs text-slate-500 font-medium">
                Blocks POS cashiers from selling items with 0 stock. Requires a valid Manager/Supervisor PIN override to force checkout if enabled.
              </p>
            </div>
            <button
              onClick={() => setPreventNegativeStock(!preventNegativeStock)}
              className={`w-12 h-6 rounded-full shrink-0 relative transition-colors ${preventNegativeStock ? 'bg-indigo-600' : 'bg-slate-350'}`}
            >
              <div className={`w-4 h-4 bg-white rounded-full absolute top-1 transition-all ${preventNegativeStock ? 'right-1' : 'left-1'}`} />
            </button>
          </div>

          {/* Toggle 2: Mandatory Batch/Expiry */}
          <div className="flex items-start justify-between p-4 rounded-xl hover:bg-slate-50 transition border border-slate-50">
            <div className="space-y-1 pr-6">
              <div className="text-sm font-bold text-slate-800 flex items-center gap-2">
                <ShieldCheck className="w-4 h-4 text-slate-500" /> Mandatory Batch/Expiry for Perishables
              </div>
              <p className="text-xs text-slate-500 font-medium">
                Rejects AI Invoice Draft imports if perishable items (`Has Expiry` is true) have blank batch numbers or null expiry dates.
              </p>
            </div>
            <button
              onClick={() => setMandatoryBatchTracking(!mandatoryBatchTracking)}
              className={`w-12 h-6 rounded-full shrink-0 relative transition-colors ${mandatoryBatchTracking ? 'bg-indigo-600' : 'bg-slate-350'}`}
            >
              <div className={`w-4 h-4 bg-white rounded-full absolute top-1 transition-all ${mandatoryBatchTracking ? 'right-1' : 'left-1'}`} />
            </button>
          </div>

          {/* Toggle 3: DB Concurrency Lock */}
          <div className="flex items-start justify-between p-4 rounded-xl hover:bg-slate-50 transition border border-slate-50">
            <div className="space-y-1 pr-6">
              <div className="text-sm font-bold text-slate-800 flex items-center gap-2">
                <Database className="w-4 h-4 text-slate-500" /> Row-Level Database Concurrency Lock
              </div>
              <p className="text-xs text-slate-500 font-medium">
                Acquires a row-level PostgreSQL lock (`SELECT FOR UPDATE`) on the product during stock operations. Eliminates race condition errors across checkouts.
              </p>
            </div>
            <button
              onClick={() => setRowLevelLocking(!rowLevelLocking)}
              className={`w-12 h-6 rounded-full shrink-0 relative transition-colors ${rowLevelLocking ? 'bg-indigo-600' : 'bg-slate-350'}`}
            >
              <div className={`w-4 h-4 bg-white rounded-full absolute top-1 transition-all ${rowLevelLocking ? 'right-1' : 'left-1'}`} />
            </button>
          </div>

          {/* Action button */}
          <div className="flex justify-end pt-4 border-t border-slate-100">
            <button
              onClick={handleSave}
              disabled={saving}
              className="px-5 py-2.5 bg-indigo-600 hover:bg-indigo-700 disabled:opacity-50 text-white font-bold rounded-xl text-sm flex items-center shadow-md transition"
            >
              {saving ? (
                <>
                  <RefreshCw className="w-4 h-4 mr-1.5 animate-spin" /> Saving Rules...
                </>
              ) : (
                <>
                  <Save className="w-4 h-4 mr-1.5" /> Save Safeguards
                </>
              )}
            </button>
          </div>
        </div>
      )}
    </div>
  );
};
