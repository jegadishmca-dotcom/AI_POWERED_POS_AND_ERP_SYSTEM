import React, { useState, useEffect } from 'react';
import { getTerminals, createTerminal, updateTerminal, deleteTerminal, TerminalDto } from '../api/settings.api';
import { Monitor, Plus, Edit, Trash2, CheckCircle2, Bookmark, Loader2 } from 'lucide-react';

export const TerminalConfig: React.FC = () => {
  const [terminals, setTerminals] = useState<TerminalDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Forms state
  const [showAddModal, setShowAddModal] = useState(false);
  const [showEditModal, setShowEditModal] = useState(false);
  const [selectedTerminal, setSelectedTerminal] = useState<TerminalDto | null>(null);

  const [code, setCode] = useState('');
  const [name, setName] = useState('');
  const [isActive, setIsActive] = useState(true);

  const [saving, setSaving] = useState(false);
  const [actionError, setActionError] = useState<string | null>(null);

  // Current local client registration details
  const [localTerminalCode, setLocalTerminalCode] = useState<string>(
    localStorage.getItem('pos_terminal_code') || 'NOT_REGISTERED'
  );

  const loadData = async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await getTerminals();
      setTerminals(data);
    } catch (err: any) {
      console.error('Failed to load terminals:', err);
      setError('Failed to load terminals list. Please verify your connection.');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadData();
  }, []);

  const handleAdd = async (e: React.FormEvent) => {
    e.preventDefault();
    setActionError(null);
    setSaving(true);
    try {
      await createTerminal({
        terminalCode: code.trim().toUpperCase(),
        name: name.trim(),
        isActive
      });
      setShowAddModal(false);
      setCode('');
      setName('');
      loadData();
    } catch (err: any) {
      setActionError(err.response?.data?.message || 'Failed to add terminal.');
    } finally {
      setSaving(false);
    }
  };

  const handleEdit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedTerminal) return;
    setActionError(null);
    setSaving(true);
    try {
      await updateTerminal(selectedTerminal.id, {
        terminalCode: code.trim().toUpperCase(),
        name: name.trim(),
        isActive
      });
      setShowEditModal(false);
      loadData();
    } catch (err: any) {
      setActionError(err.response?.data?.message || 'Failed to update terminal.');
    } finally {
      setSaving(false);
    }
  };

  const handleDelete = async (id: string) => {
    if (!window.confirm('Are you sure you want to delete this billing terminal?')) return;
    try {
      await deleteTerminal(id);
      loadData();
    } catch (err: any) {
      alert(err.response?.data?.message || 'Failed to delete terminal.');
    }
  };

  const registerLocalDevice = (term: TerminalDto) => {
    localStorage.setItem('pos_terminal_code', term.terminalCode);
    localStorage.setItem('pos_terminal_id', term.id);
    setLocalTerminalCode(term.terminalCode);
    alert(`Current browser has been successfully registered as Terminal: ${term.name} (${term.terminalCode})`);
  };

  const openEdit = (term: TerminalDto) => {
    setSelectedTerminal(term);
    setCode(term.terminalCode);
    setName(term.name);
    setIsActive(term.isActive);
    setActionError(null);
    setShowEditModal(true);
  };

  if (loading) {
    return (
      <div className="bg-white p-8 rounded-xl border border-slate-100 shadow-sm flex flex-col justify-center items-center h-80">
        <Monitor className="w-8 h-8 animate-spin text-indigo-600 mb-2" />
        <p className="text-gray-400 font-medium text-sm">Loading billing terminals...</p>
      </div>
    );
  }

  if (error) {
    return (
      <div className="bg-white p-8 rounded-xl border border-slate-100 shadow-sm text-center">
        <p className="text-red-500 font-bold mb-4">{error}</p>
        <button onClick={loadData} className="px-4 py-2 bg-indigo-600 text-white rounded-lg font-bold text-xs shadow">
          Retry
        </button>
      </div>
    );
  }

  return (
    <div className="bg-white p-6 rounded-xl border border-slate-100 shadow-sm">
      {/* Header */}
      <div className="flex justify-between items-center mb-6">
        <div className="flex items-center gap-3">
          <div className="p-2 bg-indigo-50 text-indigo-600 rounded-lg">
            <Monitor className="w-5 h-5" />
          </div>
          <div>
            <h3 className="font-bold text-slate-800">POS Billing Terminals</h3>
            <p className="text-xs text-slate-400">Configure billing counters and authorize devices</p>
          </div>
        </div>
        <button
          onClick={() => {
            setActionError(null);
            setCode('');
            setName('');
            setIsActive(true);
            setShowAddModal(true);
          }}
          className="px-4 py-2 bg-indigo-600 hover:bg-indigo-700 text-white font-bold text-xs rounded-xl shadow transition flex items-center gap-1.5"
        >
          <Plus className="w-4 h-4" /> Add Terminal
        </button>
      </div>

      {/* Local registration info alert */}
      <div className="mb-6 p-4 bg-slate-50 border rounded-xl flex items-center justify-between">
        <div>
          <p className="text-xs font-bold text-slate-400 uppercase">Current Browser Identity</p>
          <p className="text-sm font-black text-slate-700 mt-1">
            Registered Terminal Code: <span className="text-indigo-600">{localTerminalCode}</span>
          </p>
        </div>
        <div className="text-xs text-slate-500 max-w-sm text-right">
          Supermarket billing terminals must register their browser client locally to start shifts and record transactions.
        </div>
      </div>

      {/* Terminals table */}
      <div className="overflow-x-auto">
        <table className="w-full text-left text-sm">
          <thead>
            <tr className="border-b text-slate-400 uppercase text-xs font-bold">
              <th className="pb-3">Terminal Code</th>
              <th className="pb-3">Name / Location</th>
              <th className="pb-3 text-center">Status</th>
              <th className="pb-3 text-center">Registration</th>
              <th className="pb-3 text-center">Actions</th>
            </tr>
          </thead>
          <tbody className="divide-y text-slate-700">
            {terminals.map((t) => (
              <tr key={t.id} className="hover:bg-slate-50 transition-colors">
                <td className="py-4 font-black text-slate-800">{t.terminalCode}</td>
                <td className="py-4 font-semibold text-slate-500">{t.name}</td>
                <td className="py-4 text-center">
                  {t.isActive ? (
                    <span className="px-2.5 py-1 bg-emerald-50 text-emerald-700 rounded-full font-bold text-xs border border-emerald-200 inline-flex items-center gap-1">
                      Active
                    </span>
                  ) : (
                    <span className="px-2.5 py-1 bg-slate-50 text-slate-400 rounded-full font-bold text-xs border border-slate-200 inline-flex items-center gap-1">
                      Disabled
                    </span>
                  )}
                </td>
                <td className="py-4 text-center">
                  {localTerminalCode === t.terminalCode ? (
                    <span className="px-3 py-1 bg-indigo-50 border border-indigo-200 text-indigo-700 rounded-xl text-xs font-black inline-flex items-center gap-1">
                      <CheckCircle2 className="w-4 h-4 text-indigo-600" /> Current Device
                    </span>
                  ) : (
                    <button
                      onClick={() => registerLocalDevice(t)}
                      disabled={!t.isActive}
                      className={`px-3 py-1 border font-bold text-xs rounded-xl transition inline-flex items-center gap-1 ${
                        t.isActive
                          ? 'border-slate-300 hover:bg-slate-50 text-slate-700 hover:border-slate-400'
                          : 'border-slate-200 text-slate-300 cursor-not-allowed'
                      }`}
                    >
                      <Bookmark className="w-3.5 h-3.5" /> Register Browser
                    </button>
                  )}
                </td>
                <td className="py-4 text-center">
                  <div className="flex justify-center gap-2">
                    <button
                      onClick={() => openEdit(t)}
                      className="p-1.5 hover:bg-slate-100 text-slate-500 hover:text-slate-800 rounded transition"
                      title="Edit terminal"
                    >
                      <Edit className="w-4 h-4" />
                    </button>
                    <button
                      onClick={() => handleDelete(t.id)}
                      className="p-1.5 hover:bg-slate-100 text-slate-500 hover:text-rose-600 rounded transition"
                      title="Delete terminal"
                    >
                      <Trash2 className="w-4 h-4" />
                    </button>
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {/* ADD TERMINAL MODAL */}
      {showAddModal && (
        <div className="fixed inset-0 bg-slate-900/40 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div className="bg-white rounded-2xl w-full max-w-md shadow-xl border overflow-hidden p-6 space-y-4">
            <h3 className="text-lg font-bold text-slate-800">Add POS Terminal</h3>
            {actionError && (
              <div className="p-3 bg-red-50 border border-red-200 text-red-700 rounded-xl text-xs font-bold">
                {actionError}
              </div>
            )}
            <form onSubmit={handleAdd} className="space-y-4">
              <div>
                <label className="block text-xs font-bold text-slate-500 uppercase mb-1">Terminal Code</label>
                <input
                  type="text"
                  required
                  value={code}
                  onChange={(e) => setCode(e.target.value)}
                  className="w-full px-4 py-2 border rounded-xl text-sm"
                  placeholder="e.g. TERMINAL 04"
                />
              </div>
              <div>
                <label className="block text-xs font-bold text-slate-500 uppercase mb-1">Location / Counter Name</label>
                <input
                  type="text"
                  required
                  value={name}
                  onChange={(e) => setName(e.target.value)}
                  className="w-full px-4 py-2 border rounded-xl text-sm"
                  placeholder="e.g. Counter 04 Express"
                />
              </div>
              <div className="flex items-center gap-3">
                <input
                  type="checkbox"
                  id="isActive"
                  checked={isActive}
                  onChange={(e) => setIsActive(e.target.checked)}
                  className="h-4.5 w-4.5 rounded border-gray-300 text-indigo-600 focus:ring-indigo-500"
                />
                <label htmlFor="isActive" className="text-sm font-bold text-slate-700">Terminal Active</label>
              </div>
              <div className="flex gap-3 justify-end pt-2">
                <button
                  type="button"
                  onClick={() => setShowAddModal(false)}
                  className="px-4 py-2 bg-slate-100 hover:bg-slate-200 text-slate-700 font-bold text-xs rounded-xl transition"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={saving}
                  className="px-6 py-2 bg-indigo-600 hover:bg-indigo-700 text-white font-bold text-xs rounded-xl shadow transition"
                >
                  {saving ? 'Saving...' : 'Add Terminal'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* EDIT TERMINAL MODAL */}
      {showEditModal && selectedTerminal && (
        <div className="fixed inset-0 bg-slate-900/40 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div className="bg-white rounded-2xl w-full max-w-md shadow-xl border overflow-hidden p-6 space-y-4">
            <h3 className="text-lg font-bold text-slate-800">Edit Terminal: {selectedTerminal.terminalCode}</h3>
            {actionError && (
              <div className="p-3 bg-red-50 border border-red-200 text-red-700 rounded-xl text-xs font-bold">
                {actionError}
              </div>
            )}
            <form onSubmit={handleEdit} className="space-y-4">
              <div>
                <label className="block text-xs font-bold text-slate-500 uppercase mb-1">Terminal Code</label>
                <input
                  type="text"
                  required
                  value={code}
                  onChange={(e) => setCode(e.target.value)}
                  className="w-full px-4 py-2 border rounded-xl text-sm"
                />
              </div>
              <div>
                <label className="block text-xs font-bold text-slate-500 uppercase mb-1">Location / Counter Name</label>
                <input
                  type="text"
                  required
                  value={name}
                  onChange={(e) => setName(e.target.value)}
                  className="w-full px-4 py-2 border rounded-xl text-sm"
                />
              </div>
              <div className="flex items-center gap-3">
                <input
                  type="checkbox"
                  id="editIsActive"
                  checked={isActive}
                  onChange={(e) => setIsActive(e.target.checked)}
                  className="h-4.5 w-4.5 rounded border-gray-300 text-indigo-600 focus:ring-indigo-500"
                />
                <label htmlFor="editIsActive" className="text-sm font-bold text-slate-700">Terminal Active</label>
              </div>
              <div className="flex gap-3 justify-end pt-2">
                <button
                  type="button"
                  onClick={() => setShowEditModal(false)}
                  className="px-4 py-2 bg-slate-100 hover:bg-slate-200 text-slate-700 font-bold text-xs rounded-xl transition"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={saving}
                  className="px-6 py-2 bg-indigo-600 hover:bg-indigo-700 text-white font-bold text-xs rounded-xl shadow transition"
                >
                  {saving ? 'Saving...' : 'Update Terminal'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
};
