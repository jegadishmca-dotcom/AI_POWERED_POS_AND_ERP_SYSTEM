import React, { useState, useEffect } from 'react';
import { useQuery } from '@tanstack/react-query';
import { searchProducts } from '../api/catalog.api';
import { Search, Package, Plus } from 'lucide-react';
// import { useDebounce } from '@/hooks/useDebounce'; // Assuming generic debounce hook

export const ProductList = ({ onImportClick }: { onImportClick: () => void }) => {
  const [searchTerm, setSearchTerm] = useState('');
  
  // Custom quick debounce implementation for this snippet
  const [debouncedTerm, setDebouncedTerm] = useState(searchTerm);
  useEffect(() => {
    const timer = setTimeout(() => setDebouncedTerm(searchTerm), 300);
    return () => clearTimeout(timer);
  }, [searchTerm]);

  const { data: products, isLoading } = useQuery({
    queryKey: ['products', 'search', debouncedTerm],
    queryFn: () => searchProducts(debouncedTerm),
  });

  return (
    <div className="bg-white dark:bg-slate-800 shadow rounded-lg p-6">
      <div className="flex justify-between items-center mb-6">
        <h2 className="text-xl font-bold text-slate-800 dark:text-white flex items-center">
          <Package className="mr-2" /> Product Catalog
        </h2>
        <div className="flex space-x-2">
          <button 
            onClick={onImportClick}
            className="px-4 py-2 bg-emerald-600 text-white rounded-md text-sm hover:bg-emerald-700 transition"
          >
            Import CSV
          </button>
          <button className="px-4 py-2 bg-blue-600 text-white rounded-md text-sm hover:bg-blue-700 transition flex items-center">
            <Plus className="w-4 h-4 mr-1" /> New Product
          </button>
        </div>
      </div>

      <div className="relative mb-6">
        <div className="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none">
          <Search className="h-5 w-5 text-slate-400" />
        </div>
        <input
          type="text"
          className="block w-full pl-10 pr-3 py-2 border border-slate-300 dark:border-slate-700 rounded-md leading-5 bg-white dark:bg-slate-900 placeholder-slate-500 dark:placeholder-slate-400 text-slate-900 dark:text-white focus:outline-none focus:ring-blue-500 focus:border-blue-500 sm:text-sm"
          placeholder="Search by name, barcode, code, or Tamil name..."
          value={searchTerm}
          onChange={(e) => setSearchTerm(e.target.value)}
        />
      </div>

      <div className="overflow-x-auto">
        <table className="min-w-full divide-y divide-slate-200 dark:divide-slate-700">
          <thead className="bg-slate-50 dark:bg-slate-900">
            <tr>
              <th className="px-6 py-3 text-left text-xs font-medium text-slate-500 dark:text-slate-400 uppercase tracking-wider">Product</th>
              <th className="px-6 py-3 text-left text-xs font-medium text-slate-500 dark:text-slate-400 uppercase tracking-wider">Barcode</th>
              <th className="px-6 py-3 text-left text-xs font-medium text-slate-500 dark:text-slate-400 uppercase tracking-wider">Tamil Name</th>
              <th className="px-6 py-3 text-right text-xs font-medium text-slate-500 dark:text-slate-400 uppercase tracking-wider">Price (₹)</th>
            </tr>
          </thead>
          <tbody className="bg-white dark:bg-slate-800 divide-y divide-slate-200 dark:divide-slate-700">
            {isLoading ? (
              <tr><td colSpan={4} className="text-center py-4 text-slate-500">Loading...</td></tr>
            ) : products?.length === 0 ? (
              <tr><td colSpan={4} className="text-center py-4 text-slate-500">No products found.</td></tr>
            ) : (
              products?.map((p) => (
                <tr key={p.id} className="hover:bg-slate-50 dark:hover:bg-slate-700/50">
                  <td className="px-6 py-4 whitespace-nowrap text-sm font-medium text-slate-900 dark:text-white">
                    {p.name}
                    <div className="text-xs text-slate-500">{p.productCode}</div>
                  </td>
                  <td className="px-6 py-4 whitespace-nowrap text-sm text-slate-500 dark:text-slate-400">{p.primaryBarcode}</td>
                  <td className="px-6 py-4 whitespace-nowrap text-sm text-slate-500 dark:text-slate-400 font-tamil">{p.tamilName || '-'}</td>
                  <td className="px-6 py-4 whitespace-nowrap text-sm text-slate-900 dark:text-white text-right font-semibold">
                    {p.sellingPrice.toFixed(2)}
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
};
