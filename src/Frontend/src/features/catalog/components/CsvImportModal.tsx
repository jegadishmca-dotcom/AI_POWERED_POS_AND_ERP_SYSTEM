import React, { useState, useRef } from 'react';
import { UploadCloud, X, Loader2, AlertCircle, CheckCircle2 } from 'lucide-react';
import { importCsv } from '../api/catalog.api';
import { useQueryClient } from '@tanstack/react-query';

export const CsvImportModal = ({ isOpen, onClose }: { isOpen: boolean; onClose: () => void }) => {
  const [file, setFile] = useState<File | null>(null);
  const [isUploading, setIsUploading] = useState(false);
  const [result, setResult] = useState<any>(null);
  const [progress, setProgress] = useState<{ processedRows: number; totalRows: number; importedCount: number; failedCount: number; percent: number } | null>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);
  const queryClient = useQueryClient();

  if (!isOpen) return null;

  const handleUpload = async () => {
    if (!file) return;
    setIsUploading(true);
    setResult(null);
    const estimatedTotal = Math.max(100, Math.round(file.size / 110));
    setProgress({ processedRows: 0, totalRows: estimatedTotal, importedCount: 0, failedCount: 0, percent: 1 });

    const jobId = 'job_' + Math.random().toString(36).substring(2, 11) + '_' + Date.now();

    try {
      const res = await importCsv(file, jobId, (p) => {
        // Guarantee percent moves dynamically as rows process
        const calculatedPercent = p.totalRows > 0 ? Math.min(99, Math.round((p.processedRows / p.totalRows) * 100)) : 1;
        setProgress({
          ...p,
          totalRows: p.totalRows > 0 ? p.totalRows : estimatedTotal,
          percent: Math.max(1, calculatedPercent)
        });
      });
      setResult(res);
      setProgress(null);
      queryClient.invalidateQueries({ queryKey: ['products'] });
    } catch (e: any) {
      setResult({ error: 'Import failed or timed out: ' + (e.response?.data?.message || e.message || 'Server connection issue. Please verify file format and try again.') });
      setProgress(null);
    } finally {
      setIsUploading(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/50 backdrop-blur-sm">
      <div className="bg-white dark:bg-slate-800 rounded-xl shadow-xl max-w-lg w-full p-6 relative">
        <button onClick={onClose} disabled={isUploading} className="absolute right-4 top-4 text-slate-400 hover:text-slate-600 dark:hover:text-white disabled:opacity-30">
          <X className="w-5 h-5" />
        </button>
        
        <h2 className="text-xl font-bold mb-4 text-slate-900 dark:text-white">Import Products CSV</h2>
        
        {!result && !isUploading && (
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

        {isUploading && progress && (
          <div className="bg-slate-50 dark:bg-slate-900/40 border border-slate-200 dark:border-slate-700 p-5 rounded-xl space-y-4">
            <div className="flex justify-between items-center">
              <div className="flex items-center space-x-2">
                <Loader2 className="w-5 h-5 text-indigo-600 animate-spin" />
                <span className="font-bold text-sm text-slate-800 dark:text-slate-100">Importing Products...</span>
              </div>
              <span className="font-black text-indigo-600 text-base">{progress.percent}%</span>
            </div>

            {/* Progress Bar Track */}
            <div className="w-full bg-slate-200 dark:bg-slate-700 h-3 rounded-full overflow-hidden">
              <div 
                className="bg-indigo-600 h-full rounded-full transition-all duration-300 ease-out bg-gradient-to-r from-indigo-500 to-emerald-500" 
                style={{ width: `${Math.max(2, Math.min(100, progress.percent))}%` }}
              />
            </div>

            <div className="grid grid-cols-3 gap-2 text-center text-xs pt-1">
              <div className="bg-white dark:bg-slate-800 p-2 rounded-lg border border-slate-200 dark:border-slate-700">
                <p className="text-slate-400 font-bold">Processed</p>
                <p className="font-extrabold text-slate-800 dark:text-slate-200">{progress.processedRows.toLocaleString()}</p>
              </div>
              <div className="bg-emerald-50 dark:bg-emerald-900/30 p-2 rounded-lg border border-emerald-200 dark:border-emerald-800">
                <p className="text-emerald-600 dark:text-emerald-400 font-bold">Imported</p>
                <p className="font-extrabold text-emerald-700 dark:text-emerald-300">{progress.importedCount.toLocaleString()}</p>
              </div>
              <div className="bg-amber-50 dark:bg-amber-900/30 p-2 rounded-lg border border-amber-200 dark:border-amber-800">
                <p className="text-amber-600 dark:text-amber-400 font-bold">Failed</p>
                <p className="font-extrabold text-amber-700 dark:text-amber-300">{progress.failedCount.toLocaleString()}</p>
              </div>
            </div>
          </div>
        )}

        {result && !result.error && (
          <div className="bg-emerald-50 dark:bg-emerald-900/20 border border-emerald-200 dark:border-emerald-800 p-4 rounded-lg">
            <h3 className="flex items-center text-emerald-800 dark:text-emerald-400 font-bold mb-2">
              <CheckCircle2 className="w-5 h-5 mr-2" /> Import Complete
            </h3>
            <p className="text-sm text-emerald-700 dark:text-emerald-300">
              Imported: <strong>{result.totalImported?.toLocaleString()}</strong> | Failed: <strong>{result.totalFailed?.toLocaleString()}</strong>
            </p>
            {result.errors?.length > 0 && (
              <ul className="mt-2 text-xs text-red-600 dark:text-red-300 max-h-32 overflow-y-auto bg-white/50 dark:bg-black/20 p-2 rounded border border-red-100 dark:border-red-900/30">
                {result.errors.map((err: string, i: number) => <li key={i} className="mb-1">{err}</li>)}
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
          <button onClick={onClose} disabled={isUploading} className="px-4 py-2 text-sm text-slate-600 dark:text-slate-300 hover:text-slate-900 disabled:opacity-40">
            {result && !result.error && result.totalFailed === 0 ? 'Close' : 'Cancel'}
          </button>
          {!(result && !result.error && result.totalFailed === 0) && (
            <button 
              onClick={handleUpload}
              disabled={!file || isUploading}
              className="px-4 py-2 bg-blue-600 text-white rounded-md text-sm hover:bg-blue-700 transition disabled:opacity-50 flex items-center"
            >
              {isUploading ? <Loader2 className="w-4 h-4 mr-2 animate-spin" /> : 'Upload CSV'}
            </button>
          )}
        </div>
      </div>
    </div>
  );
};
