import React, { useState, useEffect } from 'react';
import { Save, ShieldAlert, Plus, Trash2, CheckCircle, XCircle, Clock, Eye, ClipboardCheck, PlusCircle, Search, FileSpreadsheet } from 'lucide-react';
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
  const [formItems, setFormItems] = useState<{
    productId: string;
    productName: string;
    batchId: string;
    batchNumber: string;
    systemQuantity: number;
    physicalQuantity: number;
    searchQuery: string;
    searchResults: any[];
    batches: any[];
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
        searchQuery: '',
        searchResults: [],
        batches: [],
      },
    ]);
  };

  const handleProductSearch = async (idx: number, query: string) => {
    const newItems = [...formItems];
    newItems[idx].searchQuery = query;

    if (!query.trim()) {
      newItems[idx].searchResults = [];
      setFormItems(newItems);
      return;
    }

    try {
      const results = await searchProducts(query);
      newItems[idx].searchResults = results;
      setFormItems(newItems);
    } catch (err) {
      console.error('Search products failed', err);
    }
  };

  const selectProduct = async (idx: number, product: any) => {
    const newItems = [...formItems];
    newItems[idx].productId = product.id;
    newItems[idx].productName = product.name;
    newItems[idx].searchResults = [];
    newItems[idx].searchQuery = product.name;

    try {
      const batchesList = await getProductBatches(product.id);
      newItems[idx].batches = batchesList || [];
      if (batchesList && batchesList.length > 0) {
        newItems[idx].batchId = batchesList[0].id;
        newItems[idx].batchNumber = batchesList[0].batchNumber;
        newItems[idx].systemQuantity = batchesList[0].currentStock;
      } else {
        newItems[idx].batchId = '';
        newItems[idx].batchNumber = 'NO BATCH';
        newItems[idx].systemQuantity = 0;
      }
    } catch (err) {
      console.error('Fetch product batches failed', err);
    }

    setFormItems(newItems);
  };

  const handleBatchChange = (idx: number, batchId: string) => {
    const newItems = [...formItems];
    const selectedBatch = newItems[idx].batches.find((b) => b.id === batchId);
    if (selectedBatch) {
      newItems[idx].batchId = selectedBatch.id;
      newItems[idx].batchNumber = selectedBatch.batchNumber;
      newItems[idx].systemQuantity = selectedBatch.currentStock;
    }
    setFormItems(newItems);
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
    const validItems = formItems.filter((i) => i.productId !== '');
    if (validItems.length === 0) {
      alert('Please add at least one product count line.');
      return;
    }

    try {
      const payload = {
        id: editingTakeId,
        storeId: null,
        scheduledDate: scheduledDate,
        status: status,
        items: validItems.map((i) => ({
          productId: i.productId,
          batchId: i.batchId || null,
          physicalQuantity: i.physicalQuantity,
        })),
      };

      await createOrUpdateStockTake(payload);
      alert(status === 'DRAFT' ? 'Stock Take draft saved successfully!' : 'Stock Take submitted for review successfully!');
      setFormItems([]);
      setEditingTakeId(null);
      setShowNewForm(false);
      fetchTakes();
    } catch (err) {
      console.error('Save/Submit stock take failed', err);
      alert('Failed to save or submit stock take.');
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
            batchNumber: i.batchNumber || 'NO BATCH',
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

  return (
    <div className="max-w-7xl mx-auto p-6">
      
      {/* Page Header */}
      <div className="flex justify-between items-center mb-6 border-b border-slate-200 pb-4">
        <div>
          <h2 className="text-3xl font-extrabold text-slate-800">Stock Take & Cycle Count</h2>
          <p className="text-sm text-slate-500 mt-1">Audit physical inventory counts against system records and post corrections.</p>
        </div>
        {!showNewForm ? (
          <button 
            onClick={() => { setEditingTakeId(null); setShowNewForm(true); handleAddRow(); }}
            className="px-6 py-2.5 bg-indigo-600 text-white rounded-lg shadow-md font-bold hover:bg-indigo-700 transition"
          >
            Start New Stock Take
          </button>
        ) : (
          <button 
            onClick={() => { setShowNewForm(false); setEditingTakeId(null); }}
            className="px-5 py-2.5 bg-slate-200 text-slate-700 rounded-lg font-bold hover:bg-slate-350 transition"
          >
            Back to History
          </button>
        )}
      </div>

      {/* Split Panel Dashboard */}
      {!showNewForm ? (
        <div className="flex gap-6">
          
          {/* Left Panel: Stock Takes History */}
          <div className="w-7/12 bg-white rounded-xl border border-slate-200 shadow-sm overflow-hidden">
            <div className="p-4 bg-slate-50 border-b border-slate-200 font-bold text-slate-700 flex justify-between items-center">
              <span>Count History Sheets</span>
              <span className="text-xs bg-slate-200 px-2 py-0.5 rounded text-slate-600">{takes.length} sheets</span>
            </div>
            
            <div className="divide-y divide-slate-100 overflow-y-auto max-h-[600px]">
              {takes.length === 0 ? (
                <div className="p-8 text-center text-slate-400">
                  <ClipboardCheck className="w-12 h-12 mx-auto mb-2 stroke-1" />
                  <p className="font-semibold">No stock take sheets found</p>
                </div>
              ) : (
                takes.map((t) => (
                  <div 
                    key={t.id} 
                    onClick={() => handleSelectTake(t)}
                    className={`p-4 cursor-pointer hover:bg-slate-50/50 transition flex justify-between items-center ${selectedTake?.id === t.id ? 'bg-indigo-50/70 border-l-4 border-indigo-600' : ''}`}
                  >
                    <div>
                      <div className="flex items-center gap-2">
                        <span className="font-bold text-slate-800">{t.takeNumber}</span>
                        <span className={`text-[10px] font-black uppercase px-2 py-0.5 rounded ${
                          t.status === 'APPROVED' ? 'bg-emerald-100 text-emerald-800' :
                          t.status === 'REJECTED' ? 'bg-red-100 text-red-800' :
                          t.status === 'REVIEW' ? 'bg-amber-100 text-amber-800' : 'bg-slate-100 text-slate-800'
                        }`}>
                          {t.status}
                        </span>
                      </div>
                      <p className="text-xs text-slate-400 mt-1">Audit Date: {new Date(t.scheduledDate).toLocaleDateString()}</p>
                      <p className="text-xs text-slate-500 font-bold mt-1">Items Counted: <span className="bg-slate-100 px-1.5 py-0.5 rounded">{t.totalItemsCount}</span></p>
                    </div>
                    
                    <div className="text-right">
                      {t.status === 'DRAFT' && (
                        <button 
                          onClick={(e) => { e.stopPropagation(); handleEditDraft(t); }}
                          className="px-3 py-1 bg-indigo-50 hover:bg-indigo-100 text-indigo-700 text-xs font-bold rounded-lg border border-indigo-200 transition"
                        >
                          Edit Draft
                        </button>
                      )}
                      {t.status !== 'DRAFT' && (
                        <button className="text-indigo-600 text-xs font-bold hover:underline flex items-center gap-1 mt-2">
                          <Eye className="w-3.5 h-3.5" /> Details
                        </button>
                      )}
                    </div>
                  </div>
                ))
              )}
            </div>
          </div>

          {/* Right Panel: Detail Panel & Manager Verification */}
          <div className="w-5/12">
            {selectedTake ? (
              <div className="bg-white rounded-xl border border-slate-200 shadow-sm p-6 flex flex-col justify-between min-h-[400px]">
                <div>
                  <div className="flex justify-between items-start border-b pb-4 mb-4">
                    <div>
                      <h3 className="font-bold text-xl text-slate-800">{selectedTake.takeNumber}</h3>
                      <p className="text-xs text-slate-400 mt-1">Audit Date: {new Date(selectedTake.scheduledDate).toLocaleDateString()}</p>
                    </div>
                    <span className={`text-xs font-extrabold px-3 py-1 rounded-full ${
                      selectedTake.status === 'APPROVED' ? 'bg-emerald-100 text-emerald-800' :
                      selectedTake.status === 'REJECTED' ? 'bg-red-100 text-red-800' :
                      selectedTake.status === 'REVIEW' ? 'bg-amber-100 text-amber-800' : 'bg-slate-100 text-slate-800'
                    }`}>
                      {selectedTake.status}
                    </span>
                  </div>

                  {selectedTake.approvedByName && (
                    <div className="mb-4">
                      <p className="text-sm font-semibold text-slate-500">Processed By</p>
                      <p className="font-semibold text-slate-800 mt-1">{selectedTake.approvedByName}</p>
                    </div>
                  )}

                  <div className="mb-6">
                    <p className="text-sm font-bold text-slate-700 mb-2">Count Line Details</p>
                    <div className="divide-y divide-slate-100 border rounded-lg overflow-hidden max-h-80 overflow-y-auto">
                      {selectedTake.items?.map((item, idx) => (
                        <div key={idx} className="p-3 bg-slate-50/30 flex justify-between items-center text-sm">
                          <div>
                            <p className="font-bold text-slate-800">{item.productName}</p>
                            <p className="text-xs text-slate-400 mt-0.5">Batch: {item.batchNumber || 'N/A'}</p>
                          </div>
                          <div className="text-right">
                            <p className="text-xs text-slate-500 font-semibold">Sys: {item.systemQuantity} | Phys: {item.physicalQuantity}</p>
                            <span className={`font-black text-sm ${item.varianceQuantity > 0 ? 'text-green-600' : item.varianceQuantity < 0 ? 'text-red-600' : 'text-gray-500'}`}>
                              {item.varianceQuantity > 0 ? `+${item.varianceQuantity}` : item.varianceQuantity}
                            </span>
                          </div>
                        </div>
                      ))}
                    </div>
                  </div>
                </div>

                {/* Manager Verification Actions */}
                {selectedTake.status === 'REVIEW' && (
                  <div className="border-t pt-4">
                    {isManager ? (
                      <div className="flex gap-4">
                        <button 
                          onClick={() => handleReject(selectedTake.id)}
                          className="flex-1 py-3 bg-red-50 text-red-600 border border-red-200 rounded-xl font-bold hover:bg-red-100 flex items-center justify-center gap-2 transition"
                        >
                          <XCircle className="w-5 h-5" /> Reject Audit
                        </button>
                        <button 
                          onClick={() => handleApprove(selectedTake.id)}
                          className="flex-1 py-3 bg-emerald-600 text-white rounded-xl font-bold hover:bg-emerald-700 shadow-md flex items-center justify-center gap-2 transition"
                        >
                          <CheckCircle className="w-5 h-5" /> Approve & Adjust
                        </button>
                      </div>
                    ) : (
                      <div className="bg-amber-50 text-amber-800 border-l-4 border-amber-500 p-3 rounded text-xs font-semibold flex items-center gap-2">
                        <Clock className="w-4 h-4" /> Manager approval is required to approve this count.
                      </div>
                    )}
                  </div>
                )}
              </div>
            ) : (
              <div className="bg-slate-50 border-2 border-dashed border-slate-200 rounded-xl p-8 text-center text-slate-400 flex flex-col justify-center items-center h-[350px]">
                <Eye className="w-12 h-12 mb-2 stroke-1" />
                <p className="font-semibold text-slate-500">Select a Stock Take Entry</p>
                <p className="text-xs mt-1">Review physical counts, live system inventory snapshots, and variances.</p>
              </div>
            )}
          </div>

        </div>
      ) : (
        
        /* New Stock Take Form Builder */
        <div className="bg-white border border-slate-200 shadow-sm rounded-xl p-6">
          <div className="flex justify-between items-center mb-6 border-b pb-4">
            <div>
              <h3 className="text-xl font-extrabold text-slate-800">{editingTakeId ? 'Edit Stock Take Draft' : 'Record New Stock Take'}</h3>
              <p className="text-xs text-slate-400 mt-1">Audit physical items and write variance corrections to the ledger.</p>
            </div>
            
            <div className="flex gap-3">
              <button 
                onClick={() => handleSaveOrSubmit('DRAFT')}
                className="px-5 py-2.5 bg-slate-100 hover:bg-slate-200 text-slate-700 rounded-lg border font-bold flex items-center transition"
              >
                <Save className="w-4.5 h-4.5 mr-2" /> Save Draft
              </button>
              <button 
                onClick={() => handleSaveOrSubmit('REVIEW')}
                className="px-6 py-2.5 bg-indigo-600 hover:bg-indigo-700 text-white rounded-lg shadow-md font-bold flex items-center transition"
              >
                <CheckCircle className="w-4.5 h-4.5 mr-2" /> Submit for Review
              </button>
            </div>
          </div>

          <div className="bg-orange-50 border-l-4 border-orange-500 p-4 mb-6 rounded-r-lg text-orange-800 text-sm flex items-start">
            <ShieldAlert className="w-5 h-5 mr-3 mt-0.5 flex-shrink-0 text-orange-500" />
            <div>
              <p className="font-bold">Variance Posting Protocol</p>
              <p className="text-xs mt-0.5">Physical counts are matched with system records. Rejections do not change stock, while approved audits automatically generate ledger adjustments at actual cost prices.</p>
            </div>
          </div>

          {/* Schedule Date Selection */}
          <div className="mb-6 w-1/4">
            <label className="block text-sm font-bold text-slate-700 mb-2">Audit scheduled Date</label>
            <input 
              type="date"
              value={scheduledDate}
              onChange={(e) => setScheduledDate(e.target.value)}
              className="w-full p-2.5 border rounded-lg outline-none focus:ring-2 focus:ring-indigo-500 font-semibold text-slate-700 bg-slate-50"
            />
          </div>

          <div className="mb-4 flex justify-between items-center border-t pt-4">
            <h4 className="text-md font-bold text-slate-800">Count Lines</h4>
            <div className="flex items-center space-x-4">
              <button 
                onClick={downloadTemplate}
                className="text-slate-500 hover:text-slate-700 font-bold text-sm flex items-center"
                title="Download CSV template"
              >
                <FileSpreadsheet className="w-4 h-4 mr-1.5" /> Template
              </button>
              <label className="cursor-pointer text-emerald-600 font-bold text-sm flex items-center hover:text-emerald-800">
                <FileSpreadsheet className="w-4 h-4 mr-1.5" /> Import CSV
                <input 
                  type="file" 
                  accept=".csv" 
                  className="hidden" 
                  onChange={handleImportCsv}
                />
              </label>
              <button 
                onClick={handleAddRow}
                className="text-indigo-600 font-bold text-sm flex items-center hover:text-indigo-800"
              >
                <PlusCircle className="w-4 h-4 mr-1.5" /> Add Product Row
              </button>
            </div>
          </div>

          <table className="w-full border-collapse text-left">
            <thead className="bg-slate-50 text-slate-500 text-xs font-bold uppercase tracking-wider">
              <tr>
                <th className="p-3 border w-5/12">Product Search</th>
                <th className="p-3 border w-3/12">Select Batch</th>
                <th className="p-3 border text-center w-1.5/12">System Stock</th>
                <th className="p-3 border text-center w-1.5/12">Physical Count</th>
                <th className="p-3 border text-center w-1/12"></th>
              </tr>
            </thead>
            <tbody>
              {formItems.length === 0 ? (
                <tr>
                  <td colSpan={5} className="p-8 text-center text-slate-400 text-sm">
                    No rows added yet. Click "Add Product Row" to begin.
                  </td>
                </tr>
              ) : (
                formItems.map((item, idx) => (
                  <tr key={idx} className="hover:bg-slate-50/20">
                    
                    {/* Product Search */}
                    <td className="p-3 border relative">
                      <div className="relative">
                        <Search className="absolute left-3 top-2.5 text-slate-400 w-4 h-4" />
                        <input 
                          type="text" 
                          placeholder="Search product..." 
                          className="w-full pl-9 pr-4 py-2 border rounded-lg text-sm outline-none focus:ring-2 focus:ring-indigo-500 font-semibold"
                          value={item.searchQuery}
                          onChange={(e) => handleProductSearch(idx, e.target.value)}
                        />
                      </div>
                      
                      {/* Search Overlay */}
                      {item.searchResults.length > 0 && (
                        <div className="absolute left-3 right-3 bg-white border border-slate-200 rounded-lg shadow-xl mt-1 z-50 max-h-48 overflow-y-auto">
                          {item.searchResults.map((p) => (
                            <div 
                              key={p.id}
                              onClick={() => selectProduct(idx, p)}
                              className="px-4 py-2 cursor-pointer hover:bg-indigo-50 flex justify-between items-center text-xs font-semibold"
                            >
                              <div>
                                <p className="font-bold text-slate-800">{p.name}</p>
                                <p className="text-[10px] text-slate-400">Code: {p.productCode}</p>
                              </div>
                              <span className="text-indigo-600 font-bold">₹{p.sellingPrice.toFixed(2)}</span>
                            </div>
                          ))}
                        </div>
                      )}
                    </td>

                    {/* Batch Selection */}
                    <td className="p-3 border">
                      {item.batches.length > 0 ? (
                        <select
                          className="w-full p-2 border rounded-lg text-sm outline-none focus:ring-2 focus:ring-indigo-500 font-semibold text-slate-700 bg-white"
                          value={item.batchId}
                          onChange={(e) => handleBatchChange(idx, e.target.value)}
                        >
                          {item.batches.map((b) => (
                            <option key={b.id} value={b.id}>
                              {b.batchNumber} {b.expiryDate ? `(Exp: ${b.expiryDate.substring(0, 10)})` : '(No Exp)'}
                            </option>
                          ))}
                        </select>
                      ) : (
                        <span className="text-xs text-slate-400 font-bold pl-2">No active batches</span>
                      )}
                    </td>

                    {/* System Stock */}
                    <td className="p-3 border text-center font-bold text-slate-700 text-sm bg-slate-50/50">
                      {item.productId ? item.systemQuantity : '-'}
                    </td>

                    {/* Physical Count */}
                    <td className="p-3 border">
                      <input 
                        type="number"
                        placeholder="0"
                        className="w-full p-2 border rounded-lg text-center font-black text-sm outline-none focus:ring-2 focus:ring-indigo-500"
                        value={item.physicalQuantity}
                        onChange={(e) => {
                          const newItems = [...formItems];
                          newItems[idx].physicalQuantity = parseFloat(e.target.value) || 0;
                          setFormItems(newItems);
                        }}
                      />
                    </td>

                    {/* Remove row */}
                    <td className="p-3 border text-center">
                      <button 
                        onClick={() => handleRemoveRow(idx)}
                        className="text-slate-300 hover:text-red-500 transition"
                      >
                        <Trash2 className="w-5 h-5" />
                      </button>
                    </td>

                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
};
