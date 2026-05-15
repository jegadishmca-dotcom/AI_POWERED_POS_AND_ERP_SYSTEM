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
