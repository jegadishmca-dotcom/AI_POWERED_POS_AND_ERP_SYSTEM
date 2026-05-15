$frontendDir = "d:\JEGADISH\AI_POWERED_POS_AND_ERP_SYSTEM\src\Frontend"

New-Item -ItemType Directory -Force -Path "$frontendDir\src\features\catalog\types"
New-Item -ItemType Directory -Force -Path "$frontendDir\src\features\catalog\api"
New-Item -ItemType Directory -Force -Path "$frontendDir\src\features\catalog\components"
New-Item -ItemType Directory -Force -Path "$frontendDir\src\features\catalog\routes"

# Types
@"
export interface ProductSearchResult {
  id: string;
  productCode: string;
  name: string;
  tamilName?: string;
  sellingPrice: number;
  primaryBarcode: string;
}

export interface ImportResult {
  totalImported: number;
  totalFailed: number;
  errors: string[];
}
"@ | Out-File -FilePath "$frontendDir\src\features\catalog\types\index.ts" -Encoding utf8

# API
@"
import { api } from '@/utils/api';
import { ProductSearchResult, ImportResult } from '../types';

export const searchProducts = async (q: string, limit: number = 20): Promise<ProductSearchResult[]> => {
  const { data } = await api.get('/api/catalog/search', { params: { q, limit } });
  return data;
};

export const importCsv = async (file: File): Promise<ImportResult> => {
  const formData = new FormData();
  formData.append('file', file);
  
  const { data } = await api.post('/api/catalog/import', formData, {
    headers: { 'Content-Type': 'multipart/form-data' },
  });
  return data;
};
"@ | Out-File -FilePath "$frontendDir\src\features\catalog\api\catalog.api.ts" -Encoding utf8

# Product List Component
@"
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
"@ | Out-File -FilePath "$frontendDir\src\features\catalog\components\ProductList.tsx" -Encoding utf8

# CSV Import Modal Component
@"
import React, { useState, useRef } from 'react';
import { UploadCloud, X, Loader2, AlertCircle, CheckCircle2 } from 'lucide-react';
import { importCsv } from '../api/catalog.api';
import { useQueryClient } from '@tanstack/react-query';

export const CsvImportModal = ({ isOpen, onClose }: { isOpen: boolean; onClose: () => void }) => {
  const [file, setFile] = useState<File | null>(null);
  const [isUploading, setIsUploading] = useState(false);
  const [result, setResult] = useState<any>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);
  const queryClient = useQueryClient();

  if (!isOpen) return null;

  const handleUpload = async () => {
    if (!file) return;
    setIsUploading(true);
    setResult(null);
    try {
      const res = await importCsv(file);
      setResult(res);
      queryClient.invalidateQueries({ queryKey: ['products'] });
    } catch (e: any) {
      setResult({ error: 'Upload failed: ' + (e.message || 'Unknown error') });
    } finally {
      setIsUploading(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/50 backdrop-blur-sm">
      <div className="bg-white dark:bg-slate-800 rounded-xl shadow-xl max-w-lg w-full p-6 relative">
        <button onClick={onClose} className="absolute right-4 top-4 text-slate-400 hover:text-slate-600 dark:hover:text-white">
          <X className="w-5 h-5" />
        </button>
        
        <h2 className="text-xl font-bold mb-4 text-slate-900 dark:text-white">Import Products CSV</h2>
        
        {!result && (
          <div className="border-2 border-dashed border-slate-300 dark:border-slate-600 rounded-lg p-8 text-center">
            <UploadCloud className="mx-auto h-12 w-12 text-slate-400 mb-4" />
            <p className="text-sm text-slate-600 dark:text-slate-400 mb-4">
              {file ? file.name : "Drag and drop your CSV here, or click to browse."}
            </p>
            <input 
              type="file" 
              accept=".csv" 
              className="hidden" 
              ref={fileInputRef}
              onChange={(e) => setFile(e.target.files?.[0] || null)}
            />
            <button 
              onClick={() => fileInputRef.current?.click()}
              className="px-4 py-2 border border-slate-300 dark:border-slate-600 rounded-md shadow-sm text-sm font-medium text-slate-700 dark:text-slate-200 bg-white dark:bg-slate-700 hover:bg-slate-50 dark:hover:bg-slate-600"
            >
              Select File
            </button>
          </div>
        )}

        {result && !result.error && (
          <div className="bg-emerald-50 dark:bg-emerald-900/20 border border-emerald-200 dark:border-emerald-800 p-4 rounded-lg">
            <h3 className="flex items-center text-emerald-800 dark:text-emerald-400 font-bold mb-2">
              <CheckCircle2 className="w-5 h-5 mr-2" /> Import Complete
            </h3>
            <p className="text-sm text-emerald-700 dark:text-emerald-300">
              Imported: <strong>{result.totalImported}</strong> | Failed: <strong>{result.totalFailed}</strong>
            </p>
            {result.errors?.length > 0 && (
              <ul className="mt-2 text-xs text-red-600 dark:text-red-400 max-h-32 overflow-y-auto bg-white/50 p-2 rounded">
                {result.errors.map((err: string, i: number) => <li key={i}>{err}</li>)}
              </ul>
            )}
          </div>
        )}

        {result?.error && (
          <div className="bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800 p-4 rounded-lg">
            <h3 className="flex items-center text-red-800 dark:text-red-400 font-bold mb-2">
              <AlertCircle className="w-5 h-5 mr-2" /> Error
            </h3>
            <p className="text-sm text-red-700 dark:text-red-300">{result.error}</p>
          </div>
        )}

        <div className="mt-6 flex justify-end space-x-3">
          <button onClick={onClose} className="px-4 py-2 text-sm text-slate-600 dark:text-slate-300 hover:text-slate-900">Cancel</button>
          <button 
            onClick={handleUpload}
            disabled={!file || isUploading}
            className="px-4 py-2 bg-blue-600 text-white rounded-md text-sm hover:bg-blue-700 transition disabled:opacity-50 flex items-center"
          >
            {isUploading ? <Loader2 className="w-4 h-4 mr-2 animate-spin" /> : 'Upload CSV'}
          </button>
        </div>
      </div>
    </div>
  );
};
"@ | Out-File -FilePath "$frontendDir\src\features\catalog\components\CsvImportModal.tsx" -Encoding utf8

# Products Route Component
@"
import React, { useState } from 'react';
import { ProductList } from '../components/ProductList';
import { CsvImportModal } from '../components/CsvImportModal';

export const Products = () => {
  const [isImportModalOpen, setIsImportModalOpen] = useState(false);

  return (
    <div className="p-6 max-w-7xl mx-auto">
      <ProductList onImportClick={() => setIsImportModalOpen(true)} />
      <CsvImportModal isOpen={isImportModalOpen} onClose={() => setIsImportModalOpen(false)} />
    </div>
  );
};
"@ | Out-File -FilePath "$frontendDir\src\features\catalog\routes\Products.tsx" -Encoding utf8

Write-Host "Frontend Catalog Scaffolded"
