import React, { useState, useEffect } from 'react';
import { History, FileText, TrendingUp, TrendingDown, RefreshCw } from 'lucide-react';
import api from '../../../utils/api';

interface StockLedgerEntry {
  id: string;
  date: string;
  productName: string;
  movementType: string;
  referenceDocument: string;
  deltaQty: number;
  runningBalance: number;
}

export const StockLedgerView = () => {
  const [ledgerEntries, setLedgerEntries] = useState<StockLedgerEntry[]>([]);
  const [loading, setLoading] = useState(false);
  
  // Filters
  const [searchTerm, setSearchTerm] = useState('');
  const [movementType, setMovementType] = useState('');

  const fetchLedger = async () => {
    try {
      setLoading(true);
      let url = '/api/inventory/ledger?';
      if (searchTerm) url += `searchToken=${encodeURIComponent(searchTerm)}&`;
      if (movementType) url += `movementType=${encodeURIComponent(movementType)}&`;
      
      const response = await api.get(url);
      setLedgerEntries(response.data);
    } catch (error) {
      console.error('Failed to fetch stock ledger', error);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchLedger();
  }, []); // Initial load

  return (
    <div className="bg-white shadow rounded-lg p-6 max-w-7xl mx-auto">
      <div className="flex justify-between items-center mb-6 border-b pb-4">
        <h2 className="text-2xl font-bold text-slate-800 flex items-center">
          <History className="mr-3 text-blue-600" /> Immutable Stock Ledger
        </h2>
        <button 
          onClick={fetchLedger}
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
          className="p-2 border rounded w-1/3 outline-none focus:ring-2 focus:ring-blue-500" 
          value={searchTerm}
          onChange={(e) => setSearchTerm(e.target.value)}
          onKeyDown={(e) => e.key === 'Enter' && fetchLedger()}
        />
        <select 
          className="p-2 border rounded w-1/4 outline-none focus:ring-2 focus:ring-blue-500"
          value={movementType}
          onChange={(e) => setMovementType(e.target.value)}
        >
          <option value="">All Movement Types</option>
          <option value="GRN">Goods Receipt (GRN)</option>
          <option value="SALE">POS Sales</option>
          <option value="ADJ">Adjustments</option>
        </select>
        <button 
          onClick={fetchLedger}
          disabled={loading}
          className="px-4 py-2 bg-blue-600 text-white rounded hover:bg-blue-700 disabled:opacity-50 transition"
        >
          Filter Ledger
        </button>
      </div>

      <div className="overflow-x-auto">
        <table className="w-full text-left border-collapse">
          <thead className="bg-slate-100 text-sm">
            <tr>
              <th className="p-3 border">Date & Time</th>
              <th className="p-3 border">Product</th>
              <th className="p-3 border text-center">Movement</th>
              <th className="p-3 border">Reference Doc</th>
              <th className="p-3 border text-right">Delta Qty</th>
              <th className="p-3 border text-right">Running Balance</th>
            </tr>
          </thead>
          <tbody>
            {ledgerEntries.length === 0 ? (
              <tr>
                <td colSpan={6} className="p-8 text-center text-gray-500">
                  {loading ? 'Loading ledger entries...' : 'No ledger entries found.'}
                </td>
              </tr>
            ) : ledgerEntries.map((entry) => (
              <tr key={entry.id} className="border-b hover:bg-slate-50">
                <td className="p-3 text-sm text-gray-600">{new Date(entry.date).toLocaleString()}</td>
                <td className="p-3 font-bold text-slate-800">{entry.productName}</td>
                <td className="p-3 text-center">
                  <span className={`px-2 py-1 rounded text-xs font-bold ${entry.movementType === 'GRN' ? 'bg-green-100 text-green-800' : entry.movementType === 'SALE' ? 'bg-blue-100 text-blue-800' : 'bg-orange-100 text-orange-800'}`}>
                    {entry.movementType}
                  </span>
                </td>
                <td className="p-3 text-sm text-blue-600 flex items-center cursor-pointer hover:underline">
                  <FileText className="w-4 h-4 mr-1" /> {entry.referenceDocument}
                </td>
                <td className={`p-3 text-right font-bold ${entry.deltaQty > 0 ? 'text-green-600' : 'text-red-600'}`}>
                  <div className="flex items-center justify-end">
                    {entry.deltaQty > 0 ? <TrendingUp className="w-4 h-4 mr-1" /> : <TrendingDown className="w-4 h-4 mr-1" />}
                    {entry.deltaQty > 0 ? '+' : ''}{entry.deltaQty}
                  </div>
                </td>
                <td className="p-3 text-right font-black text-lg text-slate-800">{entry.runningBalance}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
};
