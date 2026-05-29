import React, { useState, useEffect, useRef } from 'react';
import { 
  Upload, Download, Sparkles, CheckCircle, AlertTriangle, 
  PlusCircle, Check, X, FileSpreadsheet, RefreshCw, Layers, ShieldCheck
} from 'lucide-react';
import { api } from '../../../utils/api';

interface DraftItem {
  barcode: string;
  productName: string;
  productCode?: string;
  quantity: number;
  costPrice: number;
  existingCostPrice?: number | null;
  existingSellingPrice?: number | null;
  existingMrp?: number | null;
  mrp: number;
  sellingPrice: number;
  batchNumber: string;
  expiryDate: string | null;
  status: string;
  hasExpiry: boolean;
  remarks: string;
}

interface Rules {
  preventNegativeStock: boolean;
  mandatoryBatchTracking: boolean;
  rowLevelLocking: boolean;
}

export const AiInvoiceImport = () => {
  // Config & Rules
  const [rules, setRules] = useState<Rules>({
    preventNegativeStock: true,
    mandatoryBatchTracking: true,
    rowLevelLocking: true
  });
  
  // Extraction States
  const [file, setFile] = useState<File | null>(null);
  const [extracting, setExtracting] = useState(false);
  const [invoiceRef, setInvoiceRef] = useState<string>('');
  const [draftItems, setDraftItems] = useState<DraftItem[]>([]);
  
  // Interaction States
  const [importing, setImporting] = useState(false);
  const [importSuccess, setImportSuccess] = useState<string | null>(null);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);
  const [validationErrors, setValidationErrors] = useState<string[]>([]);

  const fileInputRef = useRef<HTMLInputElement>(null);

  // Fetch rules configuration
  const fetchRules = async () => {
    try {
      const res = await api.get('/api/settings/inventory-rules');
      setRules(res.data);
    } catch (err) {
      console.error('Failed to load inventory rules', err);
    }
  };

  useEffect(() => {
    fetchRules();
  }, []);

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    if (e.target.files && e.target.files.length > 0) {
      setFile(e.target.files[0]);
      setDraftItems([]);
      setImportSuccess(null);
      setErrorMsg(null);
      setValidationErrors([]);
    }
  };

  const handleUploadSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!file) return;

    try {
      setExtracting(true);
      setErrorMsg(null);
      setImportSuccess(null);
      setValidationErrors([]);

      const formData = new FormData();
      formData.append('file', file);

      const res = await api.post('/api/inventory/ai-extract', formData, {
        headers: { 'Content-Type': 'multipart/form-data' }
      });

      setInvoiceRef(res.data.invoiceReference);
      setDraftItems(res.data.items);
    } catch (err: any) {
      console.error('AI Extraction failed', err);
      setErrorMsg(err.response?.data?.message || err.message || 'AI invoice parsing failed.');
    } finally {
      setExtracting(false);
    }
  };

  const handleFieldChange = (index: number, field: keyof DraftItem, value: any) => {
    setDraftItems(prev => {
      const copy = [...prev];
      copy[index] = { ...copy[index], [field]: value };
      
      // Dynamic validation check
      if (field === 'hasExpiry' && value === false) {
        copy[index].batchNumber = '';
        copy[index].expiryDate = null;
      }
      return copy;
    });
  };

  // Convert draft state to CSV format
  const handleDownloadCsv = () => {
    if (draftItems.length === 0) return;
    
    const headers = ['Barcode', 'Product Name', 'Quantity', 'Cost Price', 'MRP', 'Selling Price', 'Batch Number', 'Expiry Date', 'Status'];
    const rows = draftItems.map(item => [
      item.barcode,
      item.productName,
      item.quantity,
      item.costPrice,
      item.mrp,
      item.sellingPrice,
      item.batchNumber || '',
      item.expiryDate || '',
      item.status
    ]);
    
    const csvContent = "data:text/csv;charset=utf-8," 
      + [headers.join(','), ...rows.map(e => e.map(val => `"${String(val).replace(/"/g, '""')}"`).join(','))].join('\n');
    
    const encodedUri = encodeURI(csvContent);
    const link = document.createElement("a");
    link.setAttribute("href", encodedUri);
    link.setAttribute("download", `invoice_draft_${invoiceRef}.csv`);
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
  };

  const handleImport = async () => {
    if (draftItems.length === 0) return;

    // Client-side validations based on rules
    const errors: string[] = [];
    draftItems.forEach((item, idx) => {
      if (item.hasExpiry && rules.mandatoryBatchTracking) {
        if (!item.batchNumber.trim()) {
          errors.push(`Row #${idx + 1} (${item.productName}): Batch Number is mandatory for perishable items.`);
        }
        if (!item.expiryDate) {
          errors.push(`Row #${idx + 1} (${item.productName}): Expiry Date is mandatory for perishable items.`);
        }
      }
      if (item.mrp <= 0) {
        errors.push(`Row #${idx + 1} (${item.productName}): MRP must be greater than zero.`);
      }
      if (item.sellingPrice <= 0) {
        errors.push(`Row #${idx + 1} (${item.productName}): Selling Price must be greater than zero.`);
      }
      if (item.sellingPrice > item.mrp) {
        errors.push(`Row #${idx + 1} (${item.productName}): Selling Price cannot exceed the MRP.`);
      }
    });

    if (errors.length > 0) {
      setValidationErrors(errors);
      setErrorMsg("Import blocked. Please correct the validation errors listed below.");
      return;
    }

    try {
      setImporting(true);
      setErrorMsg(null);
      setValidationErrors([]);

      const payload = {
        invoiceReference: invoiceRef,
        items: draftItems.map(item => ({
          barcode: item.barcode,
          productName: item.productName,
          costPrice: item.costPrice,
          mrp: Number(item.mrp),
          sellingPrice: Number(item.sellingPrice),
          quantity: item.quantity,
          batchNumber: item.batchNumber || null,
          expiryDate: item.expiryDate || null,
          hasExpiry: item.hasExpiry
        }))
      };

      const res = await api.post('/api/inventory/ai-import', payload);
      setImportSuccess(`Approval complete! Items imported. Stock Adjustment: ${res.data.adjustmentNumber} recorded successfully.`);
      setDraftItems([]);
      setFile(null);
    } catch (err: any) {
      console.error('Import process failed', err);
      setErrorMsg(err.response?.data?.message || err.message || 'Import failed.');
    } finally {
      setImporting(false);
    }
  };

  const getStatusBadge = (status: string) => {
    if (status === 'NEW') return <span className="px-2 py-0.5 rounded text-[10px] font-black bg-blue-100 text-blue-800">NEW</span>;
    if (status === 'DISCREPANCY') return <span className="px-2 py-0.5 rounded text-[10px] font-black bg-amber-100 text-amber-800">CONFLICT</span>;
    return <span className="px-2 py-0.5 rounded text-[10px] font-black bg-green-100 text-green-800">OK</span>;
  };

  return (
    <div className="max-w-7xl mx-auto p-6 bg-slate-50 min-h-screen">
      {/* Page Header */}
      <div className="mb-6 border-b pb-4">
        <h2 className="text-3xl font-black text-slate-800 flex items-center">
          <Sparkles className="mr-3 text-indigo-650 w-8 h-8 animate-pulse" /> AI Invoice Procurement Draft
        </h2>
        <p className="text-slate-500 text-sm font-medium mt-1">
          Upload PDF purchase invoices to extract data. Review prices, set batches, adjust MRP/Selling rates, and save updates to catalog master.
        </p>
      </div>

      {rules.mandatoryBatchTracking && (
        <div className="mb-6 p-3 bg-indigo-50 text-indigo-850 rounded-xl border border-indigo-150 flex items-center space-x-2 text-xs font-semibold">
          <ShieldCheck className="w-4 h-4 text-indigo-650 shrink-0" />
          <span>Active Policy: Perishable Batch & Expiry tracking is strictly enforced. Empty batch details will block imports.</span>
        </div>
      )}

      {/* Upload Zone */}
      {draftItems.length === 0 && (
        <div className="bg-white p-8 rounded-2xl shadow-lg border border-slate-100 max-w-2xl mx-auto mt-8">
          <form onSubmit={handleUploadSubmit} className="space-y-6 text-center">
            <div 
              onClick={() => fileInputRef.current?.click()}
              className="border-2 border-dashed border-slate-350 hover:border-indigo-600 rounded-2xl p-10 cursor-pointer bg-slate-50 hover:bg-indigo-50 hover:bg-opacity-20 transition flex flex-col items-center justify-center space-y-4"
            >
              <Upload className="w-12 h-12 text-slate-400" />
              <div>
                <span className="text-sm font-bold text-slate-700 block">Select PDF invoice or delivery note</span>
                <span className="text-xs text-slate-400 mt-1 block">Supports readable standard formatted invoices</span>
              </div>
              <input
                type="file"
                ref={fileInputRef}
                onChange={handleFileChange}
                accept="application/pdf"
                className="hidden"
              />
            </div>

            {file && (
              <div className="p-3 bg-indigo-50 text-indigo-700 rounded-xl text-xs font-bold border border-indigo-100 flex items-center justify-between">
                <span className="truncate">Selected: {file.name}</span>
                <button type="button" onClick={() => setFile(null)} className="text-indigo-400 hover:text-indigo-600">
                  <X className="w-4 h-4" />
                </button>
              </div>
            )}

            {errorMsg && (
              <div className="p-4 bg-red-50 text-red-700 rounded-xl text-xs font-bold border border-red-100 text-left flex items-start space-x-2">
                <AlertTriangle className="w-4 h-4 text-red-500 shrink-0 mt-0.5" />
                <span>{errorMsg}</span>
              </div>
            )}

            {importSuccess && (
              <div className="p-4 bg-green-50 text-green-700 rounded-xl text-xs font-bold border border-green-100 text-left flex items-start space-x-2">
                <CheckCircle className="w-4 h-4 text-green-500 shrink-0 mt-0.5" />
                <span>{importSuccess}</span>
              </div>
            )}

            <button
              type="submit"
              disabled={!file || extracting}
              className="w-full py-3 bg-indigo-600 text-white font-bold rounded-xl shadow-lg hover:bg-indigo-700 transition disabled:opacity-50 flex items-center justify-center text-sm"
            >
              {extracting ? (
                <>
                  <RefreshCw className="w-4 h-4 mr-2 animate-spin" /> Extracting invoice parameters...
                </>
              ) : (
                <>
                  <Sparkles className="w-4 h-4 mr-2" /> Upload & Parse PDF
                </>
              )}
            </button>
          </form>
        </div>
      )}

      {/* Draft review grid */}
      {draftItems.length > 0 && (
        <div className="space-y-6 animate-fadeIn">
          {/* Summary Actions Card */}
          <div className="bg-white p-5 rounded-2xl shadow-md border border-slate-100 flex flex-col md:flex-row justify-between items-start md:items-center gap-4">
            <div>
              <h3 className="text-lg font-bold text-slate-800">Procurement Draft Review</h3>
              <p className="text-xs text-slate-500">Ref: {invoiceRef} | {draftItems.length} extracted items ready to approve.</p>
            </div>
            <div className="flex gap-3 w-full md:w-auto">
              <button
                onClick={handleDownloadCsv}
                className="flex-1 md:flex-none px-4 py-2 border border-slate-350 hover:bg-slate-50 text-slate-700 font-bold rounded-lg text-xs flex items-center justify-center transition"
              >
                <FileSpreadsheet className="w-4 h-4 mr-1.5" /> Export Draft CSV
              </button>
              <button
                onClick={handleImport}
                disabled={importing}
                className="flex-1 md:flex-none px-5 py-2.5 bg-gradient-to-r from-indigo-600 to-blue-600 text-white font-bold rounded-lg text-xs flex items-center justify-center shadow hover:from-indigo-700 hover:to-blue-700 transition"
              >
                {importing ? (
                  <>
                    <RefreshCw className="w-4 h-4 mr-1.5 animate-spin" /> Processing Import...
                  </>
                ) : (
                  <>
                    <CheckCircle className="w-4 h-4 mr-1.5" /> Approve & Import to Stock
                  </>
                )}
              </button>
            </div>
          </div>

          {/* Validation Alerts Block */}
          {errorMsg && (
            <div className="p-4 bg-red-50 text-red-700 rounded-xl border border-red-100">
              <div className="flex items-start space-x-2 mb-2 font-bold text-xs">
                <AlertTriangle className="w-4 h-4 text-red-500 shrink-0 mt-0.5" />
                <span>{errorMsg}</span>
              </div>
              {validationErrors.length > 0 && (
                <ul className="list-disc pl-6 text-[11px] space-y-1 text-red-650">
                  {validationErrors.map((err, i) => (
                    <li key={i}>{err}</li>
                  ))}
                </ul>
              )}
            </div>
          )}

          {/* Table Spreadsheet */}
          <div className="bg-white rounded-2xl shadow-lg border border-slate-200 overflow-hidden">
            <div className="overflow-x-auto">
              <table className="w-full text-left border-collapse text-xs table-fixed">
                <thead className="bg-slate-50 font-bold text-slate-700 border-b sticky top-0">
                  <tr>
                    <th className="p-3 w-16 text-center">Status</th>
                    <th className="p-3 w-48">Product Description</th>
                    <th className="p-3 w-16 text-right">Qty</th>
                    <th className="p-3 w-24 text-center">Invoice Cost</th>
                    <th className="p-3 w-24 text-center">System Cost</th>
                    <th className="p-3 w-36 text-center">Cost Status</th>
                    <th className="p-3 w-12 text-center">Expiry?</th>
                    <th className="p-3 w-28">Batch Code</th>
                    <th className="p-3 w-36">Expiry Date</th>
                    <th className="p-3 w-28 text-center">MRP (Editable)</th>
                    <th className="p-3 w-28 text-center">Sell Price (Editable)</th>
                    <th className="p-3 w-44">Remarks</th>
                  </tr>
                </thead>
                <tbody>
                  {draftItems.map((item, idx) => {
                    const isPerishable = item.hasExpiry;
                    const hasBatchErr = isPerishable && rules.mandatoryBatchTracking && !item.batchNumber.trim();
                    const hasExpiryErr = isPerishable && rules.mandatoryBatchTracking && !item.expiryDate;

                    return (
                      <tr key={idx} className="border-b hover:bg-slate-50">
                        {/* Status Badge */}
                        <td className="p-3 text-center">{getStatusBadge(item.status)}</td>
                        
                        {/* Name & Barcode */}
                        <td className="p-3">
                          <div className="font-bold text-slate-800 truncate" title={item.productName}>
                            {item.productName}
                          </div>
                          <div className="text-[10px] text-slate-400 font-mono mt-0.5">{item.barcode}</div>
                        </td>
                        
                        {/* Quantity */}
                        <td className="p-3 text-right font-bold text-slate-800">{item.quantity}</td>
                        
                        {/* Invoice Cost */}
                        <td className="p-3 text-center font-bold text-slate-800">
                          ₹{item.costPrice.toFixed(2)}
                        </td>

                        {/* System Cost */}
                        <td className="p-3 text-center text-slate-600 font-medium">
                          {item.existingCostPrice !== null && item.existingCostPrice !== undefined ? (
                            `₹${item.existingCostPrice.toFixed(2)}`
                          ) : (
                            <span className="text-slate-400 font-bold">New Product</span>
                          )}
                        </td>

                        {/* Cost Status */}
                        <td className="p-3 text-center">
                          {item.existingCostPrice !== null && item.existingCostPrice !== undefined && item.costPrice === item.existingCostPrice ? (
                            <span className="px-2 py-1 rounded-full text-[10px] font-bold bg-green-50 text-green-700 border border-green-150">
                              Cost is matching
                            </span>
                          ) : (
                            <span className="px-2 py-1 rounded-full text-[10px] font-bold bg-rose-50 text-rose-700 border border-rose-150">
                              Cost Diff : Action required
                            </span>
                          )}
                        </td>

                        {/* Perishable Expiry Toggle */}
                        <td className="p-3 text-center">
                          <input
                            type="checkbox"
                            checked={item.hasExpiry}
                            onChange={(e) => handleFieldChange(idx, 'hasExpiry', e.target.checked)}
                            className="w-4 h-4 rounded text-indigo-600 border-slate-350 focus:ring-indigo-500"
                          />
                        </td>
                        
                        {/* Batch Input */}
                        <td className="p-3">
                          <input
                            type="text"
                            value={item.batchNumber}
                            disabled={!isPerishable}
                            placeholder={isPerishable ? 'Required' : 'Disabled'}
                            onChange={(e) => handleFieldChange(idx, 'batchNumber', e.target.value)}
                            className={`w-full p-1.5 border rounded outline-none text-xs focus:ring-1 focus:ring-indigo-500 ${
                              hasBatchErr ? 'border-red-500 bg-red-50 focus:ring-red-500' : 'border-slate-300'
                            }`}
                          />
                        </td>

                        {/* Expiry Date input */}
                        <td className="p-3">
                          <input
                            type="date"
                            value={item.expiryDate || ''}
                            disabled={!isPerishable}
                            onChange={(e) => handleFieldChange(idx, 'expiryDate', e.target.value || null)}
                            className={`w-full p-1 border rounded outline-none text-xs focus:ring-1 focus:ring-indigo-500 ${
                              hasExpiryErr ? 'border-red-500 bg-red-50 focus:ring-red-500' : 'border-slate-300'
                            }`}
                          />
                        </td>
                        
                        {/* MRP Input */}
                        <td className="p-3">
                          <div className="flex flex-col items-center">
                            <input
                              type="number"
                              step="0.01"
                              value={item.mrp || ''}
                              onChange={(e) => handleFieldChange(idx, 'mrp', Number(e.target.value))}
                              className="w-full p-1.5 border border-slate-300 rounded outline-none text-center font-bold focus:ring-1 focus:ring-indigo-500"
                            />
                            {item.existingMrp !== null && (
                              <span className="text-[10px] text-slate-400 mt-0.5">Prev: ₹{item.existingMrp?.toFixed(2)}</span>
                            )}
                          </div>
                        </td>
                        
                        {/* Selling Price Input */}
                        <td className="p-3">
                          <div className="flex flex-col items-center">
                            <input
                              type="number"
                              step="0.01"
                              value={item.sellingPrice || ''}
                              onChange={(e) => handleFieldChange(idx, 'sellingPrice', Number(e.target.value))}
                              className="w-full p-1.5 border border-slate-300 rounded outline-none text-center font-bold focus:ring-1 focus:ring-indigo-500"
                            />
                            {item.existingSellingPrice !== null && (
                              <span className="text-[10px] text-slate-400 mt-0.5">Prev: ₹{item.existingSellingPrice?.toFixed(2)}</span>
                            )}
                          </div>
                        </td>
                        
                        {/* Remarks */}
                        <td className="p-3 text-slate-500">
                          <div className="truncate max-w-[160px]" title={item.remarks}>
                            {item.remarks}
                          </div>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
