import React, { useState, useEffect, useRef } from 'react';
import { 
  Save, ShieldAlert, Plus, Trash2, CheckCircle, XCircle, Clock, Eye, 
  AlertCircle, FileSpreadsheet, PlusCircle, Search, ChevronDown, ChevronUp,
  Package, ArrowDownRight, ArrowUpRight, DollarSign, Layers, RefreshCw
} from 'lucide-react';
import { getStockAdjustments, createStockAdjustment, approveStockAdjustment, rejectStockAdjustment, StockAdjustment } from '../api/stockAdjustment.api';
import { searchProducts } from '../../catalog/api/catalog.api';
import { getProductBatches } from '../../pos/api/pos.api';
import { useAuthStore } from '../../auth/store/auth.store';
import { api } from '../../../utils/api';

export const StockAdjustmentForm = () => {
  const { user } = useAuthStore();
  const isManager = user?.role === 'Manager' || user?.role === 'Owner' || user?.role === 'Admin';

  const downloadTemplate = () => {
    const headers = ['ProductCode', 'Barcode', 'BatchNo', 'AdjustedQty', 'UnitCost'];
    const rows = [
      ['PROD-001', '8901030678918', 'B01', '-5', '75.00'],
      ['PROD-002', '2900000000002', 'B02', '10', '42.50']
    ];
    const csvContent = [headers.join(','), ...rows.map(r => r.join(','))].join('\n');
    const blob = new Blob([new Uint8Array([0xEF, 0xBB, 0xBF]), csvContent], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.setAttribute('download', 'Stock_Adjustment_Import_Template.csv');
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
  };
  
  // List/History state
  const [adjustments, setAdjustments] = useState<StockAdjustment[]>([]);
  const [loading, setLoading] = useState(false);
  const [selectedAdjustment, setSelectedAdjustment] = useState<StockAdjustment | null>(null);

  // Form builder state
  const [showNewForm, setShowNewForm] = useState(false);
  const [reason, setReason] = useState<'DAMAGE' | 'EXPIRED' | 'THEFT' | 'FOUND' | 'MARKET_PURCHASE'>('DAMAGE');
  const [isProtocolBannerCollapsed, setIsProtocolBannerCollapsed] = useState(false);

  // Top Eye-Level Quick-Add Entry Bar state
  const [quickSearchQuery, setQuickSearchQuery] = useState('');
  const [quickSearchResults, setQuickSearchResults] = useState<any[]>([]);
  const [quickSearchHighlightIndex, setQuickSearchHighlightIndex] = useState(0);
  const [quickSelectedProduct, setQuickSelectedProduct] = useState<any | null>(null);
  const [quickBatches, setQuickBatches] = useState<any[]>([]);
  const [quickBatchId, setQuickBatchId] = useState('');
  const [quickBatchNumber, setQuickBatchNumber] = useState('');
  const [quickCurrentStock, setQuickCurrentStock] = useState(0);
  const [quickUnitCost, setQuickUnitCost] = useState(0);
  const [quickQty, setQuickQty] = useState(-1);
  const searchInputRef = useRef<HTMLInputElement>(null);

  // Worksheet Items
  const [formItems, setFormItems] = useState<{
    productId: string;
    productName: string;
    productCode?: string;
    batchId: string;
    batchNumber: string;
    adjustedQuantity: number;
    unitCost: number;
    batches: any[];
    currentStock: number;
  }[]>([]);

  const fetchAdjustments = async () => {
    try {
      setLoading(true);
      const data = await getStockAdjustments();
      setAdjustments(data);
    } catch (error) {
      console.error('Failed to load adjustments logs', error);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchAdjustments();
  }, []);

  // Update default Quick Qty sign based on Reason selection (non-destructive to existing lines)
  const handleReasonChange = (newReason: 'DAMAGE' | 'EXPIRED' | 'THEFT' | 'FOUND' | 'MARKET_PURCHASE') => {
    setReason(newReason);
    if (newReason === 'FOUND' || newReason === 'MARKET_PURCHASE') {
      if (quickQty < 0) setQuickQty(Math.abs(quickQty) || 1);
    } else {
      if (quickQty > 0) setQuickQty(-Math.abs(quickQty) || -1);
    }
  };

  const handleQuickSearch = async (query: string) => {
    setQuickSearchQuery(query);
    if (!query.trim()) {
      setQuickSearchResults([]);
      setQuickSearchHighlightIndex(0);
      return;
    }
    try {
      const results = await searchProducts(query);
      setQuickSearchResults(results || []);
      setQuickSearchHighlightIndex(0);
    } catch (err) {
      console.error('Quick product search failed', err);
    }
  };

  const selectQuickProduct = async (product: any) => {
    setQuickSelectedProduct(product);
    setQuickSearchQuery(product.name);
    setQuickSearchResults([]);
    const defaultCost = product.costPrice || product.sellingPrice * 0.7;
    setQuickUnitCost(defaultCost);

    try {
      const batchesList = await getProductBatches(product.id);
      setQuickBatches(batchesList || []);
      if (batchesList && batchesList.length > 0) {
        setQuickBatchId(batchesList[0].id);
        setQuickBatchNumber(batchesList[0].batchNumber);
        setQuickCurrentStock(batchesList[0].currentStock);
        setQuickUnitCost(batchesList[0].costPrice || defaultCost);
      } else {
        setQuickBatchId('');
        setQuickBatchNumber('NO BATCH');
        setQuickCurrentStock(0);
      }
    } catch (err) {
      console.error('Failed to load product batches for quick selection', err);
    }
  };

  const handleQuickBatchChange = (batchIdVal: string) => {
    if (batchIdVal === '__custom__') {
      setQuickBatchId('');
      setQuickBatchNumber('');
      setQuickCurrentStock(0);
      return;
    }
    const selected = quickBatches.find(b => b.id === batchIdVal);
    if (selected) {
      setQuickBatchId(selected.id);
      setQuickBatchNumber(selected.batchNumber);
      setQuickCurrentStock(selected.currentStock);
      setQuickUnitCost(selected.costPrice || quickUnitCost);
    }
  };

  const scrollToQuickHighlightedItem = (itemIdx: number) => {
    setTimeout(() => {
      const itemEl = document.getElementById(`quick-search-item-${itemIdx}`);
      if (itemEl) {
        itemEl.scrollIntoView({ block: 'nearest', behavior: 'smooth' });
      }
    }, 15);
  };

  const handleQuickKeyDown = (e: React.KeyboardEvent<HTMLInputElement>) => {
    if (quickSearchResults.length === 0) return;

    if (e.key === 'ArrowDown') {
      e.preventDefault();
      const nextIdx = Math.min(quickSearchHighlightIndex + 1, quickSearchResults.length - 1);
      setQuickSearchHighlightIndex(nextIdx);
      scrollToQuickHighlightedItem(nextIdx);
    } else if (e.key === 'ArrowUp') {
      e.preventDefault();
      const prevIdx = Math.max(quickSearchHighlightIndex - 1, 0);
      setQuickSearchHighlightIndex(prevIdx);
      scrollToQuickHighlightedItem(prevIdx);
    } else if (e.key === 'Enter') {
      e.preventDefault();
      const selected = quickSearchResults[quickSearchHighlightIndex];
      if (selected) {
        selectQuickProduct(selected);
      }
    } else if (e.key === 'Escape') {
      e.preventDefault();
      setQuickSearchResults([]);
    }
  };

  const handleQuickAddLine = () => {
    if (!quickSelectedProduct) {
      alert('Please search and select a product from the dropdown first.');
      return;
    }
    if (quickQty === 0) {
      alert('Adjusted quantity cannot be zero.');
      return;
    }
    // Section 5 Rule: Check negative adjustment against current stock
    if (quickQty < 0 && Math.abs(quickQty) > quickCurrentStock) {
      alert(`Cannot write off ${Math.abs(quickQty)} units of "${quickSelectedProduct.name}". Current batch stock is only ${quickCurrentStock}.`);
      return;
    }

    const newLine = {
      productId: quickSelectedProduct.id,
      productName: quickSelectedProduct.name,
      productCode: quickSelectedProduct.productCode || '',
      batchId: quickBatchId,
      batchNumber: quickBatchNumber || 'NO BATCH',
      adjustedQuantity: quickQty,
      unitCost: quickUnitCost,
      batches: quickBatches,
      currentStock: quickCurrentStock,
    };

    setFormItems([...formItems, newLine]);

    // Reset quick entry bar for continuous scanning
    setQuickSearchQuery('');
    setQuickSearchResults([]);
    setQuickSelectedProduct(null);
    setQuickBatches([]);
    setQuickBatchId('');
    setQuickBatchNumber('');
    setQuickCurrentStock(0);
    setQuickUnitCost(0);
    setQuickQty(reason === 'FOUND' || reason === 'MARKET_PURCHASE' ? 1 : -1);

    if (searchInputRef.current) {
      searchInputRef.current.focus();
    }
  };

  // Live KPI Metric Calculations
  const totalShrinkageUnits = formItems.filter(i => i.adjustedQuantity < 0).reduce((acc, i) => acc + Math.abs(i.adjustedQuantity), 0);
  const totalShrinkageValue = formItems.filter(i => i.adjustedQuantity < 0).reduce((acc, i) => acc + (Math.abs(i.adjustedQuantity) * i.unitCost), 0);
  const totalSurplusUnits = formItems.filter(i => i.adjustedQuantity > 0).reduce((acc, i) => acc + i.adjustedQuantity, 0);
  const totalSurplusValue = formItems.filter(i => i.adjustedQuantity > 0).reduce((acc, i) => acc + (i.adjustedQuantity * i.unitCost), 0);
  const netValuationImpact = totalSurplusValue - totalShrinkageValue;

  const handleImportCsv = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

    const formData = new FormData();
    formData.append('file', file);

    try {
      setLoading(true);
      const response = await api.post('/api/inventory/stock-adjustment/parse-csv', formData, {
        headers: { 'Content-Type': 'multipart/form-data' }
      });
      const parsedRows = response.data;
      if (Array.isArray(parsedRows) && parsedRows.length > 0) {
        const mappedRows = parsedRows.map((r: any) => ({
          productId: r.productId,
          productName: r.productName,
          productCode: r.productCode || '',
          batchId: r.batchId === '00000000-0000-0000-0000-000000000000' ? '' : r.batchId,
          batchNumber: r.batchNumber,
          adjustedQuantity: r.adjustedQuantity,
          unitCost: r.unitCost,
          batches: r.batchId && r.batchId !== '00000000-0000-0000-0000-000000000000' ? [{
            id: r.batchId,
            batchNumber: r.batchNumber,
            currentStock: r.currentStock,
            costPrice: r.unitCost
          }] : [],
          currentStock: r.currentStock
        }));
        setFormItems(mappedRows);
        alert(`Successfully imported ${mappedRows.length} adjustment lines!`);
      } else {
        alert("No valid product adjustment lines found in CSV.");
      }
    } catch (err: any) {
      console.error("CSV import failed", err);
      alert("Failed to parse CSV file: " + (err.response?.data?.message || err.message));
    } finally {
      setLoading(false);
      e.target.value = '';
    }
  };

  const handleSubmitAdjustment = async () => {
    if (formItems.length === 0) {
      alert('Please add at least one product line.');
      return;
    }

    const validItems = formItems.filter((i) => i.productId !== '');
    if (validItems.length < formItems.length) {
      alert('Please select a product from the search dropdown for all lines.');
      return;
    }

    // Section 5 Validation check: ensure negative adjustments don't exceed current batch stock
    for (const item of validItems) {
      if (item.adjustedQuantity < 0 && Math.abs(item.adjustedQuantity) > item.currentStock) {
        alert(`Cannot reduce stock for "${item.productName}" (Batch: ${item.batchNumber}) by ${Math.abs(item.adjustedQuantity)} units. Current batch stock is only ${item.currentStock}.`);
        return;
      }
      if (item.adjustedQuantity === 0) {
        alert(`Quantity for "${item.productName}" cannot be zero.`);
        return;
      }
    }

    try {
      const payload = {
        storeId: null,
        reason: reason,
        items: validItems.map((i) => ({
          productId: i.productId,
          batchId: i.batchId || null,
          adjustedQuantity: i.adjustedQuantity,
          unitCost: i.unitCost,
        })),
      };

      await createStockAdjustment(payload);
      alert('Stock adjustment submitted for review successfully!');
      setFormItems([]);
      setShowNewForm(false);
      fetchAdjustments();
    } catch (err) {
      console.error('Submit adjustment failed', err);
      alert('Failed to submit adjustment.');
    }
  };

  const handleApprove = async (id: string) => {
    if (!window.confirm('Are you sure you want to approve this stock adjustment?')) return;
    try {
      await approveStockAdjustment(id);
      alert('Adjustment approved successfully.');
      if (selectedAdjustment?.id === id) setSelectedAdjustment(null);
      fetchAdjustments();
    } catch (err) {
      console.error('Approve failed', err);
      alert('Approval failed.');
    }
  };

  const handleReject = async (id: string) => {
    if (!window.confirm('Are you sure you want to reject this stock adjustment?')) return;
    try {
      await rejectStockAdjustment(id);
      alert('Adjustment rejected successfully.');
      if (selectedAdjustment?.id === id) setSelectedAdjustment(null);
      fetchAdjustments();
    } catch (err) {
      console.error('Reject failed', err);
      alert('Rejection failed.');
    }
  };

  const handleRemoveRow = (idx: number) => {
    setFormItems(formItems.filter((_, i) => i !== idx));
  };

  return (
    <div className="max-w-7xl mx-auto p-4 sm:p-6 space-y-6">
      
      {/* Page Header */}
      <div className="flex flex-col md:flex-row md:items-center justify-between gap-4 bg-white dark:bg-slate-800 p-5 rounded-2xl border border-slate-200 dark:border-slate-700 shadow-sm">
        <div>
          <div className="flex items-center gap-3">
            <div className="p-2.5 rounded-xl bg-indigo-600 text-white shadow-md shadow-indigo-600/30">
              <Layers className="w-6 h-6" />
            </div>
            <div>
              <h2 className="text-2xl font-black text-slate-800 dark:text-white flex items-center gap-2">
                Stock Adjustment Manager
                <span className="text-xs font-bold px-2 py-0.5 rounded bg-indigo-100 dark:bg-indigo-950 text-indigo-700 dark:text-indigo-300 border border-indigo-200 dark:border-indigo-800">
                  v1.3 Global Standard
                </span>
              </h2>
              <p className="text-xs text-slate-500 dark:text-slate-400 mt-0.5">
                Review inventory discrepancy logs or scan goods to record high-precision stock adjustments.
              </p>
            </div>
          </div>
        </div>

        <div className="flex items-center gap-3">
          {!showNewForm ? (
            <button 
              onClick={() => { setShowNewForm(true); }}
              className="px-5 py-2.5 bg-gradient-to-r from-indigo-600 to-violet-600 text-white rounded-xl shadow-md shadow-indigo-600/20 font-extrabold hover:from-indigo-700 hover:to-violet-700 transition flex items-center gap-2 text-sm"
            >
              <Plus className="w-4 h-4" /> Create New Adjustment Sheet
            </button>
          ) : (
            <button 
              onClick={() => setShowNewForm(false)}
              className="px-4 py-2.5 bg-slate-100 dark:bg-slate-700 text-slate-700 dark:text-slate-200 rounded-xl font-bold hover:bg-slate-200 dark:hover:bg-slate-600 transition text-sm flex items-center gap-2"
            >
              <Layers className="w-4 h-4" /> Back to History Logs
            </button>
          )}
        </div>
      </div>

      {/* Main Mode View */}
      {!showNewForm ? (
        /* History & Manager Review View */
        <div className="flex flex-col lg:flex-row gap-6">
          {/* Left Panel: Adjustments Log */}
          <div className="w-full lg:w-7/12 bg-white dark:bg-slate-800 rounded-2xl border border-slate-200 dark:border-slate-700 shadow-sm overflow-hidden">
            <div className="p-4 bg-slate-50 dark:bg-slate-900/50 border-b border-slate-200 dark:border-slate-700 font-bold text-slate-700 dark:text-slate-200 flex justify-between items-center text-sm">
              <span>Adjustment History Logs</span>
              <span className="text-xs bg-slate-200 dark:bg-slate-700 text-slate-600 dark:text-slate-300 px-2.5 py-0.5 rounded-full font-bold">
                {adjustments.length} logs
              </span>
            </div>
            
            <div className="divide-y divide-slate-100 dark:divide-slate-700/60 overflow-y-auto max-h-[600px]">
              {adjustments.length === 0 ? (
                <div className="p-12 text-center text-slate-400">
                  <FileSpreadsheet className="w-12 h-12 mx-auto mb-3 stroke-1 text-slate-300" />
                  <p className="font-bold text-slate-600 dark:text-slate-300">No adjustment sheets recorded</p>
                  <p className="text-xs text-slate-400 mt-1">Click "Create New Adjustment Sheet" to write off or add inventory.</p>
                </div>
              ) : (
                adjustments.map((a) => (
                  <div 
                    key={a.id} 
                    onClick={() => setSelectedAdjustment(a)}
                    className={`p-4 cursor-pointer hover:bg-indigo-50/40 dark:hover:bg-slate-700/50 transition flex justify-between items-center ${selectedAdjustment?.id === a.id ? 'bg-indigo-50/70 dark:bg-slate-700/80 border-l-4 border-indigo-600' : ''}`}
                  >
                    <div>
                      <div className="flex items-center gap-2">
                        <span className="font-black text-slate-800 dark:text-white text-sm">{a.adjustmentNumber}</span>
                        <span className={`text-[10px] font-black uppercase px-2.5 py-0.5 rounded-full ${
                          a.status === 'APPROVED' ? 'bg-emerald-100 text-emerald-800 dark:bg-emerald-950 dark:text-emerald-300' :
                          a.status === 'REJECTED' ? 'bg-rose-100 text-rose-800 dark:bg-rose-950 dark:text-rose-300' : 
                          'bg-amber-100 text-amber-800 dark:bg-amber-950 dark:text-amber-300'
                        }`}>
                          {a.status}
                        </span>
                      </div>
                      <p className="text-xs text-slate-400 mt-1">Date: {new Date(a.createdAt).toLocaleString()}</p>
                      <p className="text-xs text-slate-500 dark:text-slate-300 font-bold mt-1">
                        Reason: <span className="bg-slate-100 dark:bg-slate-700 px-2 py-0.5 rounded text-indigo-600 dark:text-indigo-400">{a.reason}</span>
                      </p>
                    </div>
                    
                    <div className="text-right">
                      <p className="text-xs font-black text-slate-700 dark:text-slate-200">{a.items.length} Line items</p>
                      <button className="text-indigo-600 dark:text-indigo-400 text-xs font-bold hover:underline flex items-center gap-1 mt-2 justify-end">
                        <Eye className="w-3.5 h-3.5" /> View Sheet
                      </button>
                    </div>
                  </div>
                ))
              )}
            </div>
          </div>

          {/* Right Panel: Selected Adjustment Detail */}
          <div className="w-full lg:w-5/12">
            {selectedAdjustment ? (
              <div className="bg-white dark:bg-slate-800 rounded-2xl border border-slate-200 dark:border-slate-700 shadow-sm p-6 flex flex-col justify-between min-h-[450px]">
                <div>
                  <div className="flex justify-between items-start border-b border-slate-100 dark:border-slate-700 pb-4 mb-4">
                    <div>
                      <h3 className="font-black text-xl text-slate-800 dark:text-white">{selectedAdjustment.adjustmentNumber}</h3>
                      <p className="text-xs text-slate-400 mt-0.5">Created: {new Date(selectedAdjustment.createdAt).toLocaleString()}</p>
                    </div>
                    <span className={`text-xs font-extrabold px-3 py-1 rounded-full ${
                      selectedAdjustment.status === 'APPROVED' ? 'bg-emerald-100 text-emerald-800 dark:bg-emerald-950 dark:text-emerald-300' :
                      selectedAdjustment.status === 'REJECTED' ? 'bg-rose-100 text-rose-800 dark:bg-rose-950 dark:text-rose-300' : 
                      'bg-amber-100 text-amber-800 dark:bg-amber-950 dark:text-amber-300'
                    }`}>
                      {selectedAdjustment.status}
                    </span>
                  </div>

                  <div className="mb-4">
                    <p className="text-xs font-bold text-slate-400 uppercase tracking-wider">Adjustment Reason</p>
                    <p className="font-bold text-slate-800 dark:text-slate-100 bg-slate-50 dark:bg-slate-900/60 p-2.5 rounded-xl border border-slate-100 dark:border-slate-700 text-sm mt-1">
                      {selectedAdjustment.reason}
                    </p>
                  </div>

                  {selectedAdjustment.approvedByName && (
                    <div className="mb-4">
                      <p className="text-xs font-bold text-slate-400 uppercase tracking-wider">Processed By</p>
                      <p className="font-semibold text-slate-800 dark:text-slate-200 mt-0.5 text-sm">{selectedAdjustment.approvedByName}</p>
                    </div>
                  )}

                  <div className="mb-6">
                    <p className="text-xs font-bold text-slate-400 uppercase tracking-wider mb-2">Line Item Details</p>
                    <div className="divide-y divide-slate-100 dark:divide-slate-700/60 border border-slate-200 dark:border-slate-700 rounded-xl overflow-hidden max-h-64 overflow-y-auto">
                      {selectedAdjustment.items.map((item, idx) => (
                        <div key={idx} className="p-3 bg-slate-50/40 dark:bg-slate-900/30 flex justify-between items-center text-xs">
                          <div>
                            <p className="font-bold text-slate-800 dark:text-white">{item.productName || 'Product'}</p>
                            <p className="text-[10px] text-slate-400 mt-0.5">Batch: {item.batchNumber || 'N/A'}</p>
                          </div>
                          <div className="text-right">
                            <span className={`font-black text-sm ${item.adjustedQuantity > 0 ? 'text-emerald-600 dark:text-emerald-400' : 'text-rose-600 dark:text-rose-400'}`}>
                              {item.adjustedQuantity > 0 ? `+${item.adjustedQuantity}` : item.adjustedQuantity}
                            </span>
                            <p className="text-[10px] text-slate-400">Unit Cost: ₹{item.unitCost.toFixed(2)}</p>
                          </div>
                        </div>
                      ))}
                    </div>
                  </div>
                </div>

                {/* Manager Actions */}
                {selectedAdjustment.status === 'PENDING' && (
                  <div className="border-t border-slate-100 dark:border-slate-700 pt-4">
                    {isManager ? (
                      <div className="flex gap-3">
                        <button 
                          onClick={() => handleReject(selectedAdjustment.id)}
                          className="flex-1 py-2.5 bg-rose-50 text-rose-600 border border-rose-200 dark:bg-rose-950/40 dark:border-rose-900 dark:text-rose-300 rounded-xl font-bold hover:bg-rose-100 text-xs flex items-center justify-center gap-2 transition"
                        >
                          <XCircle className="w-4 h-4" /> Reject Write-Off
                        </button>
                        <button 
                          onClick={() => handleApprove(selectedAdjustment.id)}
                          className="flex-1 py-2.5 bg-emerald-600 text-white rounded-xl font-bold hover:bg-emerald-700 shadow-md text-xs flex items-center justify-center gap-2 transition"
                        >
                          <CheckCircle className="w-4 h-4" /> Approve & Adjust
                        </button>
                      </div>
                    ) : (
                      <div className="bg-amber-50 text-amber-800 border-l-4 border-amber-500 p-3 rounded-xl text-xs font-semibold flex items-center gap-2">
                        <Clock className="w-4 h-4 shrink-0" /> Manager approval is required to process this adjustment sheet.
                      </div>
                    )}
                  </div>
                )}
              </div>
            ) : (
              <div className="bg-white dark:bg-slate-800 border-2 border-dashed border-slate-200 dark:border-slate-700 rounded-2xl p-8 text-center text-slate-400 flex flex-col justify-center items-center h-[350px]">
                <Eye className="w-12 h-12 mb-2 stroke-1 text-slate-300" />
                <p className="font-bold text-slate-600 dark:text-slate-300">Select an adjustment entry</p>
                <p className="text-xs text-slate-400 mt-1">Review its detailed adjustment lines, batch numbers, and manager status.</p>
              </div>
            )}
          </div>
        </div>
      ) : (
        /* Global AI ERP Standard Sheet Creation Form */
        <div className="space-y-5">
          
          {/* LAYER 1: Executive KPI Metrics Cards */}
          <div className="grid grid-cols-2 lg:grid-cols-4 gap-4">
            <div className="relative overflow-hidden bg-gradient-to-br from-white to-indigo-50/50 dark:from-slate-800 dark:to-indigo-950/30 p-4 rounded-2xl border border-indigo-100 dark:border-indigo-900/50 shadow-sm shadow-indigo-100/50 dark:shadow-indigo-950/30 flex items-center justify-between group hover:shadow-md hover:shadow-indigo-200/40 dark:hover:shadow-indigo-900/30 transition-all duration-300">
              <div className="absolute left-0 top-0 bottom-0 w-1 bg-gradient-to-b from-indigo-500 to-violet-500 rounded-l-2xl" />
              <div className="pl-2">
                <p className="text-[10px] font-extrabold uppercase tracking-wider text-indigo-500 dark:text-indigo-400">Total Products</p>
                <p className="text-2xl font-black text-slate-800 dark:text-white mt-1">{formItems.length}</p>
                <p className="text-[11px] font-semibold text-slate-400 mt-0.5">Lines in sheet</p>
              </div>
              <div className="p-3 bg-indigo-100/80 dark:bg-indigo-950/60 text-indigo-600 dark:text-indigo-400 rounded-xl group-hover:scale-110 transition-transform duration-300">
                <Package className="w-5 h-5" />
              </div>
            </div>

            <div className="relative overflow-hidden bg-gradient-to-br from-white to-rose-50/50 dark:from-slate-800 dark:to-rose-950/20 p-4 rounded-2xl border border-rose-100 dark:border-rose-900/40 shadow-sm shadow-rose-100/50 dark:shadow-rose-950/20 flex items-center justify-between group hover:shadow-md hover:shadow-rose-200/40 dark:hover:shadow-rose-900/20 transition-all duration-300">
              <div className="absolute left-0 top-0 bottom-0 w-1 bg-gradient-to-b from-rose-500 to-pink-500 rounded-l-2xl" />
              <div className="pl-2">
                <p className="text-[10px] font-extrabold uppercase tracking-wider text-rose-500">Shrinkage / Damage</p>
                <p className="text-2xl font-black text-rose-600 dark:text-rose-400 mt-1">-{totalShrinkageUnits} <span className="text-xs font-bold">pcs</span></p>
                <p className="text-[11px] font-bold text-rose-600 dark:text-rose-400 mt-0.5">-₹{totalShrinkageValue.toFixed(2)}</p>
              </div>
              <div className="p-3 bg-rose-100/80 dark:bg-rose-950/60 text-rose-600 dark:text-rose-400 rounded-xl group-hover:scale-110 transition-transform duration-300">
                <ArrowDownRight className="w-5 h-5" />
              </div>
            </div>

            <div className="relative overflow-hidden bg-gradient-to-br from-white to-emerald-50/50 dark:from-slate-800 dark:to-emerald-950/20 p-4 rounded-2xl border border-emerald-100 dark:border-emerald-900/40 shadow-sm shadow-emerald-100/50 dark:shadow-emerald-950/20 flex items-center justify-between group hover:shadow-md hover:shadow-emerald-200/40 dark:hover:shadow-emerald-900/20 transition-all duration-300">
              <div className="absolute left-0 top-0 bottom-0 w-1 bg-gradient-to-b from-emerald-500 to-teal-500 rounded-l-2xl" />
              <div className="pl-2">
                <p className="text-[10px] font-extrabold uppercase tracking-wider text-emerald-500">Surplus / Found</p>
                <p className="text-2xl font-black text-emerald-600 dark:text-emerald-400 mt-1">+{totalSurplusUnits} <span className="text-xs font-bold">pcs</span></p>
                <p className="text-[11px] font-bold text-emerald-600 dark:text-emerald-400 mt-0.5">+₹{totalSurplusValue.toFixed(2)}</p>
              </div>
              <div className="p-3 bg-emerald-100/80 dark:bg-emerald-950/60 text-emerald-600 dark:text-emerald-400 rounded-xl group-hover:scale-110 transition-transform duration-300">
                <ArrowUpRight className="w-5 h-5" />
              </div>
            </div>

            <div className={`relative overflow-hidden p-4 rounded-2xl shadow-sm flex items-center justify-between group hover:shadow-md transition-all duration-300 ${
              netValuationImpact >= 0 
                ? 'bg-gradient-to-br from-white to-emerald-50/40 dark:from-slate-800 dark:to-emerald-950/20 border border-emerald-100 dark:border-emerald-900/40 shadow-emerald-100/50 dark:shadow-emerald-950/20 hover:shadow-emerald-200/40' 
                : 'bg-gradient-to-br from-white to-rose-50/40 dark:from-slate-800 dark:to-rose-950/20 border border-rose-100 dark:border-rose-900/40 shadow-rose-100/50 dark:shadow-rose-950/20 hover:shadow-rose-200/40'
            }`}>
              <div className={`absolute left-0 top-0 bottom-0 w-1 rounded-l-2xl ${netValuationImpact >= 0 ? 'bg-gradient-to-b from-emerald-500 to-cyan-500' : 'bg-gradient-to-b from-rose-500 to-orange-500'}`} />
              <div className="pl-2">
                <p className="text-[10px] font-extrabold uppercase tracking-wider text-slate-500 dark:text-slate-400">Net Financial Impact</p>
                <p className={`text-2xl font-black mt-1 ${netValuationImpact >= 0 ? 'text-emerald-600 dark:text-emerald-400' : 'text-rose-600 dark:text-rose-400'}`}>
                  {netValuationImpact >= 0 ? `+₹${netValuationImpact.toFixed(2)}` : `-₹${Math.abs(netValuationImpact).toFixed(2)}`}
                </p>
                <p className="text-[11px] font-semibold text-slate-400 mt-0.5">Ledger valuation variance</p>
              </div>
              <div className={`p-3 rounded-xl group-hover:scale-110 transition-transform duration-300 ${netValuationImpact >= 0 ? 'bg-emerald-100/80 dark:bg-emerald-950/60 text-emerald-600' : 'bg-rose-100/80 dark:bg-rose-950/60 text-rose-600'}`}>
                <DollarSign className="w-5 h-5" />
              </div>
            </div>
          </div>

          {/* LAYER 2: Protocol Banner & Global Reason Pills Controls — Distinct Tinted Panel */}
          <div className="bg-gradient-to-br from-slate-50 via-white to-violet-50/30 dark:from-slate-800/90 dark:via-slate-800 dark:to-violet-950/20 rounded-2xl border border-violet-100 dark:border-violet-900/40 shadow-sm p-5 space-y-4 backdrop-blur-sm">
            
            {/* Manager Review Protocol Banner (Expandable/Collapsible Alert) */}
            <div className="bg-amber-50 dark:bg-amber-950/40 border border-amber-200 dark:border-amber-900/60 rounded-xl p-3.5 text-amber-900 dark:text-amber-200 text-xs flex items-start justify-between transition-all">
              <div className="flex items-start gap-3">
                <ShieldAlert className="w-4 h-4 text-amber-600 dark:text-amber-400 mt-0.5 shrink-0" />
                <div>
                  <p className="font-extrabold text-amber-900 dark:text-amber-100 flex items-center gap-2">
                    Manager Review & Audit Protocol Required
                  </p>
                  {!isProtocolBannerCollapsed && (
                    <p className="text-[11px] text-amber-800 dark:text-amber-300 mt-1 leading-relaxed">
                      All adjustment entries are logged as <strong>PENDING</strong>. Inventory balances on the Stock Ledger will not update until an authorized Manager validates and approves this sheet.
                    </p>
                  )}
                </div>
              </div>
              <button 
                onClick={() => setIsProtocolBannerCollapsed(!isProtocolBannerCollapsed)}
                className="text-amber-700 dark:text-amber-400 hover:text-amber-900 p-1 rounded transition"
                title={isProtocolBannerCollapsed ? "Expand Info" : "Collapse Info"}
              >
                {isProtocolBannerCollapsed ? <ChevronDown className="w-4 h-4" /> : <ChevronUp className="w-4 h-4" />}
              </button>
            </div>

            {/* Reason Code Selector Pills & Top Action Toolbar */}
            <div className="flex flex-col lg:flex-row items-start lg:items-center justify-between gap-4 pt-1">
              
              {/* Reason Pills */}
              <div>
                <label className="block text-[11px] font-extrabold text-slate-400 uppercase tracking-wider mb-2">
                  Select Sheet Reason Code (Global Scope)
                </label>
                <div className="flex flex-wrap items-center gap-2">
                  {[
                    { id: 'DAMAGE', label: 'Damage / Broken', colorClass: 'bg-rose-600 text-white shadow-rose-600/30' },
                    { id: 'EXPIRED', label: 'Expired Stock', colorClass: 'bg-orange-600 text-white shadow-orange-600/30' },
                    { id: 'THEFT', label: 'Shrinkage / Theft', colorClass: 'bg-purple-600 text-white shadow-purple-600/30' },
                    { id: 'FOUND', label: 'Surplus / Found', colorClass: 'bg-emerald-600 text-white shadow-emerald-600/30' },
                    { id: 'MARKET_PURCHASE', label: 'Market Purchase', colorClass: 'bg-blue-600 text-white shadow-blue-600/30' },
                  ].map(p => {
                    const isSelected = reason === p.id;
                    return (
                      <button
                        key={p.id}
                        onClick={() => handleReasonChange(p.id as any)}
                        className={`px-3 py-1.5 rounded-xl text-xs font-black transition-all ${
                          isSelected 
                            ? `${p.colorClass} shadow-md scale-105` 
                            : 'bg-slate-100 dark:bg-slate-700/60 text-slate-600 dark:text-slate-300 hover:bg-slate-200 dark:hover:bg-slate-700'
                        }`}
                      >
                        {p.label}
                      </button>
                    );
                  })}
                </div>
              </div>

              {/* Action Toolbar */}
              <div className="flex items-center gap-3 self-end lg:self-center">
                <button 
                  onClick={downloadTemplate}
                  className="px-3 py-2 bg-slate-100 dark:bg-slate-700 text-slate-600 dark:text-slate-200 rounded-xl font-bold text-xs hover:bg-slate-200 dark:hover:bg-slate-600 transition flex items-center gap-1.5"
                  title="Download CSV Template"
                >
                  <FileSpreadsheet className="w-3.5 h-3.5" /> Template
                </button>
                
                <label className="cursor-pointer px-3 py-2 bg-emerald-50 dark:bg-emerald-950/60 text-emerald-700 dark:text-emerald-300 border border-emerald-200 dark:border-emerald-800 rounded-xl font-bold text-xs hover:bg-emerald-100 transition flex items-center gap-1.5">
                  <FileSpreadsheet className="w-3.5 h-3.5" /> Import CSV
                  <input 
                    type="file" 
                    accept=".csv" 
                    className="hidden" 
                    onChange={handleImportCsv}
                  />
                </label>

                <button 
                  onClick={handleSubmitAdjustment}
                  className="px-5 py-2 bg-gradient-to-r from-indigo-600 to-violet-600 text-white rounded-xl shadow-md shadow-indigo-600/30 font-black text-xs hover:from-indigo-700 hover:to-violet-700 transition flex items-center gap-1.5"
                >
                  <Save className="w-4 h-4" /> Submit for Approval
                </button>
              </div>
            </div>
          </div>

          {/* LAYER 3: Top Eye-Level Quick-Entry Command Bar (PRIMARY WORKING ZONE) */}
          <div className="bg-gradient-to-r from-slate-900 via-slate-900 to-indigo-950/80 text-white p-5 rounded-2xl shadow-xl shadow-indigo-950/20 border border-indigo-900/50 space-y-4 relative overflow-hidden">
            {/* Subtle glow accent */}
            <div className="absolute -top-20 -right-20 w-40 h-40 bg-indigo-600/10 rounded-full blur-3xl pointer-events-none" />
            <div className="absolute -bottom-10 -left-10 w-32 h-32 bg-violet-600/8 rounded-full blur-2xl pointer-events-none" />
            <div className="flex items-center justify-between relative z-10">
              <div className="text-[11px] text-indigo-400 font-extrabold uppercase tracking-widest flex items-center gap-2">
                <Search className="w-4 h-4 text-indigo-400" /> Fast Product Scanner & Quick-Add Entry Bar (Top Eye-Level)
              </div>
              <span className="text-[10px] text-slate-400 font-bold">
                Press <kbd className="bg-slate-800/80 px-1.5 py-0.5 rounded border border-slate-700 text-indigo-300">↓ / ↑</kbd> to navigate, <kbd className="bg-slate-800/80 px-1.5 py-0.5 rounded border border-slate-700 text-indigo-300">Enter</kbd> to select
              </span>
            </div>

            <div className="grid grid-cols-1 md:grid-cols-12 gap-3 items-end relative z-10">
              
              {/* Product Search & Dropdown Input (6 cols) */}
              <div className="md:col-span-5 relative">
                <label className="block text-[10px] font-extrabold uppercase tracking-wider text-slate-400 mb-1">
                  1. Search Product Name or Barcode
                </label>
                <div className="relative">
                  <Search className="absolute left-3.5 top-3 text-slate-400 w-4 h-4" />
                  <input
                    ref={searchInputRef}
                    type="text"
                    placeholder="Type name or scan barcode..."
                    className="w-full pl-10 pr-9 py-2.5 bg-slate-950 border border-slate-700 rounded-xl text-xs font-bold text-white outline-none focus:ring-2 focus:ring-indigo-500 focus:border-indigo-500 transition placeholder:text-slate-500"
                    value={quickSearchQuery}
                    onChange={(e) => handleQuickSearch(e.target.value)}
                    onKeyDown={handleQuickKeyDown}
                  />
                  {quickSearchQuery && (
                    <button
                      onClick={() => { setQuickSearchQuery(''); setQuickSearchResults([]); setQuickSelectedProduct(null); }}
                      className="absolute right-3 top-3 text-slate-400 hover:text-white text-xs font-bold"
                    >
                      ✕
                    </button>
                  )}
                </div>

                {/* Live Search Overlay Dropdown with Scroll Sync */}
                {quickSearchResults.length > 0 && (
                  <div className="absolute left-0 right-0 mt-1 bg-slate-900 border border-slate-700 rounded-xl shadow-2xl z-50 max-h-52 overflow-y-auto divide-y divide-slate-800 custom-scrollbar">
                    {quickSearchResults.map((p, pIdx) => {
                      const isHighlighted = pIdx === quickSearchHighlightIndex;
                      return (
                        <div
                          key={p.id}
                          id={`quick-search-item-${pIdx}`}
                          onClick={() => selectQuickProduct(p)}
                          onMouseEnter={() => setQuickSearchHighlightIndex(pIdx)}
                          className={`px-4 py-2.5 cursor-pointer flex justify-between items-center text-xs transition-colors ${
                            isHighlighted ? 'bg-indigo-600 text-white font-bold' : 'hover:bg-slate-800 text-slate-200'
                          }`}
                        >
                          <div>
                            <p className="font-bold">{p.name}</p>
                            <p className={`text-[10px] ${isHighlighted ? 'text-indigo-200' : 'text-slate-400'}`}>
                              Code: {p.productCode} | Barcode: {p.barcode || 'N/A'}
                            </p>
                          </div>
                          <span className={`font-black ${isHighlighted ? 'text-white' : 'text-indigo-400'}`}>
                            ₹{(p.costPrice || p.sellingPrice * 0.7).toFixed(2)}
                          </span>
                        </div>
                      );
                    })}
                  </div>
                )}
              </div>

              {/* Batch Selector Dropdown (3 cols) */}
              <div className="md:col-span-3">
                <label className="block text-[10px] font-extrabold uppercase tracking-wider text-slate-400 mb-1">
                  2. Select Batch
                </label>
                {quickBatches.length > 0 ? (
                  <select
                    className="w-full p-2.5 bg-slate-950 border border-slate-700 rounded-xl text-xs font-bold text-white outline-none focus:ring-2 focus:ring-indigo-500"
                    value={quickBatchId || (quickBatchNumber ? '__custom__' : '')}
                    onChange={(e) => handleQuickBatchChange(e.target.value)}
                  >
                    {quickBatches.map(b => (
                      <option key={b.id} value={b.id}>
                        {b.batchNumber} {b.expiryDate ? `(Exp: ${b.expiryDate.substring(0, 10)})` : ''} [Stock: {b.currentStock}]
                      </option>
                    ))}
                    <option value="__custom__">+ Custom Batch...</option>
                  </select>
                ) : (
                  <input
                    type="text"
                    placeholder="Batch No (e.g. BATCH-01)"
                    className="w-full p-2.5 bg-slate-950 border border-slate-700 rounded-xl text-xs font-bold text-white outline-none focus:ring-2 focus:ring-indigo-500 placeholder:text-slate-500"
                    value={quickBatchNumber}
                    onChange={(e) => { setQuickBatchNumber(e.target.value); setQuickBatchId(''); }}
                  />
                )}
              </div>

              {/* Delta Quantity Stepper (2 cols) */}
              <div className="md:col-span-2">
                <label className="block text-[10px] font-extrabold uppercase tracking-wider text-slate-400 mb-1">
                  3. Adjusted Qty
                </label>
                <div className="flex items-center">
                  <button
                    type="button"
                    onClick={() => setQuickQty(prev => prev - 1)}
                    className="px-3 py-2.5 bg-slate-800 hover:bg-slate-700 text-rose-400 rounded-l-xl font-black text-sm border border-r-0 border-slate-700"
                  >
                    -
                  </button>
                  <input
                    type="number"
                    className={`w-full py-2 text-center bg-slate-950 border-t border-b border-slate-700 text-xs font-black outline-none ${
                      quickQty < 0 ? 'text-rose-400' : 'text-emerald-400'
                    }`}
                    value={quickQty}
                    onChange={(e) => setQuickQty(parseInt(e.target.value) || 0)}
                  />
                  <button
                    type="button"
                    onClick={() => setQuickQty(prev => prev + 1)}
                    className="px-3 py-2.5 bg-slate-800 hover:bg-slate-700 text-emerald-400 rounded-r-xl font-black text-sm border border-l-0 border-slate-700"
                  >
                    +
                  </button>
                </div>
              </div>

              {/* Add Button (2 cols) */}
              <div className="md:col-span-2">
                <button
                  onClick={handleQuickAddLine}
                  className="w-full py-2.5 bg-gradient-to-r from-emerald-600 to-teal-600 text-white rounded-xl font-extrabold text-xs shadow-lg shadow-emerald-600/30 hover:from-emerald-500 hover:to-teal-500 hover:shadow-emerald-500/40 transition-all duration-200 flex items-center justify-center gap-1.5"
                >
                  <PlusCircle className="w-4 h-4" /> Add to Sheet
                </button>
              </div>

            </div>
          </div>

          {/* LAYER 4: Bounded Worksheet Table Container — Distinct Data Grid */}
          <div className="bg-gradient-to-b from-white to-slate-50/80 dark:from-slate-800 dark:to-slate-850 rounded-2xl border border-slate-200 dark:border-slate-700 shadow-sm overflow-hidden">
            <div className="flex justify-between items-center px-5 py-4 bg-gradient-to-r from-slate-50 to-white dark:from-slate-800 dark:to-slate-800 border-b border-slate-200 dark:border-slate-700">
              <h4 className="text-sm font-black text-slate-800 dark:text-white uppercase tracking-wider flex items-center gap-2">
                <div className="w-2 h-2 rounded-full bg-indigo-500 animate-pulse" />
                Adjustment Lines Worksheet
              </h4>
              <span className="text-xs font-extrabold px-3 py-1 rounded-full bg-indigo-50 dark:bg-indigo-950/50 text-indigo-600 dark:text-indigo-400 border border-indigo-100 dark:border-indigo-900/40">{formItems.length} Products Added</span>
            </div>

            <div className="max-h-[380px] overflow-y-auto">
              <table className="w-full border-collapse text-left">
                <thead className="sticky top-0 bg-gradient-to-r from-indigo-600 to-violet-600 text-white text-xs font-extrabold uppercase tracking-wider z-10 shadow-sm">
                  <tr>
                    <th className="px-5 py-3 font-extrabold">#</th>
                    <th className="px-4 py-3 font-extrabold">Product Name & Code</th>
                    <th className="px-4 py-3 font-extrabold">Batch Number</th>
                    <th className="px-4 py-3 text-center font-extrabold">Live Batch Stock</th>
                    <th className="px-4 py-3 text-center font-extrabold">Adjusted Qty (- / +)</th>
                    <th className="px-4 py-3 text-right font-extrabold">Unit Cost</th>
                    <th className="px-4 py-3 text-center font-extrabold">Action</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-100 dark:divide-slate-700/40 text-xs">
                  {formItems.length === 0 ? (
                    <tr>
                      <td colSpan={7} className="p-12 text-center text-slate-400 font-semibold bg-white dark:bg-slate-800">
                        No rows added to worksheet yet. Use the top Quick-Entry bar above to add items.
                      </td>
                    </tr>
                  ) : (
                    formItems.map((item, idx) => (
                      <tr key={idx} className={`transition-colors duration-150 ${
                        idx % 2 === 0 
                          ? 'bg-white dark:bg-slate-800' 
                          : 'bg-slate-50/70 dark:bg-slate-800/60'
                      } hover:bg-indigo-50/50 dark:hover:bg-indigo-950/20`}>
                        
                        {/* Row Number */}
                        <td className="px-5 py-3">
                          <span className="inline-flex items-center justify-center w-6 h-6 rounded-full bg-slate-100 dark:bg-slate-700 text-[10px] font-black text-slate-500 dark:text-slate-400">
                            {idx + 1}
                          </span>
                        </td>

                        {/* Product Info & Code Badge */}
                        <td className="px-4 py-3">
                          <p className="font-bold text-slate-800 dark:text-white">{item.productName}</p>
                          {item.productCode && (
                            <p className="text-[10px] text-slate-400 font-semibold mt-0.5">Code: {item.productCode}</p>
                          )}
                        </td>

                        {/* Inline Batch Selector */}
                        <td className="px-4 py-3">
                          {item.batches && item.batches.length > 0 ? (
                            <select
                              className="p-1.5 border border-slate-200 dark:border-slate-700 rounded-lg text-xs font-bold bg-white dark:bg-slate-900 text-slate-800 dark:text-slate-200 outline-none focus:ring-2 focus:ring-indigo-500"
                              value={item.batchId || (item.batchNumber ? '__custom__' : '')}
                              onChange={(e) => {
                                const selectedBatchId = e.target.value;
                                const updated = [...formItems];
                                const selectedB = item.batches.find(b => b.id === selectedBatchId);
                                if (selectedB) {
                                  updated[idx].batchId = selectedB.id;
                                  updated[idx].batchNumber = selectedB.batchNumber;
                                  updated[idx].currentStock = selectedB.currentStock;
                                  updated[idx].unitCost = selectedB.costPrice || updated[idx].unitCost;
                                }
                                setFormItems(updated);
                              }}
                            >
                              {item.batches.map(b => (
                                <option key={b.id} value={b.id}>
                                  {b.batchNumber} [Stock: {b.currentStock}]
                                </option>
                              ))}
                            </select>
                          ) : (
                            <span className="font-bold text-slate-600 dark:text-slate-300 bg-slate-100 dark:bg-slate-700 px-2 py-1 rounded">
                              {item.batchNumber || 'NO BATCH'}
                            </span>
                          )}
                        </td>

                        {/* Live Current Stock */}
                        <td className="px-4 py-3 text-center">
                          <span className={`font-black text-xs px-2 py-0.5 rounded ${
                            item.currentStock === 0 ? 'bg-amber-100 text-amber-800 dark:bg-amber-950 dark:text-amber-300' : 'text-slate-700 dark:text-slate-200'
                          }`}>
                            {item.currentStock} pcs
                          </span>
                        </td>

                        {/* Inline Adjusted Qty Stepper & Direct Typing with Section 5 Validation */}
                        <td className="px-4 py-3 text-center">
                          <div className="flex items-center justify-center space-x-1">
                            <button
                              type="button"
                              onClick={() => {
                                const newQty = item.adjustedQuantity - 1;
                                // Section 5 Validation check
                                if (newQty < 0 && Math.abs(newQty) > item.currentStock) {
                                  alert(`Cannot write off ${Math.abs(newQty)} units of "${item.productName}". Available stock in batch is only ${item.currentStock}.`);
                                  return;
                                }
                                const updated = [...formItems];
                                updated[idx].adjustedQuantity = newQty;
                                setFormItems(updated);
                              }}
                              className="w-6 h-6 bg-slate-100 dark:bg-slate-700 hover:bg-slate-200 dark:hover:bg-slate-600 text-slate-700 dark:text-slate-200 rounded font-black text-xs flex items-center justify-center"
                            >
                              -
                            </button>
                            <input
                              type="number"
                              className={`w-16 p-1 border rounded-lg text-center font-black text-xs outline-none focus:ring-2 focus:ring-indigo-500 ${
                                item.adjustedQuantity < 0 
                                  ? 'text-rose-600 dark:text-rose-400 bg-rose-50 dark:bg-rose-950/40 border-rose-200 dark:border-rose-900/50' 
                                  : 'text-emerald-600 dark:text-emerald-400 bg-emerald-50 dark:bg-emerald-950/40 border-emerald-200 dark:border-emerald-900/50'
                              }`}
                              value={item.adjustedQuantity}
                              onChange={(e) => {
                                const val = parseInt(e.target.value) || 0;
                                // Section 5 Validation check
                                if (val < 0 && Math.abs(val) > item.currentStock) {
                                  alert(`Cannot write off ${Math.abs(val)} units of "${item.productName}". Available stock in batch is only ${item.currentStock}.`);
                                  return;
                                }
                                const updated = [...formItems];
                                updated[idx].adjustedQuantity = val;
                                setFormItems(updated);
                              }}
                            />
                            <button
                              type="button"
                              onClick={() => {
                                const newQty = item.adjustedQuantity + 1;
                                const updated = [...formItems];
                                updated[idx].adjustedQuantity = newQty;
                                setFormItems(updated);
                              }}
                              className="w-6 h-6 bg-slate-100 dark:bg-slate-700 hover:bg-slate-200 dark:hover:bg-slate-600 text-slate-700 dark:text-slate-200 rounded font-black text-xs flex items-center justify-center"
                            >
                              +
                            </button>
                          </div>
                        </td>

                        {/* Financial Impact / Unit Cost */}
                        <td className="px-4 py-3 text-right">
                          <p className="font-bold text-slate-800 dark:text-white">₹{item.unitCost.toFixed(2)}</p>
                          <p className={`text-[10px] font-black ${item.adjustedQuantity < 0 ? 'text-rose-500' : 'text-emerald-500'}`}>
                            {item.adjustedQuantity < 0 ? '-' : '+'}₹{(Math.abs(item.adjustedQuantity) * item.unitCost).toFixed(2)}
                          </p>
                        </td>

                        {/* Action Delete */}
                        <td className="px-4 py-3 text-center">
                          <button 
                            onClick={() => handleRemoveRow(idx)}
                            className="p-1.5 text-slate-400 hover:text-rose-500 hover:bg-rose-50 dark:hover:bg-rose-950/50 rounded-lg transition"
                            title="Remove Line"
                          >
                            <Trash2 className="w-4 h-4" />
                          </button>
                        </td>
                      </tr>
                    ))
                  )}
                </tbody>
              </table>
            </div>
          </div>

        </div>
      )}
    </div>
  );
};
