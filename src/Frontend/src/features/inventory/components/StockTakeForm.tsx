import React, { useState, useEffect, useRef } from 'react';
import { Save, ShieldAlert, Plus, Trash2, CheckCircle, XCircle, Clock, Eye, ClipboardCheck, PlusCircle, Search, FileSpreadsheet, Layers, ChevronDown, ChevronUp, AlertCircle, ArrowUpRight, ArrowDownRight, Package, Calculator, Barcode } from 'lucide-react';
import { getStockTakes, getStockTakeDetails, createOrUpdateStockTake, approveStockTake, rejectStockTake, StockTake } from '../api/stockTake.api';
import { searchProducts } from '../../catalog/api/catalog.api';
import { getProductBatches } from '../../pos/api/pos.api';
import { useAuthStore } from '../../auth/store/auth.store';
import { api } from '../../../utils/api';

export const StockTakeForm = () => {
  const { user } = useAuthStore();
  const isManager = user?.role === 'Manager' || user?.role === 'Owner' || user?.role === 'Admin';

  const downloadTemplate = () => {
    const headers = ['ProductCode', 'Barcode', 'BatchNo', 'PhysicalCount'];
    const rows = [
      ['PROD-001', '8901030678918', 'B01', '50'],
      ['PROD-002', '2900000000002', 'B02', '12']
    ];
    const csvContent = [headers.join(','), ...rows.map(r => r.join(','))].join('\n');
    const blob = new Blob([new Uint8Array([0xEF, 0xBB, 0xBF]), csvContent], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.setAttribute('download', 'Stock_Take_Import_Template.csv');
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
  };

  // List/History state
  const [takes, setTakes] = useState<StockTake[]>([]);
  const [loading, setLoading] = useState(false);
  const [selectedTake, setSelectedTake] = useState<StockTake | null>(null);

  // Form builder state
  const [showNewForm, setShowNewForm] = useState(false);
  const [editingTakeId, setEditingTakeId] = useState<string | null>(null);
  const [scheduledDate, setScheduledDate] = useState(new Date().toISOString().substring(0, 10));
  const [countType, setCountType] = useState<'FULL' | 'CYCLE' | 'SCAN'>('FULL');
  const [isProtocolBannerCollapsed, setIsProtocolBannerCollapsed] = useState(false);

  // Quick-Add Scanner Entry Bar State (Stage 2)
  const [quickSearchQuery, setQuickSearchQuery] = useState('');
  const [quickSearchResults, setQuickSearchResults] = useState<any[]>([]);
  const [quickHighlightIndex, setQuickHighlightIndex] = useState(0);
  const [quickSelectedProduct, setQuickSelectedProduct] = useState<any | null>(null);
  const [quickBatches, setQuickBatches] = useState<any[]>([]);
  const [quickBatchId, setQuickBatchId] = useState('');
  const [quickBatchNumber, setQuickBatchNumber] = useState('');
  const [quickSystemStock, setQuickSystemStock] = useState(0);
  const [quickPhysicalQty, setQuickPhysicalQty] = useState(0);
  const [quickUnitCost, setQuickUnitCost] = useState(0);
  const [isCustomBatchMode, setIsCustomBatchMode] = useState(false);
  const quickSearchInputRef = useRef<HTMLInputElement>(null);
  const quickPhysicalInputRef = useRef<HTMLInputElement>(null);

  // Worksheet Table Items
  const [formItems, setFormItems] = useState<{
    productId: string;
    productName: string;
    productCode?: string;
    batchId: string;
    batchNumber: string;
    systemQuantity: number;
    physicalQuantity: number;
    unitCost?: number;
    searchQuery: string;
    searchResults: any[];
    batches: any[];
    highlightIndex?: number;
  }[]>([]);

  const fetchTakes = async () => {
    try {
      setLoading(true);
      const data = await getStockTakes();
      setTakes(data);
    } catch (error) {
      console.error('Failed to load stock take history', error);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchTakes();
  }, []);

  const handleSelectTake = async (take: StockTake) => {
    try {
      const details = await getStockTakeDetails(take.id);
      setSelectedTake(details);
    } catch (error) {
      console.error('Failed to fetch stock take details', error);
    }
  };

  // --- Stage 2: Quick Scanner Functions ---
  const handleQuickProductSearch = async (query: string) => {
    setQuickSearchQuery(query);
    setQuickHighlightIndex(0);
    if (!query.trim()) {
      setQuickSearchResults([]);
      return;
    }
    try {
      const results = await searchProducts(query);
      setQuickSearchResults(results || []);
    } catch (err) {
      console.error('Quick product search failed', err);
    }
  };

  const selectQuickProduct = async (product: any) => {
    setQuickSelectedProduct(product);
    setQuickSearchQuery(product.name);
    setQuickSearchResults([]);
    setIsCustomBatchMode(false);
    const defaultCost = product.costPrice || product.sellingPrice * 0.7;
    setQuickUnitCost(defaultCost);

    try {
      const batchesList = await getProductBatches(product.id);
      setQuickBatches(batchesList || []);
      if (batchesList && batchesList.length > 0) {
        // Auto-select option with highest available stock as default (Option A)
        const bestDefault = [...batchesList].sort((a, b) => (b.currentStock || 0) - (a.currentStock || 0))[0] || batchesList[0];
        const sanitizedId = (bestDefault.id === '00000000-0000-0000-0000-000000000000') ? '' : bestDefault.id;
        setQuickBatchId(sanitizedId);
        setQuickBatchNumber(bestDefault.batchNumber);
        setQuickSystemStock(bestDefault.currentStock);
        setQuickPhysicalQty(bestDefault.currentStock);
        setQuickUnitCost(bestDefault.costPrice || defaultCost);
      } else {
        setQuickBatchId('');
        setQuickBatchNumber('UNBATCHED');
        setQuickSystemStock(0);
        setQuickPhysicalQty(0);
      }
    } catch (err) {
      console.error('Failed to load product batches for quick selection', err);
    }

    // Auto-focus Physical Count Input
    setTimeout(() => {
      if (quickPhysicalInputRef.current) {
        quickPhysicalInputRef.current.focus();
        quickPhysicalInputRef.current.select();
      }
    }, 50);
  };

  const handleQuickBatchChange = (batchVal: string) => {
    if (batchVal === '__custom__') {
      setIsCustomBatchMode(true);
      setQuickBatchId('');
      setQuickBatchNumber('');
      setQuickSystemStock(0);
      return;
    }
    setIsCustomBatchMode(false);
    const selected = quickBatches.find((b, idx) => b.id === batchVal || `batch-${idx}` === batchVal);
    if (selected) {
      setQuickBatchId(selected.id === '00000000-0000-0000-0000-000000000000' ? '' : selected.id);
      setQuickBatchNumber(selected.batchNumber);
      setQuickSystemStock(selected.currentStock);
      setQuickPhysicalQty(selected.currentStock);
      setQuickUnitCost(selected.costPrice || quickUnitCost);
    }
  };

  const scrollToQuickHighlightItem = (itemIdx: number) => {
    setTimeout(() => {
      const itemEl = document.getElementById(`quick-search-item-${itemIdx}`);
      if (itemEl) {
        itemEl.scrollIntoView({ block: 'nearest', behavior: 'smooth' });
      }
    }, 15);
  };

  const handleQuickKeyDown = (e: React.KeyboardEvent<HTMLInputElement>) => {
    if (!quickSearchResults || quickSearchResults.length === 0) return;

    if (e.key === 'ArrowDown') {
      e.preventDefault();
      const nextIndex = Math.min(quickHighlightIndex + 1, quickSearchResults.length - 1);
      setQuickHighlightIndex(nextIndex);
      scrollToQuickHighlightItem(nextIndex);
    } else if (e.key === 'ArrowUp') {
      e.preventDefault();
      const prevIndex = Math.max(quickHighlightIndex - 1, 0);
      setQuickHighlightIndex(prevIndex);
      scrollToQuickHighlightItem(prevIndex);
    } else if (e.key === 'Enter') {
      e.preventDefault();
      const selectedProd = quickSearchResults[quickHighlightIndex];
      if (selectedProd) {
        selectQuickProduct(selectedProd);
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
    if (quickPhysicalQty < 0) {
      alert('Physical count cannot be negative.');
      return;
    }

    const newLine = {
      productId: quickSelectedProduct.id,
      productName: quickSelectedProduct.name,
      productCode: quickSelectedProduct.productCode || quickSelectedProduct.code || '',
      batchId: quickBatchId,
      batchNumber: quickBatchNumber || 'UNBATCHED',
      systemQuantity: quickSystemStock,
      physicalQuantity: Number(quickPhysicalQty),
      unitCost: quickUnitCost,
      searchQuery: quickSelectedProduct.name,
      searchResults: [],
      batches: quickBatches,
    };

    setFormItems([...formItems, newLine]);

    // Reset Quick-Entry Bar for continuous scanning
    setQuickSelectedProduct(null);
    setQuickSearchQuery('');
    setQuickBatches([]);
    setQuickBatchId('');
    setQuickBatchNumber('');
    setQuickSystemStock(0);
    setQuickPhysicalQty(0);
    setQuickUnitCost(0);
    setIsCustomBatchMode(false);

    // Re-focus search input for next item
    setTimeout(() => {
      if (quickSearchInputRef.current) {
        quickSearchInputRef.current.focus();
      }
    }, 50);
  };

  const handleAddRow = () => {
    setFormItems([
      ...formItems,
      {
        productId: '',
        productName: '',
        batchId: '',
        batchNumber: '',
        systemQuantity: 0,
        physicalQuantity: 0,
        unitCost: 0,
        searchQuery: '',
        searchResults: [],
        batches: [],
        highlightIndex: 0,
      },
    ]);
  };

  const handleRemoveRow = (idx: number) => {
    setFormItems(formItems.filter((_, i) => i !== idx));
  };

  const handleImportCsv = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

    const formData = new FormData();
    formData.append('file', file);

    try {
      setLoading(true);
      const response = await api.post('/api/inventory/stock-take/parse-csv', formData, {
        headers: { 'Content-Type': 'multipart/form-data' }
      });
      const parsedRows = response.data;
      if (Array.isArray(parsedRows) && parsedRows.length > 0) {
        const mappedRows = parsedRows.map((r: any) => ({
          productId: r.productId,
          productName: r.productName,
          batchId: r.batchId === '00000000-0000-0000-0000-000000000000' ? '' : r.batchId,
          batchNumber: r.batchNumber,
          systemQuantity: r.systemQuantity,
          physicalQuantity: r.physicalQuantity,
          unitCost: r.unitCost || 0,
          searchQuery: r.productName,
          searchResults: [],
          batches: r.batchId && r.batchId !== '00000000-0000-0000-0000-000000000000' ? [{
            id: r.batchId,
            batchNumber: r.batchNumber,
            currentStock: r.systemQuantity
          }] : []
        }));
        setFormItems(mappedRows);
        alert(`Successfully imported ${mappedRows.length} count lines!`);
      } else {
        alert("No valid product count lines found in CSV.");
      }
    } catch (err: any) {
      console.error("CSV import failed", err);
      alert("Failed to parse CSV file: " + (err.response?.data?.message || err.message));
    } finally {
      setLoading(false);
      e.target.value = '';
    }
  };

  const handleSaveOrSubmit = async (status: 'DRAFT' | 'REVIEW') => {
    if (formItems.length === 0) {
      alert('Please add at least one product count line.');
      return;
    }

    const validItems = formItems.filter((i) => i.productId !== '');
    if (validItems.length < formItems.length) {
      alert('Please select a product from the search dropdown for all lines.');
      return;
    }

    try {
      const isoScheduledDate = new Date(scheduledDate).toISOString();
      const payload = {
        id: editingTakeId,
        storeId: null,
        scheduledDate: isoScheduledDate,
        status: status,
        items: validItems.map((i) => ({
          productId: i.productId,
          batchId: i.batchId && i.batchId !== '00000000-0000-0000-0000-000000000000' ? i.batchId : null,
          physicalQuantity: Number(i.physicalQuantity) || 0,
        })),
      };

      // Stage 2 Audit Logging Trace
      console.log(`[STOCK_TAKE_AUDIT_TRACE] Status: ${status}, DraftID: ${editingTakeId || 'NEW'}, Date: ${isoScheduledDate}, LineCount: ${validItems.length}, Timestamp: ${new Date().toISOString()}`, payload);

      await createOrUpdateStockTake(payload);
      alert(status === 'DRAFT' ? 'Stock Take draft saved successfully!' : 'Stock Take submitted for review successfully!');
      setFormItems([]);
      setEditingTakeId(null);
      setShowNewForm(false);
      fetchTakes();
    } catch (err: any) {
      console.error('Save/Submit stock take failed', err);
      const backendMsg = err.response?.data?.message || err.response?.data?.title || err.response?.data || err.message;
      alert('Failed to save or submit stock take: ' + (typeof backendMsg === 'string' ? backendMsg : JSON.stringify(backendMsg)));
    }
  };

  const handleEditDraft = async (take: StockTake) => {
    try {
      const details = await getStockTakeDetails(take.id);
      if (!details || !details.items) {
        alert('Could not retrieve items for this stock take draft.');
        return;
      }
      setEditingTakeId(details.id);
      setScheduledDate(details.scheduledDate.substring(0, 10));
      
      const loadedItems = await Promise.all(
        details.items.map(async (i) => {
          let batchesList: any[] = [];
          try {
            batchesList = await getProductBatches(i.productId);
          } catch (err) {
            console.error('Failed to fetch batches for product during draft edit', err);
          }
          return {
            productId: i.productId,
            productName: i.productName,
            batchId: i.batchId || '',
            batchNumber: i.batchNumber || 'UNBATCHED',
            systemQuantity: i.systemQuantity,
            physicalQuantity: i.physicalQuantity,
            searchQuery: i.productName,
            searchResults: [],
            batches: batchesList,
          };
        })
      );
      setFormItems(loadedItems);
      setShowNewForm(true);
    } catch (err) {
      console.error('Failed to load draft details', err);
      alert('Failed to load draft details for editing.');
    }
  };

  const handleApprove = async (id: string) => {
    if (!window.confirm('Are you sure you want to approve this stock take? This will post adjustment entries to the Stock Ledger.')) return;
    try {
      await approveStockTake(id);
      alert('Stock take approved and inventory updated successfully.');
      setSelectedTake(null);
      fetchTakes();
    } catch (err) {
      console.error('Approve failed', err);
      alert('Approval failed.');
    }
  };

  const handleReject = async (id: string) => {
    if (!window.confirm('Are you sure you want to reject this stock take?')) return;
    try {
      await rejectStockTake(id);
      alert('Stock take rejected successfully.');
      setSelectedTake(null);
      fetchTakes();
    } catch (err) {
      console.error('Reject failed', err);
      alert('Rejection failed.');
    }
  };

  // Stage 1 & 2 Calculations for KPI Cards
  const totalCountedLines = formItems.length;
  const matchedCount = formItems.filter(i => Number(i.physicalQuantity) === Number(i.systemQuantity)).length;
  const shortageItems = formItems.filter(i => Number(i.physicalQuantity) < Number(i.systemQuantity));
  const surplusItems = formItems.filter(i => Number(i.physicalQuantity) > Number(i.systemQuantity));

  const totalShortageQty = shortageItems.reduce((acc, i) => acc + (Number(i.systemQuantity) - Number(i.physicalQuantity)), 0);
  const totalSurplusQty = surplusItems.reduce((acc, i) => acc + (Number(i.physicalQuantity) - Number(i.systemQuantity)), 0);
  
  const totalShortageValue = shortageItems.reduce((acc, i) => acc + ((Number(i.systemQuantity) - Number(i.physicalQuantity)) * (i.unitCost || 0)), 0);
  const totalSurplusValue = surplusItems.reduce((acc, i) => acc + ((Number(i.physicalQuantity) - Number(i.systemQuantity)) * (i.unitCost || 0)), 0);

  // Stage 2 Quick Bar Variance Preview Calculation
  const quickVariance = quickSelectedProduct ? Number(quickPhysicalQty) - Number(quickSystemStock) : 0;

  return (
    <div className="min-h-screen bg-slate-950 text-slate-100 p-4 md:p-6 font-sans">
      <div className="max-w-[1600px] mx-auto space-y-6">

        {/* Global ERP Header Band */}
        <div className="flex flex-col md:flex-row items-start md:items-center justify-between gap-4 bg-slate-900/90 border border-slate-800 p-5 rounded-2xl shadow-xl backdrop-blur-md">
          <div className="flex items-center gap-3">
            <div className="p-3 bg-gradient-to-br from-indigo-600 to-violet-700 rounded-xl shadow-lg shadow-indigo-600/30">
              <ClipboardCheck className="w-6 h-6 text-white" />
            </div>
            <div>
              <div className="flex items-center gap-2">
                <h1 className="text-xl md:text-2xl font-black text-white tracking-tight">Stock Take Manager</h1>
                <span className="text-[10px] font-black uppercase px-2 py-0.5 rounded-md bg-violet-950 text-violet-300 border border-violet-700/50">
                  v1.3 Global Standard
                </span>
              </div>
              <p className="text-xs text-slate-400 font-medium">
                Perform high-precision physical stock counts, verify book stock variances, and reconcile stock ledger balances.
              </p>
            </div>
          </div>

          <div className="flex items-center gap-3">
            {!showNewForm ? (
              <button
                onClick={() => { setEditingTakeId(null); setShowNewForm(true); setFormItems([]); }}
                className="px-5 py-2.5 bg-gradient-to-r from-indigo-600 to-violet-600 hover:from-indigo-500 hover:to-violet-500 text-white rounded-xl shadow-lg shadow-indigo-600/30 font-bold text-xs transition flex items-center gap-2"
              >
                <PlusCircle className="w-4 h-4" /> Create New Stock Take Sheet
              </button>
            ) : (
              <button
                onClick={() => { setShowNewForm(false); setEditingTakeId(null); }}
                className="px-5 py-2.5 bg-slate-800 hover:bg-slate-700 text-slate-200 rounded-xl border border-slate-700 font-bold text-xs transition"
              >
                ← Back to Audit History
              </button>
            )}
          </div>
        </div>

        {!showNewForm ? (
          /* Split Panel Dashboard (History View) */
          <div className="grid grid-cols-1 lg:grid-cols-12 gap-6">
            
            {/* Left Panel: History Logs (7 cols) */}
            <div className="lg:col-span-7 bg-slate-900/90 border border-slate-800 rounded-2xl shadow-xl overflow-hidden backdrop-blur-md flex flex-col min-h-[550px]">
              <div className="p-4 bg-slate-900 border-b border-slate-800 font-bold text-slate-200 text-xs flex justify-between items-center">
                <span className="flex items-center gap-2 uppercase tracking-wider text-[11px] text-slate-400 font-extrabold">
                  <Clock className="w-4 h-4 text-indigo-400" /> Count History Sheets
                </span>
                <span className="text-[10px] bg-slate-800 border border-slate-700 px-2 py-0.5 rounded-full font-black text-indigo-300">
                  {takes.length} Audit Records
                </span>
              </div>

              <div className="divide-y divide-slate-800/60 overflow-y-auto max-h-[600px] flex-1">
                {takes.length === 0 ? (
                  <div className="p-12 text-center text-slate-500 flex flex-col items-center justify-center h-full">
                    <ClipboardCheck className="w-12 h-12 mb-3 text-slate-600 stroke-1" />
                    <p className="font-bold text-sm text-slate-400">No stock take sheets found</p>
                    <p className="text-xs text-slate-500 mt-1">Click "Create New Stock Take Sheet" above to start an audit.</p>
                  </div>
                ) : (
                  takes.map((t) => (
                    <div
                      key={t.id}
                      onClick={() => handleSelectTake(t)}
                      className={`p-4 cursor-pointer hover:bg-slate-800/50 transition flex justify-between items-center border-l-4 ${
                        selectedTake?.id === t.id ? 'bg-slate-800/80 border-indigo-500' : 'border-transparent'
                      }`}
                    >
                      <div>
                        <div className="flex items-center gap-2">
                          <span className="font-black text-sm text-white">{t.takeNumber}</span>
                          <span className={`text-[10px] font-black uppercase px-2 py-0.5 rounded-md ${
                            t.status === 'APPROVED' ? 'bg-emerald-950 text-emerald-300 border border-emerald-700/50' :
                            t.status === 'REJECTED' ? 'bg-rose-950 text-rose-300 border border-rose-700/50' :
                            t.status === 'REVIEW' ? 'bg-amber-950 text-amber-300 border border-amber-700/50' : 
                            'bg-slate-800 text-slate-300 border border-slate-700'
                          }`}>
                            {t.status}
                          </span>
                        </div>
                        <p className="text-xs text-slate-400 mt-1">Audit Date: {new Date(t.scheduledDate).toLocaleDateString()}</p>
                        <p className="text-xs text-slate-400 font-bold mt-1">
                          Items Counted: <span className="bg-slate-800 px-2 py-0.5 rounded text-indigo-300 border border-slate-700">{t.totalItemsCount}</span>
                        </p>
                      </div>

                      <div className="text-right">
                        {t.status === 'DRAFT' && (
                          <button
                            onClick={(e) => { e.stopPropagation(); handleEditDraft(t); }}
                            className="px-3 py-1 bg-indigo-900/60 hover:bg-indigo-800/80 text-indigo-300 text-xs font-bold rounded-lg border border-indigo-700/60 transition"
                          >
                            Edit Draft
                          </button>
                        )}
                        {t.status !== 'DRAFT' && (
                          <button className="text-indigo-400 text-xs font-bold hover:underline flex items-center gap-1 mt-2 justify-end">
                            <Eye className="w-3.5 h-3.5" /> Details
                          </button>
                        )}
                      </div>
                    </div>
                  ))
                )}
              </div>
            </div>

            {/* Right Panel: Selected Audit Detail (5 cols) */}
            <div className="lg:col-span-5">
              {selectedTake ? (
                <div className="bg-slate-900/90 border border-slate-800 rounded-2xl shadow-xl p-6 flex flex-col justify-between min-h-[550px] backdrop-blur-md">
                  <div>
                    <div className="flex justify-between items-start border-b border-slate-800 pb-4 mb-4">
                      <div>
                        <h3 className="font-black text-xl text-white">{selectedTake.takeNumber}</h3>
                        <p className="text-xs text-slate-400 mt-0.5">Audit Date: {new Date(selectedTake.scheduledDate).toLocaleDateString()}</p>
                      </div>
                      <span className={`text-xs font-extrabold px-3 py-1 rounded-full ${
                        selectedTake.status === 'APPROVED' ? 'bg-emerald-950 text-emerald-300 border border-emerald-700/50' :
                        selectedTake.status === 'REJECTED' ? 'bg-rose-950 text-rose-300 border border-rose-700/50' :
                        selectedTake.status === 'REVIEW' ? 'bg-amber-950 text-amber-300 border border-amber-700/50' : 
                        'bg-slate-800 text-slate-300 border border-slate-700'
                      }`}>
                        {selectedTake.status}
                      </span>
                    </div>

                    {selectedTake.approvedByName && (
                      <div className="mb-4">
                        <p className="text-xs font-bold text-slate-400 uppercase tracking-wider">Processed By</p>
                        <p className="font-semibold text-slate-200 mt-0.5 text-sm">{selectedTake.approvedByName}</p>
                      </div>
                    )}

                    <div className="mb-6">
                      <p className="text-xs font-bold text-slate-400 uppercase tracking-wider mb-2">Count Line Details</p>
                      <div className="divide-y divide-slate-800 border border-slate-800 rounded-xl overflow-hidden max-h-80 overflow-y-auto">
                        {selectedTake.items?.map((item, idx) => (
                          <div key={idx} className="p-3 bg-slate-950/40 flex justify-between items-center text-sm">
                            <div>
                              <p className="font-bold text-slate-200">{item.productName}</p>
                              <p className="text-xs text-slate-400 mt-0.5">Batch: {item.batchNumber || 'N/A'}</p>
                            </div>
                            <div className="text-right">
                              <p className="text-xs text-slate-400 font-semibold">Sys: {item.systemQuantity} | Phys: {item.physicalQuantity}</p>
                              <span className={`font-black text-sm ${item.varianceQuantity > 0 ? 'text-emerald-400' : item.varianceQuantity < 0 ? 'text-rose-400' : 'text-slate-400'}`}>
                                {item.varianceQuantity > 0 ? `+${item.varianceQuantity}` : item.varianceQuantity}
                              </span>
                            </div>
                          </div>
                        ))}
                      </div>
                    </div>
                  </div>

                  {/* Manager Actions */}
                  {selectedTake.status === 'REVIEW' && (
                    <div className="border-t border-slate-800 pt-4">
                      {isManager ? (
                        <div className="flex gap-4">
                          <button
                            onClick={() => handleReject(selectedTake.id)}
                            className="flex-1 py-3 bg-rose-950/60 text-rose-300 border border-rose-800/80 rounded-xl font-bold hover:bg-rose-900/80 flex items-center justify-center gap-2 transition"
                          >
                            <XCircle className="w-5 h-5" /> Reject Audit
                          </button>
                          <button
                            onClick={() => handleApprove(selectedTake.id)}
                            className="flex-1 py-3 bg-gradient-to-r from-emerald-600 to-teal-600 text-white rounded-xl font-bold hover:from-emerald-500 hover:to-teal-500 flex items-center justify-center gap-2 shadow-lg shadow-emerald-600/30 transition"
                          >
                            <CheckCircle className="w-5 h-5" /> Approve & Adjust
                          </button>
                        </div>
                      ) : (
                        <p className="text-xs text-slate-400 text-center font-semibold">Awaiting Manager Audit Approval</p>
                      )}
                    </div>
                  )}
                </div>
              ) : (
                <div className="bg-slate-900/60 border border-slate-800/80 rounded-2xl p-8 text-center flex flex-col items-center justify-center min-h-[550px]">
                  <ClipboardCheck className="w-16 h-16 text-slate-700 mb-3 stroke-1" />
                  <h4 className="font-bold text-slate-300 text-base">Select an audit sheet entry</h4>
                  <p className="text-xs text-slate-500 max-w-xs mt-1">Review detailed item counts, book variances, and approval status.</p>
                </div>
              )}
            </div>

          </div>
        ) : (
          /* Form Builder View (Layers 1-4 Shell) */
          <div className="space-y-6">

            {/* LAYER 1: Executive KPI Metrics Cards */}
            <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
              
              {/* KPI Card 1: Total Products */}
              <div className="bg-gradient-to-br from-slate-900 via-slate-900 to-indigo-950/40 p-4 rounded-2xl border border-slate-800 shadow-xl relative overflow-hidden group">
                <div className="absolute top-0 left-0 w-1.5 h-full bg-indigo-500" />
                <div className="flex justify-between items-start">
                  <div>
                    <p className="text-[11px] font-extrabold text-slate-400 uppercase tracking-wider">Total Products</p>
                    <h3 className="text-2xl font-black text-white mt-1">{totalCountedLines}</h3>
                    <p className="text-[11px] text-slate-500 mt-1 font-semibold">Lines in sheet</p>
                  </div>
                  <div className="p-3 bg-indigo-500/10 border border-indigo-500/20 rounded-xl text-indigo-400 group-hover:scale-110 transition-transform">
                    <Package className="w-5 h-5" />
                  </div>
                </div>
              </div>

              {/* KPI Card 2: Matched Items */}
              <div className="bg-gradient-to-br from-slate-900 via-slate-900 to-emerald-950/40 p-4 rounded-2xl border border-slate-800 shadow-xl relative overflow-hidden group">
                <div className="absolute top-0 left-0 w-1.5 h-full bg-emerald-500" />
                <div className="flex justify-between items-start">
                  <div>
                    <p className="text-[11px] font-extrabold text-emerald-400 uppercase tracking-wider">Matched Items</p>
                    <h3 className="text-2xl font-black text-emerald-300 mt-1">{matchedCount}</h3>
                    <p className="text-[11px] text-emerald-400/70 mt-1 font-semibold">Zero variance</p>
                  </div>
                  <div className="p-3 bg-emerald-500/10 border border-emerald-500/20 rounded-xl text-emerald-400 group-hover:scale-110 transition-transform">
                    <CheckCircle className="w-5 h-5" />
                  </div>
                </div>
              </div>

              {/* KPI Card 3: Shortage / Deficit */}
              <div className="bg-gradient-to-br from-slate-900 via-slate-900 to-rose-950/40 p-4 rounded-2xl border border-slate-800 shadow-xl relative overflow-hidden group">
                <div className="absolute top-0 left-0 w-1.5 h-full bg-rose-500" />
                <div className="flex justify-between items-start">
                  <div>
                    <p className="text-[11px] font-extrabold text-rose-400 uppercase tracking-wider">Shortage / Deficit</p>
                    <h3 className="text-2xl font-black text-rose-400 mt-1">-{totalShortageQty} pcs</h3>
                    <p className="text-[11px] text-rose-400/80 mt-1 font-semibold">-₹{totalShortageValue.toFixed(2)}</p>
                  </div>
                  <div className="p-3 bg-rose-500/10 border border-rose-500/20 rounded-xl text-rose-400 group-hover:scale-110 transition-transform">
                    <ArrowDownRight className="w-5 h-5" />
                  </div>
                </div>
              </div>

              {/* KPI Card 4: Surplus / Overage */}
              <div className="bg-gradient-to-br from-slate-900 via-slate-900 to-teal-950/40 p-4 rounded-2xl border border-slate-800 shadow-xl relative overflow-hidden group">
                <div className="absolute top-0 left-0 w-1.5 h-full bg-teal-500" />
                <div className="flex justify-between items-start">
                  <div>
                    <p className="text-[11px] font-extrabold text-teal-400 uppercase tracking-wider">Surplus / Overage</p>
                    <h3 className="text-2xl font-black text-teal-300 mt-1">+{totalSurplusQty} pcs</h3>
                    <p className="text-[11px] text-teal-400/80 mt-1 font-semibold">+₹{totalSurplusValue.toFixed(2)}</p>
                  </div>
                  <div className="p-3 bg-teal-500/10 border border-teal-500/20 rounded-xl text-teal-400 group-hover:scale-110 transition-transform">
                    <ArrowUpRight className="w-5 h-5" />
                  </div>
                </div>
              </div>

            </div>

            {/* LAYER 2: Protocol Banner & Count Filter Controls (Distinct Tinted Panel) */}
            <div className="bg-gradient-to-r from-slate-900 via-slate-900 to-violet-950/30 rounded-2xl border border-violet-900/40 shadow-xl p-5 backdrop-blur-md space-y-4">
              
              {/* Draft Concurrency Safeguard Alert (Stage 2 UX Enhancement) */}
              {editingTakeId && (
                <div className="bg-rose-950/40 border border-rose-800/60 rounded-xl p-3 text-rose-200 text-xs flex items-center gap-2 font-bold shadow-md">
                  <ShieldAlert className="w-4 h-4 text-rose-400 flex-shrink-0" />
                  <span>
                    ⚠️ Editing Existing Draft (Sheet ID: <code className="bg-rose-900/60 px-1.5 py-0.5 rounded text-rose-300 font-black">{editingTakeId.substring(0, 8)}...</code>). Do not share this draft with other staff — each counter should create their own sheet.
                  </span>
                </div>
              )}

              {/* Expandable Protocol Banner */}
              <div className="bg-amber-950/30 border border-amber-800/40 rounded-xl p-3.5 text-amber-200/90 text-xs flex items-center justify-between">
                <div className="flex items-center gap-2.5">
                  <ShieldAlert className="w-4 h-4 text-amber-400 flex-shrink-0" />
                  <div>
                    <span className="font-extrabold text-amber-300">Manager Review & Audit Protocol Required</span>
                    {!isProtocolBannerCollapsed && (
                      <span className="text-amber-200/70 ml-2">
                        Counts saved as DRAFT can be resumed anytime. REVIEW submissions lock the sheet for Manager validation before updating ledger stock.
                      </span>
                    )}
                  </div>
                </div>
                <button
                  onClick={() => setIsProtocolBannerCollapsed(!isProtocolBannerCollapsed)}
                  className="text-amber-400 hover:text-amber-300 p-1"
                >
                  {isProtocolBannerCollapsed ? <ChevronDown className="w-4 h-4" /> : <ChevronUp className="w-4 h-4" />}
                </button>
              </div>

              {/* Count Type Selector Pills & Toolbar */}
              <div className="flex flex-col lg:flex-row items-start lg:items-center justify-between gap-4 pt-1">
                
                {/* Count Type Pills */}
                <div>
                  <label className="block text-[11px] font-extrabold text-slate-400 uppercase tracking-wider mb-2">
                    Select Stock Count Protocol Mode
                  </label>
                  <div className="flex flex-wrap items-center gap-2">
                    {[
                      { id: 'FULL', label: 'Full Physical Audit', colorClass: 'bg-indigo-600 text-white shadow-indigo-600/30' },
                      { id: 'CYCLE', label: 'Category Cycle Count', colorClass: 'bg-violet-600 text-white shadow-violet-600/30' },
                      { id: 'SCAN', label: 'Fast Barcode Scan', colorClass: 'bg-teal-600 text-white shadow-teal-600/30' },
                    ].map(p => {
                      const isSelected = countType === p.id;
                      return (
                        <button
                          key={p.id}
                          onClick={() => setCountType(p.id as any)}
                          className={`px-3.5 py-1.5 rounded-xl text-xs font-black transition-all ${
                            isSelected 
                              ? `${p.colorClass} shadow-md scale-105` 
                              : 'bg-slate-800 text-slate-300 hover:bg-slate-700 border border-slate-700/60'
                          }`}
                        >
                          {p.label}
                        </button>
                      );
                    })}
                  </div>
                </div>

                {/* Audit Scheduled Date & Action Buttons */}
                <div className="flex flex-wrap items-center gap-3 w-full lg:w-auto justify-end">
                  <div className="flex items-center gap-2 bg-slate-950/80 px-3 py-1.5 rounded-xl border border-slate-800">
                    <span className="text-[10px] font-bold text-slate-400 uppercase">Audit Date:</span>
                    <input
                      type="date"
                      value={scheduledDate}
                      onChange={(e) => setScheduledDate(e.target.value)}
                      className="bg-transparent text-xs font-bold text-white outline-none"
                    />
                  </div>

                  <button
                    onClick={downloadTemplate}
                    className="px-3.5 py-2 bg-slate-800 hover:bg-slate-700 text-slate-200 rounded-xl border border-slate-700 font-bold text-xs transition flex items-center gap-1.5"
                  >
                    <FileSpreadsheet className="w-3.5 h-3.5 text-slate-400" /> Template
                  </button>

                  <label className="px-3.5 py-2 bg-slate-800 hover:bg-slate-700 text-emerald-400 rounded-xl border border-slate-700 font-bold text-xs transition flex items-center gap-1.5 cursor-pointer">
                    <FileSpreadsheet className="w-3.5 h-3.5 text-emerald-400" /> Import CSV
                    <input type="file" accept=".csv" onChange={handleImportCsv} className="hidden" />
                  </label>

                  <button
                    onClick={() => handleSaveOrSubmit('DRAFT')}
                    className="px-4 py-2 bg-slate-800 hover:bg-slate-700 text-amber-300 rounded-xl border border-slate-700 font-extrabold text-xs transition flex items-center gap-1.5"
                  >
                    <Save className="w-3.5 h-3.5" /> Save Draft
                  </button>

                  <button
                    onClick={() => handleSaveOrSubmit('REVIEW')}
                    className="px-5 py-2 bg-gradient-to-r from-indigo-600 to-violet-600 hover:from-indigo-500 hover:to-violet-500 text-white rounded-xl shadow-md shadow-indigo-600/30 font-black text-xs transition flex items-center gap-1.5"
                  >
                    <Save className="w-4 h-4" /> Submit for Review
                  </button>
                </div>

              </div>
            </div>

            {/* LAYER 3: Top Eye-Level Quick-Entry Command Bar (STAGE 2 INTEGRATION) */}
            <div className="bg-gradient-to-r from-slate-900 via-slate-900 to-indigo-950/80 text-white p-5 rounded-2xl shadow-xl border border-indigo-900/50 space-y-4 relative" style={{ zIndex: 30 }}>
              <div className="flex items-center justify-between relative z-10">
                <div className="text-[11px] text-indigo-400 font-extrabold uppercase tracking-widest flex items-center gap-2">
                  <Search className="w-4 h-4 text-indigo-400" /> Fast Product Scanner & Quick-Add Entry Bar (Top Eye-Level)
                </div>
                <span className="text-[10px] text-slate-400 font-bold">
                  Press <kbd className="bg-slate-800/80 px-1.5 py-0.5 rounded border border-slate-700 text-indigo-300">↓ / ↑</kbd> to navigate, <kbd className="bg-slate-800/80 px-1.5 py-0.5 rounded border border-slate-700 text-indigo-300">Enter</kbd> to select, <kbd className="bg-slate-800/80 px-1.5 py-0.5 rounded border border-slate-700 text-indigo-300">Esc</kbd> to close
                </span>
              </div>

              <div className="grid grid-cols-1 md:grid-cols-12 gap-3 items-end relative z-10">
                
                {/* 1. Search Product Name or Barcode (5 cols) */}
                <div className="md:col-span-5 relative">
                  <label className="block text-[10px] font-extrabold uppercase tracking-wider text-slate-400 mb-1">
                    1. Search Product Name or Barcode
                  </label>
                  <div className="relative">
                    <Search className="w-4 h-4 absolute left-3 top-3 text-slate-500" />
                    <input
                      ref={quickSearchInputRef}
                      type="text"
                      placeholder="Type name or scan barcode..."
                      className="w-full pl-9 pr-4 py-2.5 bg-slate-950 border border-slate-700 rounded-xl text-xs font-bold text-white outline-none focus:ring-2 focus:ring-indigo-500 placeholder:text-slate-500"
                      value={quickSearchQuery}
                      onChange={(e) => handleQuickProductSearch(e.target.value)}
                      onKeyDown={handleQuickKeyDown}
                    />
                    {quickSearchQuery && (
                      <button
                        type="button"
                        onClick={() => { setQuickSearchQuery(''); setQuickSearchResults([]); setQuickSelectedProduct(null); }}
                        className="absolute right-3 top-2.5 text-xs text-slate-500 hover:text-white"
                      >
                        ✕
                      </button>
                    )}
                  </div>

                  {/* Dropdown Search Results List */}
                  {quickSearchResults.length > 0 && (
                    <div className="absolute left-0 right-0 top-full mt-1 bg-slate-900 border border-slate-700 rounded-xl shadow-2xl overflow-hidden max-h-60 overflow-y-auto z-50 divide-y divide-slate-800">
                      {quickSearchResults.map((prod, pIdx) => {
                        const isHighlighted = pIdx === quickHighlightIndex;
                        return (
                          <div
                            key={prod.id}
                            id={`quick-search-item-${pIdx}`}
                            onClick={() => selectQuickProduct(prod)}
                            className={`p-3 cursor-pointer transition flex items-center justify-between text-xs ${
                              isHighlighted ? 'bg-indigo-600 text-white font-bold' : 'hover:bg-slate-800 text-slate-200'
                            }`}
                          >
                            <div>
                              <div className="font-extrabold">{prod.name}</div>
                              <div className={`text-[10px] ${isHighlighted ? 'text-indigo-100' : 'text-slate-400'}`}>
                                Code: {prod.productCode || prod.code || 'N/A'} {prod.barcode ? `| Barcode: ${prod.barcode}` : ''}
                              </div>
                            </div>
                            <span className={`font-black ${isHighlighted ? 'text-white' : 'text-emerald-400'}`}>
                              ₹{prod.sellingPrice?.toFixed(2) || '0.00'}
                            </span>
                          </div>
                        );
                      })}
                    </div>
                  )}
                </div>

                {/* 2. Select Batch Dropdown (3 cols) */}
                <div className="md:col-span-3">
                  <label className="block text-[10px] font-extrabold uppercase tracking-wider text-slate-400 mb-1">
                    2. Select Batch
                  </label>
                  {quickBatches.length > 0 && !isCustomBatchMode ? (
                    <select
                      className="w-full p-2.5 bg-slate-950 border border-slate-700 rounded-xl text-xs font-bold text-white outline-none focus:ring-2 focus:ring-indigo-500"
                      value={quickBatchId || (quickBatchNumber ? quickBatches.find(b => b.batchNumber === quickBatchNumber)?.id || '__custom__' : '')}
                      onChange={(e) => handleQuickBatchChange(e.target.value)}
                    >
                      {quickBatches.map((b, bIdx) => (
                        <option key={bIdx} value={b.id && b.id !== '00000000-0000-0000-0000-000000000000' ? b.id : `batch-${bIdx}`}>
                          {b.batchNumber} {b.expiryDate ? `(Exp: ${b.expiryDate.substring(0, 10)})` : ''} [Stock: {b.currentStock}]
                        </option>
                      ))}
                      <option value="__custom__">+ Custom Batch / Text Entry...</option>
                    </select>
                  ) : (
                    <div>
                      <input
                        type="text"
                        placeholder="Batch No (e.g. BATCH-01)"
                        className="w-full p-2.5 bg-slate-950 border border-slate-700 rounded-xl text-xs font-bold text-white outline-none focus:ring-2 focus:ring-indigo-500 placeholder:text-slate-500"
                        value={quickBatchNumber}
                        onChange={(e) => { setQuickBatchNumber(e.target.value); setQuickBatchId(''); }}
                      />
                      {quickBatches.length > 0 && (
                        <button
                          type="button"
                          onClick={() => setIsCustomBatchMode(false)}
                          className="text-[10px] text-indigo-400 font-extrabold hover:underline mt-1 block"
                        >
                          ← Back to batch dropdown list
                        </button>
                      )}
                    </div>
                  )}
                </div>

                {/* 3. Physical Count Input & Live Variance Calculator Preview (2 cols) */}
                <div className="md:col-span-2">
                  <label className="block text-[10px] font-extrabold uppercase tracking-wider text-slate-400 mb-1 flex items-center justify-between">
                    <span>3. Physical Count</span>
                    {quickSelectedProduct && (
                      <span className={`text-[10px] font-black uppercase px-1.5 py-0.2 rounded ${
                        quickVariance === 0 ? 'bg-slate-800 text-slate-300' :
                        quickVariance > 0 ? 'bg-emerald-950 text-emerald-300' : 'bg-rose-950 text-rose-300'
                      }`}>
                        Var: {quickVariance > 0 ? `+${quickVariance}` : quickVariance}
                      </span>
                    )}
                  </label>
                  <input
                    ref={quickPhysicalInputRef}
                    type="number"
                    min="0"
                    placeholder="Physical Qty"
                    className="w-full p-2.5 bg-slate-950 border border-slate-700 rounded-xl text-xs font-black text-white text-center outline-none focus:ring-2 focus:ring-indigo-500"
                    value={quickPhysicalQty}
                    onChange={(e) => setQuickPhysicalQty(Number(e.target.value))}
                    onKeyDown={(e) => { if (e.key === 'Enter') handleQuickAddLine(); }}
                  />
                </div>

                {/* 4. Add to Sheet Button (2 cols) */}
                <div className="md:col-span-2">
                  <button
                    type="button"
                    onClick={handleQuickAddLine}
                    className="w-full p-2.5 bg-gradient-to-r from-emerald-600 to-teal-600 hover:from-emerald-500 hover:to-teal-500 text-white font-extrabold text-xs rounded-xl shadow-lg shadow-emerald-600/30 transition flex items-center justify-center gap-1.5"
                  >
                    <Plus className="w-4 h-4" /> Add to Sheet
                  </button>
                </div>

              </div>
            </div>

            {/* LAYER 4: Bounded Worksheet Table Container (Data Grid) */}
            <div className="bg-slate-900/90 border border-slate-800 rounded-2xl shadow-xl overflow-hidden backdrop-blur-md space-y-4 p-5">
              <div className="flex items-center justify-between">
                <div className="flex items-center gap-2">
                  <div className="w-2.5 h-2.5 rounded-full bg-indigo-500 animate-pulse" />
                  <h3 className="font-extrabold text-sm text-slate-200 uppercase tracking-wider">
                    Physical Count Worksheet
                  </h3>
                </div>
                <div className="flex items-center gap-3">
                  <button
                    onClick={handleAddRow}
                    className="px-3.5 py-1.5 bg-indigo-900/50 hover:bg-indigo-800/60 text-indigo-300 rounded-xl border border-indigo-700/50 text-xs font-bold transition flex items-center gap-1.5"
                  >
                    <Plus className="w-3.5 h-3.5" /> Add Manual Line
                  </button>
                  <span className="text-xs bg-slate-800 text-indigo-300 px-3 py-1 rounded-full font-extrabold border border-slate-700">
                    {formItems.length} Products Added
                  </span>
                </div>
              </div>

              {/* Data Table */}
              <div className="border border-slate-800 rounded-xl overflow-hidden shadow-inner max-h-[500px] overflow-y-auto">
                <table className="w-full text-left border-collapse">
                  <thead className="sticky top-0 bg-gradient-to-r from-indigo-900 via-indigo-950 to-violet-950 text-white text-[11px] uppercase tracking-wider font-black z-10 shadow-md">
                    <tr>
                      <th className="px-4 py-3 text-center w-12 border-b border-indigo-800/50">#</th>
                      <th className="px-4 py-3 border-b border-indigo-800/50">Product Name & Code</th>
                      <th className="px-4 py-3 border-b border-indigo-800/50">Batch Number</th>
                      <th className="px-4 py-3 text-right border-b border-indigo-800/50">Book Stock (Sys)</th>
                      <th className="px-4 py-3 text-center border-b border-indigo-800/50">Physical Count (Actual)</th>
                      <th className="px-4 py-3 text-right border-b border-indigo-800/50">Variance (- / +)</th>
                      <th className="px-4 py-3 text-right border-b border-indigo-800/50">Unit Cost</th>
                      <th className="px-4 py-3 text-center w-16 border-b border-indigo-800/50">Action</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-slate-800/60 text-xs">
                    {formItems.length === 0 ? (
                      <tr>
                        <td colSpan={8} className="p-8 text-center text-slate-500 italic">
                          No count lines added to worksheet yet. Use the scanner entry bar above or click "Add Manual Line".
                        </td>
                      </tr>
                    ) : (
                      formItems.map((item, idx) => {
                        const variance = Number(item.physicalQuantity) - Number(item.systemQuantity);
                        return (
                          <tr key={idx} className={`${idx % 2 === 0 ? 'bg-slate-900/40' : 'bg-slate-950/60'} hover:bg-indigo-950/30 transition-colors`}>
                            <td className="px-4 py-3 text-center font-bold text-slate-500">
                              <span className="w-5 h-5 rounded-full bg-slate-800 inline-flex items-center justify-center text-[10px] text-slate-300">
                                {idx + 1}
                              </span>
                            </td>
                            <td className="px-4 py-3 font-bold text-slate-200">
                              {item.productName || 'Unselected Item'}
                              {item.productCode && <span className="text-[10px] text-slate-400 block font-normal">Code: {item.productCode}</span>}
                            </td>
                            <td className="px-4 py-3 text-slate-300 font-medium">
                              {item.batchNumber || 'UNBATCHED'}
                            </td>
                            <td className="px-4 py-3 text-right font-bold text-slate-300">
                              {item.systemQuantity} pcs
                            </td>
                            <td className="px-4 py-3 text-center">
                              <input
                                type="number"
                                value={item.physicalQuantity}
                                onChange={(e) => {
                                  const updated = [...formItems];
                                  updated[idx].physicalQuantity = Number(e.target.value);
                                  setFormItems(updated);
                                }}
                                className="w-24 p-1.5 bg-slate-950 border border-slate-700 rounded-lg text-center font-black text-white text-xs outline-none focus:ring-2 focus:ring-indigo-500"
                              />
                            </td>
                            <td className="px-4 py-3 text-right font-black">
                              <span className={`px-2 py-0.5 rounded-md ${
                                variance === 0 ? 'bg-slate-800 text-slate-400' :
                                variance > 0 ? 'bg-emerald-950 text-emerald-300 border border-emerald-700/50' :
                                'bg-rose-950 text-rose-300 border border-rose-700/50'
                              }`}>
                                {variance > 0 ? `+${variance}` : variance}
                              </span>
                            </td>
                            <td className="px-4 py-3 text-right font-semibold text-slate-400">
                              ₹{(item.unitCost || 0).toFixed(2)}
                            </td>
                            <td className="px-4 py-3 text-center">
                              <button
                                onClick={() => handleRemoveRow(idx)}
                                className="text-slate-500 hover:text-rose-400 transition p-1"
                                title="Remove line"
                              >
                                <Trash2 className="w-4 h-4" />
                              </button>
                            </td>
                          </tr>
                        );
                      })
                    )}
                  </tbody>
                </table>
              </div>
            </div>

          </div>
        )}

      </div>
    </div>
  );
};
