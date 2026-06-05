import React, { useState, useEffect } from 'react';
import { History, FileText, TrendingUp, TrendingDown, RefreshCw } from 'lucide-react';
import { api } from '../../../utils/api';
import { DocumentPreviewModal } from './DocumentPreviewModal';

interface StockLedgerEntry {
  id: string;
  date: string;
  productName: string;
  movementType: string;
  referenceDocument: string;
  deltaQty: number;
  runningBalance: number;
  batchNumber?: string | null;
  expiryDate?: string | null;
  referenceDocumentId: string;
}

export const StockLedgerView = () => {
  const [ledgerEntries, setLedgerEntries] = useState<StockLedgerEntry[]>([]);
  const [loading, setLoading] = useState(false);
  const [previewDoc, setPreviewDoc] = useState<{ id: string; type: string; referenceNumber?: string } | null>(null);
  
  // Filters & Pagination
  const [searchTerm, setSearchTerm] = useState('');
  const [movementType, setMovementType] = useState('');
  const [page, setPage] = useState(1);
  const [totalCount, setTotalCount] = useState(0);

  const fetchLedger = async (targetPage: number, term = searchTerm, moveType = movementType) => {
    try {
      setLoading(true);
      let url = `/api/inventory/ledger?page=${targetPage}&pageSize=50&`;
      if (term) url += `searchToken=${encodeURIComponent(term)}&`;
      if (moveType) url += `movementType=${encodeURIComponent(moveType)}&`;
      
      const response = await api.get(url);
      setLedgerEntries(response.data.items);
      setTotalCount(response.data.totalCount);
      setPage(targetPage);
    } catch (error) {
      console.error('Failed to fetch stock ledger', error);
    } finally {
      setLoading(false);
    }
  };

  const handleReset = () => {
    setSearchTerm('');
    setMovementType('');
    fetchLedger(1, '', '');
  };

  const handleExportExcel = async () => {
    try {
      setLoading(true);
      let url = `/api/inventory/ledger?page=1&pageSize=-1&`;
      if (searchTerm) url += `searchToken=${encodeURIComponent(searchTerm)}&`;
      if (movementType) url += `movementType=${encodeURIComponent(movementType)}&`;
      
      const response = await api.get(url);
      const items = response.data.items || [];
      
      if (items.length === 0) {
        alert('No data to export.');
        return;
      }
      
      const headers = ['Date & Time', 'Product', 'Batch No', 'Expiry Date', 'Movement', 'Reference Doc', 'Delta Qty', 'Running Balance'];
      const rows = items.map((entry: any) => [
        `"${new Date(entry.date).toLocaleString()}"`,
        `"${entry.productName.replace(/"/g, '""')}"`,
        `"${(entry.batchNumber || '').replace(/"/g, '""')}"`,
        entry.expiryDate ? entry.expiryDate.substring(0, 10) : '-',
        entry.movementType,
        entry.referenceDocument,
        entry.deltaQty,
        entry.runningBalance
      ]);

      const csvString = [headers.join(','), ...rows.map((e: any) => e.join(','))].join('\n');
      const blob = new Blob([new Uint8Array([0xEF, 0xBB, 0xBF]), csvString], { type: 'text/csv;charset=utf-8;' });
      const downloadUrl = URL.createObjectURL(blob);
      
      const link = document.createElement('a');
      link.setAttribute('href', downloadUrl);
      link.setAttribute('download', `Stock_Ledger_Export_${new Date().toISOString().substring(0, 10)}.csv`);
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);
      URL.revokeObjectURL(downloadUrl);
    } catch (error) {
      console.error('Failed to export stock ledger to excel', error);
      alert('Failed to export data.');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchLedger(1);
  }, []); // Initial load

  return (
    <div className="bg-white shadow rounded-lg p-6 max-w-7xl mx-auto">
      <div className="flex justify-between items-center mb-6 border-b pb-4">
        <h2 className="text-2xl font-bold text-slate-800 flex items-center">
          <History className="mr-3 text-blue-600" /> Immutable Stock Ledger
        </h2>
        <button 
          onClick={() => fetchLedger(page)}
          className="text-gray-500 hover:text-blue-600"
          title="Refresh Ledger"
        >
          <RefreshCw className={`w-5 h-5 ${loading ? 'animate-spin text-blue-600' : ''}`} />
        </button>
      </div>

      <div className="flex gap-4 mb-6">
        <input 
          type="text" 
          placeholder="Search Product or Ref..." 
          className="p-2 border rounded w-1/3 outline-none focus:ring-2 focus:ring-blue-500 font-semibold" 
          value={searchTerm}
          onChange={(e) => setSearchTerm(e.target.value)}
          onKeyDown={(e) => e.key === 'Enter' && fetchLedger(1)}
        />
        <select 
          className="p-2 border rounded w-1/4 outline-none focus:ring-2 focus:ring-blue-500 font-semibold text-slate-700"
          value={movementType}
          onChange={(e) => {
            setMovementType(e.target.value);
            fetchLedger(1, searchTerm, e.target.value);
          }}
        >
          <option value="">All Movement Types</option>
          <option value="GRN">Goods Receipt (GRN)</option>
          <option value="SALE">POS Sales</option>
          <option value="ADJ">Adjustments</option>
        </select>
        <button 
          onClick={() => fetchLedger(1)}
          disabled={loading}
          className="px-4 py-2 bg-blue-600 text-white rounded hover:bg-blue-700 disabled:opacity-50 transition font-bold"
        >
          Filter Ledger
        </button>
        <button 
          onClick={handleReset}
          disabled={loading}
          className="px-4 py-2 bg-slate-100 text-slate-700 hover:bg-slate-200 border rounded transition font-bold"
        >
          Clear / Reset
        </button>
        <button 
          onClick={handleExportExcel}
          disabled={loading}
          className="px-4 py-2 bg-emerald-600 hover:bg-emerald-700 text-white rounded transition font-bold flex items-center shadow-sm"
        >
          <FileText className="w-4 h-4 mr-2" /> Export to Excel
        </button>
      </div>

      <div className="overflow-x-auto">
        <table className="w-full text-left border-collapse">
          <thead className="bg-slate-50 dark:bg-slate-900 border-b-2 border-blue-200 dark:border-blue-800 text-sm">
            <tr>
              <th className="p-3 font-semibold text-slate-500 dark:text-slate-400">Date & Time</th>
              <th className="p-3 font-semibold text-slate-500 dark:text-slate-400">Product</th>
              <th className="p-3 font-semibold text-slate-500 dark:text-slate-400">Batch No</th>
              <th className="p-3 font-semibold text-slate-500 dark:text-slate-400">Expiry Date</th>
              <th className="p-3 font-semibold text-slate-500 dark:text-slate-400 text-center">Movement</th>
              <th className="p-3 font-semibold text-slate-500 dark:text-slate-400">Reference Doc</th>
              <th className="p-3 font-semibold text-slate-500 dark:text-slate-400 text-right">Delta Qty</th>
              <th className="p-3 font-semibold text-slate-500 dark:text-slate-400 text-right">Running Balance</th>
            </tr>
          </thead>
          <tbody className="bg-white dark:bg-slate-800">
            {ledgerEntries.length === 0 ? (
              <tr>
                <td colSpan={8} className="p-8 text-center text-gray-500 border-b-2 border-blue-200 dark:border-blue-800/80">
                  {loading ? 'Loading ledger entries...' : 'No ledger entries found.'}
                </td>
              </tr>
            ) : ledgerEntries.map((entry) => (
              <tr key={entry.id} className="hover:bg-slate-50 dark:hover:bg-slate-700/50">
                <td className="p-3 text-sm text-gray-600 dark:text-slate-300 border-b-2 border-blue-200 dark:border-blue-800/80">{new Date(entry.date).toLocaleString()}</td>
                <td className="p-3 font-bold text-slate-800 dark:text-white border-b-2 border-blue-200 dark:border-blue-800/80">{entry.productName}</td>
                <td className="p-3 text-sm text-slate-600 dark:text-slate-300 font-semibold border-b-2 border-blue-200 dark:border-blue-800/80">{entry.batchNumber || '-'}</td>
                <td className="p-3 text-sm text-slate-600 dark:text-slate-300 font-semibold border-b-2 border-blue-200 dark:border-blue-800/80">{entry.expiryDate ? entry.expiryDate.substring(0, 10) : '-'}</td>
                <td className="p-3 text-center border-b-2 border-blue-200 dark:border-blue-800/80">
                  <span className={`px-2 py-1 rounded text-xs font-bold ${entry.movementType === 'GRN' ? 'bg-green-100 text-green-800 dark:bg-green-950 dark:text-green-300' : entry.movementType === 'SALE' ? 'bg-blue-100 text-blue-800 dark:bg-blue-950 dark:text-blue-300' : 'bg-orange-100 text-orange-800 dark:bg-orange-950 dark:text-orange-300'}`}>
                    {entry.movementType}
                  </span>
                </td>
                <td 
                  className="p-3 text-sm text-blue-600 dark:text-blue-400 flex items-center cursor-pointer hover:underline border-b-2 border-blue-200 dark:border-blue-800/80"
                  onClick={() => setPreviewDoc({ id: entry.referenceDocumentId, type: entry.movementType, referenceNumber: entry.referenceDocument })}
                >
                  <FileText className="w-4 h-4 mr-1" /> {entry.referenceDocument}
                </td>
                <td className={`p-3 text-right font-bold border-b-2 border-blue-200 dark:border-blue-800/80 ${entry.deltaQty > 0 ? 'text-green-600 dark:text-green-400' : 'text-red-600 dark:text-red-400'}`}>
                  <div className="flex items-center justify-end">
                    {entry.deltaQty > 0 ? <TrendingUp className="w-4 h-4 mr-1" /> : <TrendingDown className="w-4 h-4 mr-1" />}
                    {entry.deltaQty > 0 ? '+' : ''}{entry.deltaQty}
                  </div>
                </td>
                <td className="p-3 text-right font-black text-lg text-slate-800 dark:text-white border-b-2 border-blue-200 dark:border-blue-800/80">{entry.runningBalance}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {/* Pagination Controls */}
      <div className="flex justify-between items-center mt-6 bg-slate-50 p-4 rounded-xl border border-slate-200">
        <div className="text-sm text-slate-500 font-semibold">
          Showing {totalCount > 0 ? (page - 1) * 50 + 1 : 0} to {Math.min(page * 50, totalCount)} of {totalCount} entries
        </div>
        <div className="flex gap-2">
          <button
            onClick={() => fetchLedger(page - 1)}
            disabled={page <= 1 || loading}
            className="px-4 py-2 bg-white border border-slate-200 rounded-lg text-sm font-semibold text-slate-700 hover:bg-slate-50 disabled:opacity-50 transition"
          >
            Previous
          </button>
          <button
            onClick={() => fetchLedger(page + 1)}
            disabled={page * 50 >= totalCount || loading}
            className="px-4 py-2 bg-white border border-slate-200 rounded-lg text-sm font-semibold text-slate-700 hover:bg-slate-50 disabled:opacity-50 transition"
          >
            Next
          </button>
        </div>
      </div>

      {previewDoc && (
        <DocumentPreviewModal 
          docId={previewDoc.id} 
          docType={previewDoc.type} 
          referenceNumber={previewDoc.referenceNumber}
          onClose={() => setPreviewDoc(null)} 
        />
      )}
    </div>
  );
};
