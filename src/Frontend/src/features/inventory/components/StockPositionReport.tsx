import React, { useState, useEffect } from 'react';
import { Layers, Download, Search, RefreshCw } from 'lucide-react';
import { getStockPositions, StockPosition } from '../api/stockTake.api';

export const StockPositionReport = () => {
  const [stockData, setStockData] = useState<StockPosition[]>([]);
  const [searchTerm, setSearchTerm] = useState('');
  const [selectedCategory, setSelectedCategory] = useState('');
  const [loading, setLoading] = useState(false);

  const fetchPositions = async () => {
    try {
      setLoading(true);
      const data = await getStockPositions({
        storeId: null,
        categoryId: null,
        searchTerm: searchTerm.trim() || null
      });
      setStockData(data);
    } catch (error) {
      console.error('Failed to load stock positions', error);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    // Fetch on mount and debounce search
    const delayDebounceFn = setTimeout(() => {
      fetchPositions();
    }, 300);

    return () => clearTimeout(delayDebounceFn);
  }, [searchTerm]);

  // Extract distinct categories from the loaded stock data to populate filter dropdown dynamically
  const categories = Array.from(new Set(stockData.map(item => item.categoryName))).filter(Boolean);

  // Filter stock data by category client-side
  const filteredData = stockData.filter(item => {
    if (!selectedCategory) return true;
    return item.categoryName === selectedCategory;
  });

  const totalInventoryValue = filteredData.reduce((sum, item) => sum + item.totalValue, 0);

  const handleExportCSV = () => {
    if (filteredData.length === 0) {
      alert('No data to export.');
      return;
    }

    const headers = ['Product Code', 'Product Name', 'Category', 'Current Stock', 'Last Unit Cost (₹)', 'Total Value (₹)'];
    const rows = filteredData.map(item => [
      item.productCode,
      `"${item.productName.replace(/"/g, '""')}"`,
      item.categoryName,
      item.currentStock,
      item.lastUnitCost.toFixed(2),
      item.totalValue.toFixed(2)
    ]);

    const csvContent = 'data:text/csv;charset=utf-8,' 
      + [headers.join(','), ...rows.map(e => e.join(','))].join('\n');
    
    const encodedUri = encodeURI(csvContent);
    const link = document.createElement('a');
    link.setAttribute('href', encodedUri);
    link.setAttribute('download', `Stock_Position_Report_${new Date().toISOString().substring(0, 10)}.csv`);
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
  };

  return (
    <div className="bg-white shadow border border-slate-200 rounded-xl p-6 max-w-7xl mx-auto">
      
      {/* Header */}
      <div className="flex justify-between items-center mb-6 border-b pb-4">
        <div>
          <h2 className="text-2xl font-bold text-slate-800 flex items-center">
            <Layers className="mr-3 text-indigo-600 w-6 h-6" /> Current Stock Position
          </h2>
          <p className="text-xs text-slate-500 mt-1">Live snapshot of inventory quantities and asset valuations across all products.</p>
        </div>
        <div className="flex gap-2">
          <button 
            onClick={fetchPositions}
            disabled={loading}
            className="p-2.5 bg-slate-100 hover:bg-slate-200 text-slate-600 rounded-lg border flex items-center transition"
            title="Refresh positions"
          >
            <RefreshCw className={`w-4 h-4 ${loading ? 'animate-spin' : ''}`} />
          </button>
          <button 
            onClick={handleExportCSV}
            className="px-4 py-2 bg-slate-800 text-white rounded-lg shadow-sm flex items-center hover:bg-slate-700 font-bold transition text-sm"
          >
            <Download className="w-4 h-4 mr-2" /> Export to CSV
          </button>
        </div>
      </div>

      {/* Filter panel */}
      <div className="flex gap-4 mb-6 bg-slate-50 p-4 rounded-xl border border-slate-200">
        <div className="relative flex-1">
          <Search className="absolute left-3 top-3 text-gray-400 w-4 h-4" />
          <input 
            type="text" 
            placeholder="Search Product Code or Name..." 
            className="w-full pl-9 p-2.5 border rounded-lg text-sm bg-white outline-none focus:ring-2 focus:ring-indigo-500 font-semibold" 
            value={searchTerm}
            onChange={(e) => setSearchTerm(e.target.value)}
          />
        </div>
        <select 
          className="p-2.5 border rounded-lg text-sm w-52 font-semibold text-slate-700 bg-white outline-none focus:ring-2 focus:ring-indigo-500"
          value={selectedCategory}
          onChange={(e) => setSelectedCategory(e.target.value)}
        >
          <option value="">All Categories</option>
          {categories.map((cat, idx) => (
            <option key={idx} value={cat}>{cat}</option>
          ))}
        </select>
      </div>

      {/* Positions Table */}
      <div className="overflow-x-auto border rounded-xl shadow-sm">
        <table className="w-full text-left border-collapse">
          <thead className="bg-indigo-50/50 text-indigo-900 text-xs font-bold uppercase tracking-wider">
            <tr>
              <th className="p-4 border-b">Product Code</th>
              <th className="p-4 border-b">Product Name</th>
              <th className="p-4 border-b">Category</th>
              <th className="p-4 border-b text-right">Current Stock</th>
              <th className="p-4 border-b text-right">Last Unit Cost</th>
              <th className="p-4 border-b text-right">Stock Value</th>
            </tr>
          </thead>
          <tbody>
            {loading ? (
              <tr>
                <td colSpan={6} className="p-8 text-center text-slate-400 font-bold text-sm">
                  <RefreshCw className="w-6 h-6 animate-spin mx-auto mb-2 text-indigo-600" />
                  Loading inventory position reports...
                </td>
              </tr>
            ) : filteredData.length === 0 ? (
              <tr>
                <td colSpan={6} className="p-8 text-center text-slate-400 font-bold text-sm">
                  No stock items match the current filters.
                </td>
              </tr>
            ) : (
              filteredData.map((item) => (
                <tr key={item.productId} className="border-b hover:bg-slate-50/50 transition">
                  <td className="p-4 text-xs font-mono text-slate-500">{item.productCode}</td>
                  <td className="p-4 font-bold text-slate-800">{item.productName}</td>
                  <td className="p-4 text-sm font-semibold text-slate-500">
                    <span className="bg-slate-100 px-2 py-0.5 rounded">{item.categoryName || 'General'}</span>
                  </td>
                  <td className="p-4 text-right font-black text-md text-slate-800">
                    <span className={item.currentStock < 10 ? 'text-red-600 bg-red-50 px-2 py-0.5 rounded' : ''}>
                      {item.currentStock}
                    </span>
                  </td>
                  <td className="p-4 text-right font-bold text-slate-600">₹{item.lastUnitCost.toFixed(2)}</td>
                  <td className="p-4 text-right font-black text-slate-800">₹{item.totalValue.toFixed(2)}</td>
                </tr>
              ))
            )}
          </tbody>
          <tfoot className="bg-slate-50/50 font-bold text-slate-700">
            <tr>
              <td colSpan={5} className="p-4 text-right text-sm">Total Inventory Valuation:</td>
              <td className="p-4 text-right text-indigo-700 text-lg font-black">₹{totalInventoryValue.toLocaleString('en-IN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}</td>
            </tr>
          </tfoot>
        </table>
      </div>
    </div>
  );
};
