import React, { useState } from 'react';
import { History, FileText, TrendingUp, TrendingDown } from 'lucide-react';

export const StockLedgerView = () => {
  // Mock ledger data for visual structure
  const ledgerEntries = [
    { id: 1, date: '2026-05-15 10:30', product: 'Aashirvaad Atta 5kg', type: 'GRN', ref: 'GRN-2026-001', qty: 100, balance: 150 },
    { id: 2, date: '2026-05-15 11:15', product: 'Aashirvaad Atta 5kg', type: 'SALE', ref: 'T1-20260515-001', qty: -2, balance: 148 },
    { id: 3, date: '2026-05-15 14:00', product: 'Tata Salt 1kg', type: 'ADJ', ref: 'ADJ-101', qty: -1, balance: 49 },
  ];

  return (
    <div className="bg-white shadow rounded-lg p-6 max-w-7xl mx-auto">
      <div className="flex justify-between items-center mb-6 border-b pb-4">
        <h2 className="text-2xl font-bold text-slate-800 flex items-center">
          <History className="mr-3 text-blue-600" /> Immutable Stock Ledger
        </h2>
      </div>

      <div className="flex gap-4 mb-6">
        <input type="text" placeholder="Search Product..." className="p-2 border rounded w-1/3" />
        <select className="p-2 border rounded w-1/4">
          <option value="">All Movement Types</option>
          <option value="GRN">Goods Receipt (GRN)</option>
          <option value="SALE">POS Sales</option>
          <option value="ADJ">Adjustments</option>
        </select>
        <button className="px-4 py-2 bg-blue-600 text-white rounded">Filter Ledger</button>
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
            {ledgerEntries.map((entry) => (
              <tr key={entry.id} className="border-b hover:bg-slate-50">
                <td className="p-3 text-sm text-gray-600">{entry.date}</td>
                <td className="p-3 font-bold text-slate-800">{entry.product}</td>
                <td className="p-3 text-center">
                  <span className={`px-2 py-1 rounded text-xs font-bold ${entry.type === 'GRN' ? 'bg-green-100 text-green-800' : entry.type === 'SALE' ? 'bg-blue-100 text-blue-800' : 'bg-orange-100 text-orange-800'}`}>
                    {entry.type}
                  </span>
                </td>
                <td className="p-3 text-sm text-blue-600 flex items-center cursor-pointer hover:underline">
                  <FileText className="w-4 h-4 mr-1" /> {entry.ref}
                </td>
                <td className={`p-3 text-right font-bold flex items-center justify-end ${entry.qty > 0 ? 'text-green-600' : 'text-red-600'}`}>
                  {entry.qty > 0 ? <TrendingUp className="w-4 h-4 mr-1" /> : <TrendingDown className="w-4 h-4 mr-1" />}
                  {entry.qty > 0 ? '+' : ''}{entry.qty}
                </td>
                <td className="p-3 text-right font-black text-lg text-slate-800">{entry.balance}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
};
