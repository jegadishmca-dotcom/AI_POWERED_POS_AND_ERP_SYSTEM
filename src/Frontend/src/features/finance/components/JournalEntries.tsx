import React, { useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { getJournalEntries } from '../services/finance.service';
import { api } from '../../../utils/api';
import { Modal } from '../../../components/common/Modal';
import { FileText, Plus, Search, Trash2, AlertCircle } from 'lucide-react';
import { formatCurrency } from '../../../utils/formatters';

interface JournalLineItem {
  accountCode: string;
  description: string;
  debit: number;
  credit: number;
}

export const JournalEntries: React.FC = () => {
  const queryClient = useQueryClient();
  const [startDate, setStartDate] = useState(() => {
    const d = new Date();
    d.setMonth(d.getMonth() - 1);
    return d.toISOString().split('T')[0];
  });
  const [endDate, setEndDate] = useState(new Date().toISOString().split('T')[0]);
  const [storeId, setStoreId] = useState('');
  const [searchTerm, setSearchTerm] = useState('');

  // Modal State
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [entryDate, setEntryDate] = useState(new Date().toISOString().split('T')[0]);
  const [description, setDescription] = useState('');
  const [referenceDocument, setReferenceDocument] = useState('');
  const [lines, setLines] = useState<JournalLineItem[]>([
    { accountCode: '1010', description: 'Cash / Bank', debit: 1000, credit: 0 },
    { accountCode: '4010', description: 'Sales Revenue', debit: 0, credit: 1000 },
  ]);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const { data: entries, isLoading } = useQuery({
    queryKey: ['journalEntries', storeId, startDate, endDate],
    queryFn: () => getJournalEntries(storeId || undefined, startDate, endDate)
  });

  const totalDebit = lines.reduce((acc, l) => acc + (Number(l.debit) || 0), 0);
  const totalCredit = lines.reduce((acc, l) => acc + (Number(l.credit) || 0), 0);
  const isUnbalanced = totalDebit !== totalCredit || totalDebit <= 0;

  const handleAddLine = () => {
    setLines([...lines, { accountCode: '', description: '', debit: 0, credit: 0 }]);
  };

  const handleRemoveLine = (index: number) => {
    if (lines.length <= 2) return;
    setLines(lines.filter((_, i) => i !== index));
  };

  const handleLineChange = (index: number, field: keyof JournalLineItem, value: any) => {
    const next = [...lines];
    next[index] = { ...next[index], [field]: value };
    setLines(next);
  };

  const handleSaveJournal = async (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMessage(null);

    if (isUnbalanced) {
      setErrorMessage(`Journal is unbalanced. Total Debit: ₹${totalDebit.toFixed(2)}, Total Credit: ₹${totalCredit.toFixed(2)}`);
      return;
    }

    setIsSubmitting(true);
    try {
      await api.post('/api/journalentries', {
        storeId: '00000000-0000-0000-0000-000000000000',
        entryDate: new Date(entryDate).toISOString(),
        description: description.trim() || 'Manual Journal Entry',
        referenceDocument: referenceDocument.trim() || undefined,
        lines: lines.map(l => ({
          accountCode: l.accountCode.trim(),
          description: l.description.trim() || description.trim(),
          debit: Number(l.debit) || 0,
          credit: Number(l.credit) || 0
        }))
      });
      queryClient.invalidateQueries({ queryKey: ['journalEntries'] });
      setIsModalOpen(false);
      setDescription('');
      setReferenceDocument('');
    } catch (err: any) {
      setErrorMessage(err.response?.data?.message || err.message || 'Failed to post journal entry.');
    } finally {
      setIsSubmitting(false);
    }
  };

  const filteredEntries = entries?.filter((entry: any) => {
    const entryNum = entry.entryNumber || '';
    const desc = entry.description || '';
    return entryNum.toLowerCase().includes(searchTerm.toLowerCase()) || 
           desc.toLowerCase().includes(searchTerm.toLowerCase());
  }) || [];

  return (
    <div className="p-6 max-w-7xl mx-auto">
      <div className="flex flex-col md:flex-row md:items-center justify-between mb-8 gap-4">
        <div>
          <h2 className="text-2xl font-extrabold text-slate-800 dark:text-white flex items-center gap-3">
            <FileText className="w-7 h-7 text-indigo-600" />
            Journal Entries
          </h2>
          <p className="text-slate-500 dark:text-slate-400 mt-1">General ledger transactions and manual journals</p>
        </div>
        <button 
          onClick={() => setIsModalOpen(true)}
          className="bg-indigo-600 hover:bg-indigo-700 text-white px-4 py-2.5 rounded-lg font-bold flex items-center gap-2 shadow-lg shadow-indigo-600/30 transition-all cursor-pointer"
        >
          <Plus className="w-5 h-5" />
          New Journal Entry
        </button>
      </div>

      <div className="bg-white dark:bg-slate-900 rounded-xl shadow-sm border border-slate-200 dark:border-slate-800 overflow-hidden mb-6">
        <div className="p-4 bg-slate-50 dark:bg-slate-950 flex flex-wrap gap-4 items-end">
          <div className="flex-1 min-w-[250px]">
            <label className="block text-xs font-bold text-slate-500 uppercase tracking-wider mb-1.5">Search</label>
            <div className="relative">
              <Search className="w-5 h-5 absolute left-3 top-2.5 text-slate-400" />
              <input 
                type="text" 
                placeholder="Entry Number or Description..."
                value={searchTerm}
                onChange={(e) => setSearchTerm(e.target.value)}
                className="w-full pl-10 pr-3 py-2 bg-white dark:bg-slate-900 border border-slate-300 dark:border-slate-700 rounded-lg text-sm text-slate-900 dark:text-white focus:ring-2 focus:ring-indigo-500 outline-none"
              />
            </div>
          </div>
          <div className="w-[160px]">
            <label className="block text-xs font-bold text-slate-500 uppercase tracking-wider mb-1.5">From Date</label>
            <input 
              type="date" 
              value={startDate}
              onChange={(e) => setStartDate(e.target.value)}
              className="w-full px-3 py-2 bg-white dark:bg-slate-900 border border-slate-300 dark:border-slate-700 rounded-lg text-sm text-slate-900 dark:text-white focus:ring-2 focus:ring-indigo-500 outline-none"
            />
          </div>
          <div className="w-[160px]">
            <label className="block text-xs font-bold text-slate-500 uppercase tracking-wider mb-1.5">To Date</label>
            <input 
              type="date" 
              value={endDate}
              onChange={(e) => setEndDate(e.target.value)}
              className="w-full px-3 py-2 bg-white dark:bg-slate-900 border border-slate-300 dark:border-slate-700 rounded-lg text-sm text-slate-900 dark:text-white focus:ring-2 focus:ring-indigo-500 outline-none"
            />
          </div>
          <div className="w-[200px]">
            <label className="block text-xs font-bold text-slate-500 uppercase tracking-wider mb-1.5">Store</label>
            <select
              value={storeId}
              onChange={(e) => setStoreId(e.target.value)}
              className="w-full px-3 py-2 bg-white dark:bg-slate-900 border border-slate-300 dark:border-slate-700 rounded-lg text-sm text-slate-900 dark:text-white focus:ring-2 focus:ring-indigo-500 outline-none"
            >
              <option value="">All Stores</option>
            </select>
          </div>
        </div>
      </div>

      <div className="space-y-4">
        {isLoading ? (
          <div className="flex justify-center items-center h-48 text-slate-400">Loading Journal Entries...</div>
        ) : filteredEntries.length > 0 ? (
          filteredEntries.map((entry: any) => (
            <div key={entry.id} className="bg-white dark:bg-slate-900 rounded-xl shadow-sm border border-slate-200 dark:border-slate-800 overflow-hidden">
              <div className="p-4 bg-slate-50 dark:bg-slate-950 border-b border-slate-200 dark:border-slate-800 flex justify-between items-center">
                <div>
                  <h3 className="font-bold text-indigo-600 dark:text-indigo-400 text-lg flex items-center gap-2">
                    {entry.entryNumber}
                    {entry.isPosted ? (
                      <span className="text-[10px] bg-emerald-100 text-emerald-800 px-2 py-0.5 rounded-full uppercase tracking-widest border border-emerald-200">Posted</span>
                    ) : (
                      <span className="text-[10px] bg-amber-100 text-amber-800 px-2 py-0.5 rounded-full uppercase tracking-widest border border-amber-200">Draft</span>
                    )}
                  </h3>
                  <div className="text-sm text-slate-500 mt-1 flex items-center gap-3">
                    <span>{new Date(entry.entryDate).toLocaleDateString('en-IN')}</span>
                    <span className="w-1 h-1 rounded-full bg-slate-300"></span>
                    <span>Ref: {entry.referenceDocument || 'N/A'}</span>
                  </div>
                </div>
                <div className="text-right">
                  <p className="text-sm font-semibold text-slate-700 dark:text-slate-200">{entry.description}</p>
                </div>
              </div>
              <div className="p-0">
                <table className="w-full text-left text-sm">
                  <thead className="bg-slate-50/50 dark:bg-slate-900/50 border-b border-slate-100 dark:border-slate-800 text-xs font-bold text-slate-500 uppercase tracking-wider">
                    <tr>
                      <th className="p-3 pl-4 w-1/3">Account</th>
                      <th className="p-3 w-1/3">Line Description</th>
                      <th className="p-3 text-right">Debit</th>
                      <th className="p-3 pr-4 text-right">Credit</th>
                    </tr>
                  </thead>
                  <tbody>
                    {entry.lines?.map((line: any) => (
                      <tr key={line.id} className="border-b border-slate-50 dark:border-slate-800/50 last:border-0 hover:bg-slate-50/50 dark:hover:bg-slate-800/30">
                        <td className="p-3 pl-4">
                          <span className="font-mono text-indigo-500 mr-2">{line.account?.accountCode}</span>
                          <span className="text-slate-700 dark:text-slate-300">{line.account?.name}</span>
                        </td>
                        <td className="p-3 text-slate-600 dark:text-slate-400">{line.description}</td>
                        <td className="p-3 text-right text-slate-700 dark:text-slate-300">{line.debitAmount > 0 ? formatCurrency(line.debitAmount) : '-'}</td>
                        <td className="p-3 pr-4 text-right text-slate-700 dark:text-slate-300">{line.creditAmount > 0 ? formatCurrency(line.creditAmount) : '-'}</td>
                      </tr>
                    ))}
                    <tr className="bg-slate-50 dark:bg-slate-800/30 font-bold border-t border-slate-200 dark:border-slate-700">
                      <td colSpan={2} className="p-3 text-right text-slate-500 uppercase tracking-wider text-xs">Total</td>
                      <td className="p-3 text-right text-indigo-600">{formatCurrency(entry.lines?.reduce((sum: number, l: any) => sum + l.debitAmount, 0) || 0)}</td>
                      <td className="p-3 pr-4 text-right text-indigo-600">{formatCurrency(entry.lines?.reduce((sum: number, l: any) => sum + l.creditAmount, 0) || 0)}</td>
                    </tr>
                  </tbody>
                </table>
              </div>
            </div>
          ))
        ) : (
          <div className="text-center p-12 text-slate-500 bg-white dark:bg-slate-900 rounded-xl border border-slate-200 dark:border-slate-800">
            No journal entries found for the selected criteria.
          </div>
        )}
      </div>

      {/* NEW JOURNAL ENTRY MODAL */}
      <Modal
        isOpen={isModalOpen}
        onClose={() => setIsModalOpen(false)}
        title="Post New Journal Entry"
        subtitle="Create balanced double-entry manual journal voucher"
        maxWidth="max-w-3xl"
      >
        <form onSubmit={handleSaveJournal} className="space-y-4">
          {errorMessage && (
            <div className="p-3 bg-red-50 border border-red-200 dark:bg-red-950/40 dark:border-red-800 rounded-xl text-red-600 dark:text-red-300 text-sm flex items-center gap-2">
              <AlertCircle className="w-4 h-4 shrink-0" />
              <span>{errorMessage}</span>
            </div>
          )}

          <div className="grid grid-cols-3 gap-4">
            <div>
              <label className="block text-xs font-bold text-slate-600 dark:text-slate-400 uppercase mb-1">Entry Date *</label>
              <input
                type="date"
                required
                value={entryDate}
                onChange={(e) => setEntryDate(e.target.value)}
                className="w-full px-3 py-2 border border-slate-300 dark:border-slate-700 bg-white dark:bg-slate-900 rounded-lg text-sm dark:text-white outline-none focus:ring-2 focus:ring-indigo-500"
              />
            </div>
            <div>
              <label className="block text-xs font-bold text-slate-600 dark:text-slate-400 uppercase mb-1">Journal Description *</label>
              <input
                type="text"
                required
                placeholder="e.g. Monthly rent allocation"
                value={description}
                onChange={(e) => setDescription(e.target.value)}
                className="w-full px-3 py-2 border border-slate-300 dark:border-slate-700 bg-white dark:bg-slate-900 rounded-lg text-sm dark:text-white outline-none focus:ring-2 focus:ring-indigo-500"
              />
            </div>
            <div>
              <label className="block text-xs font-bold text-slate-600 dark:text-slate-400 uppercase mb-1">Ref Document #</label>
              <input
                type="text"
                placeholder="e.g. INV-9982, VOUCHER-01"
                value={referenceDocument}
                onChange={(e) => setReferenceDocument(e.target.value)}
                className="w-full px-3 py-2 border border-slate-300 dark:border-slate-700 bg-white dark:bg-slate-900 rounded-lg text-sm dark:text-white outline-none focus:ring-2 focus:ring-indigo-500"
              />
            </div>
          </div>

          {/* Line items table */}
          <div>
            <div className="flex justify-between items-center mb-2">
              <span className="text-xs font-bold text-slate-600 dark:text-slate-400 uppercase">Journal Lines</span>
              <button
                type="button"
                onClick={handleAddLine}
                className="text-xs font-bold text-indigo-600 hover:text-indigo-700 flex items-center gap-1"
              >
                <Plus className="w-3.5 h-3.5" /> Add Line Item
              </button>
            </div>

            <div className="border border-slate-200 dark:border-slate-800 rounded-xl overflow-hidden">
              <table className="w-full text-left text-xs">
                <thead className="bg-slate-100 dark:bg-slate-950 font-bold text-slate-600 dark:text-slate-400">
                  <tr>
                    <th className="p-2 w-32">Account Code</th>
                    <th className="p-2">Line Narration</th>
                    <th className="p-2 w-28 text-right">Debit (₹)</th>
                    <th className="p-2 w-28 text-right">Credit (₹)</th>
                    <th className="p-2 w-10 text-center"></th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-200 dark:divide-slate-800">
                  {lines.map((line, idx) => (
                    <tr key={idx} className="bg-white dark:bg-slate-900">
                      <td className="p-2">
                        <input
                          type="text"
                          required
                          placeholder="Code (1010)"
                          value={line.accountCode}
                          onChange={(e) => handleLineChange(idx, 'accountCode', e.target.value)}
                          className="w-full px-2 py-1 border border-slate-300 dark:border-slate-700 rounded bg-white dark:bg-slate-950 text-xs font-mono dark:text-white"
                        />
                      </td>
                      <td className="p-2">
                        <input
                          type="text"
                          placeholder="Line details..."
                          value={line.description}
                          onChange={(e) => handleLineChange(idx, 'description', e.target.value)}
                          className="w-full px-2 py-1 border border-slate-300 dark:border-slate-700 rounded bg-white dark:bg-slate-950 text-xs dark:text-white"
                        />
                      </td>
                      <td className="p-2 text-right">
                        <input
                          type="number"
                          min="0"
                          step="0.01"
                          value={line.debit || ''}
                          onChange={(e) => handleLineChange(idx, 'debit', parseFloat(e.target.value) || 0)}
                          className="w-full px-2 py-1 border border-slate-300 dark:border-slate-700 rounded bg-white dark:bg-slate-950 text-xs text-right font-mono dark:text-white"
                        />
                      </td>
                      <td className="p-2 text-right">
                        <input
                          type="number"
                          min="0"
                          step="0.01"
                          value={line.credit || ''}
                          onChange={(e) => handleLineChange(idx, 'credit', parseFloat(e.target.value) || 0)}
                          className="w-full px-2 py-1 border border-slate-300 dark:border-slate-700 rounded bg-white dark:bg-slate-950 text-xs text-right font-mono dark:text-white"
                        />
                      </td>
                      <td className="p-2 text-center">
                        {lines.length > 2 && (
                          <button
                            type="button"
                            onClick={() => handleRemoveLine(idx)}
                            className="text-slate-400 hover:text-red-600 transition"
                          >
                            <Trash2 className="w-4 h-4 mx-auto" />
                          </button>
                        )}
                      </td>
                    </tr>
                  ))}
                  <tr className="bg-slate-50 dark:bg-slate-950 font-bold border-t border-slate-200 dark:border-slate-800">
                    <td colSpan={2} className="p-2.5 text-right uppercase tracking-wider text-[11px] text-slate-500">Totals:</td>
                    <td className={`p-2.5 text-right font-mono text-xs ${totalDebit !== totalCredit ? 'text-red-600' : 'text-emerald-600'}`}>
                      ₹{totalDebit.toFixed(2)}
                    </td>
                    <td className={`p-2.5 text-right font-mono text-xs ${totalDebit !== totalCredit ? 'text-red-600' : 'text-emerald-600'}`}>
                      ₹{totalCredit.toFixed(2)}
                    </td>
                    <td></td>
                  </tr>
                </tbody>
              </table>
            </div>

            {totalDebit !== totalCredit && (
              <p className="text-xs font-bold text-red-500 mt-2 flex items-center gap-1">
                <AlertCircle className="w-3.5 h-3.5" />
                Debits and Credits must balance! Discrepancy: ₹{Math.abs(totalDebit - totalCredit).toFixed(2)}
              </p>
            )}
          </div>

          <div className="pt-4 flex justify-end gap-3 border-t border-slate-200 dark:border-slate-800">
            <button
              type="button"
              onClick={() => setIsModalOpen(false)}
              className="px-4 py-2 text-sm font-semibold text-slate-600 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-800 rounded-lg transition"
            >
              Cancel
            </button>
            <button
              type="submit"
              disabled={isSubmitting || isUnbalanced}
              className="px-5 py-2 text-sm font-bold text-white bg-indigo-600 hover:bg-indigo-700 disabled:opacity-50 rounded-lg shadow-md shadow-indigo-600/30 transition flex items-center gap-2"
            >
              {isSubmitting ? 'Posting...' : 'Post Journal Entry'}
            </button>
          </div>
        </form>
      </Modal>
    </div>
  );
};
