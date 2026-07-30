import React, { useState, useEffect } from 'react';
import { PlusCircle, Search, Edit2, Trash2, FileText, X, Download } from 'lucide-react';
import { api } from '../../../utils/api';

export interface Supplier {
  id: string;
  name: string;
  gstin: string;
  phone: string;
  paymentTerms: string;
  isActive: boolean;
}

interface SupplierListProps {
  onEdit: (supplier: Supplier) => void;
  onAddNew: () => void;
}

export const SupplierList: React.FC<SupplierListProps> = ({ onEdit, onAddNew }) => {
  const [suppliers, setSuppliers] = useState<Supplier[]>([]);
  const [searchTerm, setSearchTerm] = useState('');
  const [loading, setLoading] = useState(true);

  // Supplier Ledger Modal State
  const [selectedLedgerSupplier, setSelectedLedgerSupplier] = useState<Supplier | null>(null);
  const [ledgerEntries, setLedgerEntries] = useState<any[]>([]);
  const [ledgerLoading, setLedgerLoading] = useState(false);

  useEffect(() => {
    fetchSuppliers();
  }, []);

  const fetchSuppliers = async () => {
    try {
      const response = await api.get('/api/suppliers');
      setSuppliers(response.data);
    } catch (error) {
      console.error('Failed to fetch suppliers:', error);
    } finally {
      setLoading(false);
    }
  };

  const handleOpenLedger = async (supplier: Supplier) => {
    setSelectedLedgerSupplier(supplier);
    setLedgerLoading(true);
    try {
      const res = await api.get(`/api/accounts-payable/ledger?supplierId=${supplier.id}`);
      setLedgerEntries(res.data || []);
    } catch (err) {
      console.error('Failed to fetch supplier ledger:', err);
      setLedgerEntries([]);
    } finally {
      setLedgerLoading(false);
    }
  };

  const handleExportLedgerCSV = () => {
    if (!selectedLedgerSupplier || ledgerEntries.length === 0) return;
    const filename = `Supplier_Ledger_${selectedLedgerSupplier.name.replace(/\s+/g, '_')}.csv`;
    const headers = ['Date', 'Reference #', 'Type', 'Debit (₹)', 'Credit (₹)', 'Running Balance (₹)', 'Description'];
    const rows = ledgerEntries.map(e => [
      e.entryDate ? new Date(e.entryDate).toLocaleDateString('en-IN') : '',
      e.referenceNumber || '',
      e.transactionType || '',
      (e.debitAmount || 0).toFixed(2),
      (e.creditAmount || 0).toFixed(2),
      (e.runningBalance || 0).toFixed(2),
      (e.description || '').replace(/"/g, '""')
    ]);

    const csvContent = [headers.join(','), ...rows.map(r => r.map(c => `"${c}"`).join(','))].join('\n');
    const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
    const link = document.createElement('a');
    link.href = URL.createObjectURL(blob);
    link.setAttribute('download', filename);
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
  };

  const handleDelete = async (id: string, name: string) => {
    if (!window.confirm(`Are you sure you want to delete supplier "${name}"?`)) return;
    try {
      await api.delete(`/api/suppliers/${id}`);
      fetchSuppliers();
    } catch (error: any) {
      console.error('Failed to delete supplier:', error);
      alert(error.response?.data?.message || 'Failed to delete supplier.');
    }
  };

  const filteredSuppliers = suppliers.filter(s => 
    (s.name || '').toLowerCase().includes(searchTerm.toLowerCase()) || 
    (s.gstin || '').toLowerCase().includes(searchTerm.toLowerCase())
  );

  return (
    <div className="p-6 max-w-6xl mx-auto">
      <div className="flex justify-between items-center mb-6">
        <div>
          <h1 className="text-2xl font-bold text-gray-800">Supplier Master & Accounts Payable</h1>
          <p className="text-gray-500 text-sm">Manage vendor profiles, payment terms, and ledger statements</p>
        </div>
        <button
          onClick={onAddNew}
          className="flex items-center gap-2 bg-blue-600 text-white px-4 py-2 rounded-lg font-bold hover:bg-blue-700 transition-colors shadow-sm"
        >
          <PlusCircle size={20} />
          New Supplier
        </button>
      </div>

      <div className="bg-white rounded-xl shadow-sm border border-gray-200 overflow-hidden">
        <div className="p-4 border-b border-gray-100 bg-gray-50/50">
          <div className="relative max-w-md">
            <Search className="absolute left-3 top-1/2 -translate-y-1/2 text-gray-400" size={20} />
            <input
              type="text"
              placeholder="Search by supplier name or GSTIN..."
              value={searchTerm}
              onChange={(e) => setSearchTerm(e.target.value)}
              className="w-full pl-10 pr-4 py-2 rounded-lg border border-gray-300 font-semibold text-slate-800 focus:outline-none focus:ring-2 focus:ring-blue-500/20 focus:border-blue-500"
            />
          </div>
        </div>

        {loading ? (
          <div className="p-8 text-center text-gray-500 font-semibold">Loading suppliers...</div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-left border-collapse">
              <thead className="bg-slate-100 border-b-2 border-slate-200 text-sm">
                <tr>
                  <th className="px-6 py-4 font-bold text-slate-700 border-r border-slate-200">Supplier Name</th>
                  <th className="px-6 py-4 font-bold text-slate-700 border-r border-slate-200">GSTIN</th>
                  <th className="px-6 py-4 font-bold text-slate-700 border-r border-slate-200">Phone</th>
                  <th className="px-6 py-4 font-bold text-slate-700 border-r border-slate-200">Terms</th>
                  <th className="px-6 py-4 font-bold text-slate-700 border-r border-slate-200 text-center">Status</th>
                  <th className="px-6 py-4 font-bold text-slate-700 text-center">Actions</th>
                </tr>
              </thead>
              <tbody className="divide-y text-sm">
                {filteredSuppliers.map((supplier) => (
                  <tr key={supplier.id} className="hover:bg-blue-50/50 transition-colors">
                    <td className="px-6 py-4 font-bold text-slate-900">{supplier.name}</td>
                    <td className="px-6 py-4 text-slate-600 font-semibold">{supplier.gstin || '-'}</td>
                    <td className="px-6 py-4 text-slate-600 font-semibold">{supplier.phone}</td>
                    <td className="px-6 py-4 text-slate-600 font-bold">{supplier.paymentTerms}</td>
                    <td className="px-6 py-4 text-center">
                      <span className={`px-2.5 py-1 text-xs font-black rounded-full uppercase ${
                        supplier.isActive 
                          ? 'bg-emerald-100 text-emerald-800 border border-emerald-300' 
                          : 'bg-rose-100 text-rose-800 border border-rose-300'
                      }`}>
                        {supplier.isActive ? 'Active' : 'Inactive'}
                      </span>
                    </td>
                    <td className="px-6 py-4 text-center">
                      <div className="flex justify-center gap-2">
                        <button 
                          onClick={() => handleOpenLedger(supplier)}
                          className="px-3 py-1 bg-indigo-50 hover:bg-indigo-100 text-indigo-700 border border-indigo-200 rounded-md font-bold text-xs flex items-center gap-1 transition-colors"
                          title="View Accounts Payable Ledger Statement"
                        >
                          <FileText size={14} /> Ledger
                        </button>
                        <button 
                          onClick={() => onEdit(supplier)}
                          className="p-1.5 text-slate-600 hover:text-blue-600 hover:bg-blue-50 rounded-lg transition-colors"
                          title="Edit Supplier"
                        >
                          <Edit2 size={16} />
                        </button>
                        <button 
                          onClick={() => handleDelete(supplier.id, supplier.name)}
                          className="p-1.5 text-slate-600 hover:text-rose-600 hover:bg-rose-50 rounded-lg transition-colors"
                          title="Delete Supplier"
                        >
                          <Trash2 size={16} />
                        </button>
                      </div>
                    </td>
                  </tr>
                ))}
                {filteredSuppliers.length === 0 && (
                  <tr>
                    <td colSpan={6} className="px-6 py-8 text-center text-gray-400 font-semibold">
                      No suppliers found.
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {/* SUPPLIER AP LEDGER STATEMENT MODAL */}
      {selectedLedgerSupplier && (
        <div className="fixed inset-0 bg-slate-900/60 backdrop-blur-xs flex justify-center items-center z-50 p-4">
          <div className="bg-white rounded-2xl shadow-xl w-full max-w-4xl max-h-[90vh] flex flex-col overflow-hidden border border-slate-200">
            {/* Modal Header */}
            <div className="p-6 bg-slate-900 text-white flex justify-between items-center">
              <div>
                <h3 className="text-xl font-black">{selectedLedgerSupplier.name}</h3>
                <p className="text-xs text-slate-300 font-semibold mt-0.5">
                  Accounts Payable (AP) Ledger Statement • GSTIN: {selectedLedgerSupplier.gstin || 'N/A'} • Phone: {selectedLedgerSupplier.phone}
                </p>
              </div>
              <button 
                onClick={() => setSelectedLedgerSupplier(null)}
                className="text-slate-400 hover:text-white p-1 rounded-lg hover:bg-slate-800 transition"
              >
                <X className="w-6 h-6" />
              </button>
            </div>

            {/* Controls Bar */}
            <div className="p-4 bg-slate-50 border-b border-slate-200 flex justify-between items-center">
              <span className="text-xs font-bold text-slate-600 uppercase tracking-wider">
                Total Transactions: {ledgerEntries.length}
              </span>
              <button 
                onClick={handleExportLedgerCSV}
                disabled={ledgerEntries.length === 0}
                className="bg-slate-800 hover:bg-slate-900 disabled:opacity-50 text-white px-3.5 py-1.5 rounded-lg text-xs font-bold flex items-center gap-1.5 shadow-sm transition"
              >
                <Download className="w-3.5 h-3.5" /> Export CSV
              </button>
            </div>

            {/* Ledger Table */}
            <div className="p-6 overflow-y-auto flex-1">
              {ledgerLoading ? (
                <div className="py-12 text-center text-slate-400 font-semibold">Loading ledger transactions...</div>
              ) : ledgerEntries.length === 0 ? (
                <div className="py-12 text-center text-slate-400 font-semibold">
                  No recorded ledger transactions for this supplier yet.
                </div>
              ) : (
                <table className="w-full text-left text-xs border-collapse">
                  <thead>
                    <tr className="bg-slate-100 border-b border-slate-200 text-slate-700 font-black uppercase">
                      <th className="p-3">Date</th>
                      <th className="p-3">Reference #</th>
                      <th className="p-3">Type</th>
                      <th className="p-3 text-right">Debit (₹)</th>
                      <th className="p-3 text-right">Credit (₹)</th>
                      <th className="p-3 text-right">Balance (₹)</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-slate-200 text-slate-800">
                    {ledgerEntries.map((row, idx) => (
                      <tr key={idx} className="hover:bg-slate-50 transition-colors">
                        <td className="p-3 font-semibold whitespace-nowrap">
                          {row.entryDate ? new Date(row.entryDate).toLocaleDateString('en-IN') : 'N/A'}
                        </td>
                        <td className="p-3 font-black text-slate-900">{row.referenceNumber || '-'}</td>
                        <td className="p-3 font-bold text-slate-600">{row.transactionType || '-'}</td>
                        <td className="p-3 text-right font-bold text-emerald-700">
                          {row.debitAmount > 0 ? `₹${row.debitAmount.toFixed(2)}` : '-'}
                        </td>
                        <td className="p-3 text-right font-bold text-amber-700">
                          {row.creditAmount > 0 ? `₹${row.creditAmount.toFixed(2)}` : '-'}
                        </td>
                        <td className="p-3 text-right font-black text-slate-900 text-sm">
                          ₹{(row.runningBalance || 0).toFixed(2)}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              )}
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
