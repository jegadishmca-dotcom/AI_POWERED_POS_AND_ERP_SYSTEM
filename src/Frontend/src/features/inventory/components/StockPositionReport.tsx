import React, { useState, useEffect } from 'react';
import { Layers, Download, Search, RefreshCw } from 'lucide-react';
import { getStockPositions, getCategories, StockPosition } from '../api/stockTake.api';

export const StockPositionReport = () => {
  const [stockData, setStockData] = useState<StockPosition[]>([]);
  const [searchTerm, setSearchTerm] = useState('');
  const [selectedCategory, setSelectedCategory] = useState('');
  const [categories, setCategories] = useState<{ id: string; name: string }[]>([]);
  const [loading, setLoading] = useState(false);
  const [page, setPage] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const [totalValue, setTotalValue] = useState(0);

  const fetchPositions = async (targetPage: number, term = searchTerm, catId = selectedCategory) => {
    try {
      setLoading(true);
      const response = await getStockPositions({
        storeId: null,
        categoryId: catId || null,
        searchTerm: term.trim() || null,
        page: targetPage,
        pageSize: 50
      });
      setStockData(response.items);
      setTotalCount(response.totalCount);
      setTotalValue(response.totalValue);
      setPage(targetPage);
    } catch (error) {
      console.error('Failed to load stock positions', error);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    const loadCategories = async () => {
      try {
        const cats = await getCategories();
        setCategories(cats);
      } catch (err) {
        console.error('Failed to load categories', err);
      }
    };
    loadCategories();
  }, []);

  useEffect(() => {
    // Fetch on mount and debounce search
    const delayDebounceFn = setTimeout(() => {
      fetchPositions(1, searchTerm, selectedCategory);
    }, 300);

    return () => clearTimeout(delayDebounceFn);
  }, [searchTerm, selectedCategory]);

  const handleReset = () => {
    setSearchTerm('');
    setSelectedCategory('');
    fetchPositions(1, '', '');
  };

  const handleExportCSV = () => {
    if (stockData.length === 0) {
      alert('No data to export.');
      return;
    }

    const headers = ['Product Code', 'Product Name', 'Category', 'Current Stock', 'Last Unit Cost (₹)', 'Total Value (₹)'];
    const rows = stockData.map(item => [
      item.productCode,
      `"${item.productName.replace(/"/g, '""')}"`,
      item.categoryName || 'General',
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
            onClick={() => fetchPositions(page)}
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
          {categories.map((cat) => (
            <option key={cat.id} value={cat.id}>{cat.name}</option>
          ))}
        </select>
        <button 
          onClick={handleReset}
          disabled={loading}
          className="px-4 py-2 bg-slate-100 text-slate-700 hover:bg-slate-200 border rounded-lg transition font-semibold text-sm"
        >
          Clear / Reset
        </button>
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
            ) : stockData.length === 0 ? (
              <tr>
                <td colSpan={6} className="p-8 text-center text-slate-400 font-bold text-sm">
                  No stock items match the current filters.
                </td>
              </tr>
            ) : (
              stockData.map((item) => (
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
              <td className="p-4 text-right text-indigo-700 text-lg font-black">₹{totalValue.toLocaleString('en-IN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}</td>
            </tr>
          </tfoot>
        </table>
      </div>

      {/* Pagination Controls */}
      <div className="flex justify-between items-center mt-6 bg-slate-50 p-4 rounded-xl border border-slate-200">
        <div className="text-sm text-slate-500 font-semibold">
          Showing {totalCount > 0 ? (page - 1) * 50 + 1 : 0} to {Math.min(page * 50, totalCount)} of {totalCount} entries
        </div>
        <div className="flex gap-2">
          <button
            onClick={() => fetchPositions(page - 1)}
            disabled={page <= 1 || loading}
            className="px-4 py-2 bg-white border border-slate-200 rounded-lg text-sm font-semibold text-slate-700 hover:bg-slate-50 disabled:opacity-50 transition"
          >
            Previous
          </button>
          <button
            onClick={() => fetchPositions(page + 1)}
            disabled={page * 50 >= totalCount || loading}
            className="px-4 py-2 bg-white border border-slate-200 rounded-lg text-sm font-semibold text-slate-700 hover:bg-slate-50 disabled:opacity-50 transition"
          >
            Next
          </button>
        </div>
      </div>

    </div>
  );
};
