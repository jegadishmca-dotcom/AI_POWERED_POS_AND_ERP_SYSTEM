import React, { useState, useEffect } from 'react';
import { Save, ShieldAlert, Plus, Trash2, CheckCircle, XCircle, Clock, Eye, AlertCircle, FileSpreadsheet, PlusCircle, Search } from 'lucide-react';
import { getStockAdjustments, createStockAdjustment, approveStockAdjustment, rejectStockAdjustment, StockAdjustment } from '../api/stockAdjustment.api';
import { searchProducts } from '../../catalog/api/catalog.api';
import { getProductBatches } from '../../pos/api/pos.api';
import { useAuthStore } from '../../auth/store/auth.store';
import { api } from '../../../utils/api';

export const StockAdjustmentForm = () => {
  const { user } = useAuthStore();
  const isManager = user?.role === 'Manager' || user?.role === 'Owner' || user?.role === 'Admin';
  
  // List/History state
  const [adjustments, setAdjustments] = useState<StockAdjustment[]>([]);
  const [loading, setLoading] = useState(false);
  const [selectedAdjustment, setSelectedAdjustment] = useState<StockAdjustment | null>(null);

  // Form builder state
  const [showNewForm, setShowNewForm] = useState(false);
  const [reason, setReason] = useState('DAMAGE');
  const [formItems, setFormItems] = useState<{
    productId: string;
    productName: string;
    batchId: string;
    batchNumber: string;
    adjustedQuantity: number;
    unitCost: number;
    searchQuery: string;
    searchResults: any[];
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

  const handleAddRow = () => {
    setFormItems([
      ...formItems,
      {
        productId: '',
        productName: '',
        batchId: '',
        batchNumber: '',
        adjustedQuantity: -1,
        unitCost: 0,
        searchQuery: '',
        searchResults: [],
        batches: [],
        currentStock: 0,
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
    newItems[idx].unitCost = product.costPrice || product.sellingPrice * 0.7; // Fallback estimate
    newItems[idx].searchResults = [];
    newItems[idx].searchQuery = product.name;

    try {
      const batchesList = await getProductBatches(product.id);
      newItems[idx].batches = batchesList || [];
      if (batchesList && batchesList.length > 0) {
        newItems[idx].batchId = batchesList[0].id;
        newItems[idx].batchNumber = batchesList[0].batchNumber;
        newItems[idx].currentStock = batchesList[0].currentStock;
        newItems[idx].unitCost = batchesList[0].costPrice || newItems[idx].unitCost;
      } else {
        newItems[idx].batchId = '';
        newItems[idx].batchNumber = 'NO BATCH';
        newItems[idx].currentStock = 0;
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
      newItems[idx].currentStock = selectedBatch.currentStock;
      newItems[idx].unitCost = selectedBatch.costPrice || newItems[idx].unitCost;
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
      const response = await api.post('/api/inventory/stock-adjustment/parse-csv', formData, {
        headers: { 'Content-Type': 'multipart/form-data' }
      });
      const parsedRows = response.data;
      if (Array.isArray(parsedRows) && parsedRows.length > 0) {
        const mappedRows = parsedRows.map((r: any) => ({
          productId: r.productId,
          productName: r.productName,
          batchId: r.batchId === '00000000-0000-0000-0000-000000000000' ? '' : r.batchId,
          batchNumber: r.batchNumber,
          adjustedQuantity: r.adjustedQuantity,
          unitCost: r.unitCost,
          searchQuery: r.productName,
          searchResults: [],
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
    const validItems = formItems.filter((i) => i.productId !== '');
    if (validItems.length === 0) {
      alert('Please add at least one product line.');
      return;
    }

    // Validation check: ensure negative adjustments don't exceed current batch stock
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

  return (
    <div className="max-w-7xl mx-auto p-6">
      
      {/* Dynamic Header */}
      <div className="flex justify-between items-center mb-6 border-b border-slate-200 pb-4">
        <div>
          <h2 className="text-3xl font-extrabold text-slate-800">Stock Adjustment Manager</h2>
          <p className="text-sm text-slate-500 mt-1">Review inventory discrepancy logs or record new adjustments.</p>
        </div>
        {!showNewForm ? (
          <button 
            onClick={() => { setShowNewForm(true); handleAddRow(); }}
            className="px-6 py-2.5 bg-indigo-600 text-white rounded-lg shadow-md font-bold hover:bg-indigo-700 transition"
          >
            Create New Adjustment
          </button>
        ) : (
          <button 
            onClick={() => setShowNewForm(false)}
            className="px-5 py-2.5 bg-slate-200 text-slate-700 rounded-lg font-bold hover:bg-slate-350 transition"
          >
            Back to Dashboard
          </button>
        )}
      </div>

      {/* Split Panel Dashboard */}
      {!showNewForm ? (
        <div className="flex gap-6">
          
          {/* Left Panel: Adjustments Log */}
          <div className="w-7/12 bg-white rounded-xl border border-slate-200 shadow-sm overflow-hidden">
            <div className="p-4 bg-slate-50 border-b border-slate-200 font-bold text-slate-700 flex justify-between items-center">
              <span>Adjustment History logs</span>
              <span className="text-xs bg-slate-200 px-2 py-0.5 rounded text-slate-600">{adjustments.length} logs</span>
            </div>
            
            <div className="divide-y divide-slate-100 overflow-y-auto max-h-[600px]">
              {adjustments.length === 0 ? (
                <div className="p-8 text-center text-slate-400">
                  <FileSpreadsheet className="w-12 h-12 mx-auto mb-2 stroke-1" />
                  <p className="font-semibold">No adjustments found</p>
                </div>
              ) : (
                adjustments.map((a) => (
                  <div 
                    key={a.id} 
                    onClick={() => setSelectedAdjustment(a)}
                    className={`p-4 cursor-pointer hover:bg-slate-50/50 transition flex justify-between items-center ${selectedAdjustment?.id === a.id ? 'bg-indigo-50/70 border-l-4 border-indigo-600' : ''}`}
                  >
                    <div>
                      <div className="flex items-center gap-2">
                        <span className="font-bold text-slate-800">{a.adjustmentNumber}</span>
                        <span className={`text-[10px] font-black uppercase px-2 py-0.5 rounded ${
                          a.status === 'APPROVED' ? 'bg-emerald-100 text-emerald-800' :
                          a.status === 'REJECTED' ? 'bg-red-100 text-red-800' : 'bg-amber-100 text-amber-800'
                        }`}>
                          {a.status}
                        </span>
                      </div>
                      <p className="text-xs text-slate-400 mt-1">Date: {new Date(a.createdAt).toLocaleString()}</p>
                      <p className="text-xs text-slate-500 font-bold mt-1">Reason: <span className="bg-slate-100 px-1.5 py-0.5 rounded">{a.reason}</span></p>
                    </div>
                    
                    <div className="text-right">
                      <p className="text-sm font-extrabold text-slate-700">{a.items.length} Line items</p>
                      <button className="text-indigo-600 text-xs font-bold hover:underline flex items-center gap-1 mt-2">
                        <Eye className="w-3.5 h-3.5" /> Details
                      </button>
                    </div>
                  </div>
                ))
              )}
            </div>
          </div>

          {/* Right Panel: Detail Panel & Approval Actions */}
          <div className="w-5/12">
            {selectedAdjustment ? (
              <div className="bg-white rounded-xl border border-slate-200 shadow-sm p-6 flex flex-col justify-between min-h-[400px]">
                <div>
                  <div className="flex justify-between items-start border-b pb-4 mb-4">
                    <div>
                      <h3 className="font-bold text-xl text-slate-800">{selectedAdjustment.adjustmentNumber}</h3>
                      <p className="text-xs text-slate-400 mt-1">Submitted on {new Date(selectedAdjustment.createdAt).toLocaleString()}</p>
                    </div>
                    <span className={`text-xs font-extrabold px-3 py-1 rounded-full ${
                      selectedAdjustment.status === 'APPROVED' ? 'bg-emerald-100 text-emerald-800' :
                      selectedAdjustment.status === 'REJECTED' ? 'bg-red-100 text-red-800' : 'bg-amber-100 text-amber-800'
                    }`}>
                      {selectedAdjustment.status}
                    </span>
                  </div>

                  <div className="mb-4">
                    <p className="text-sm font-semibold text-slate-500">Reason Code</p>
                    <p className="font-bold text-slate-800 bg-slate-50 p-2 rounded mt-1">{selectedAdjustment.reason}</p>
                  </div>

                  {selectedAdjustment.approvedByName && (
                    <div className="mb-4">
                      <p className="text-sm font-semibold text-slate-500">Processed By</p>
                      <p className="font-semibold text-slate-800 mt-1">{selectedAdjustment.approvedByName}</p>
                    </div>
                  )}

                  <div className="mb-6">
                    <p className="text-sm font-bold text-slate-700 mb-2">Adjusted Items</p>
                    <div className="divide-y divide-slate-100 border rounded-lg overflow-hidden">
                      {selectedAdjustment.items.map((item, idx) => (
                        <div key={idx} className="p-3 bg-slate-50/30 flex justify-between items-center text-sm">
                          <div>
                            <p className="font-bold text-slate-800">{item.productName || 'Product'}</p>
                            <p className="text-xs text-slate-400 mt-0.5">Batch: {item.batchNumber || 'N/A'}</p>
                          </div>
                          <div className="text-right">
                            <span className={`font-black text-md ${item.adjustedQuantity > 0 ? 'text-green-600' : 'text-red-600'}`}>
                              {item.adjustedQuantity > 0 ? `+${item.adjustedQuantity}` : item.adjustedQuantity}
                            </span>
                            <p className="text-xs text-slate-400">Cost: ₹{item.unitCost.toFixed(2)}</p>
                          </div>
                        </div>
                      ))}
                    </div>
                  </div>
                </div>

                {/* Manager actions */}
                {selectedAdjustment.status === 'PENDING' && (
                  <div className="border-t pt-4">
                    {isManager ? (
                      <div className="flex gap-4">
                        <button 
                          onClick={() => handleReject(selectedAdjustment.id)}
                          className="flex-1 py-3 bg-red-50 text-red-600 border border-red-200 rounded-xl font-bold hover:bg-red-100 flex items-center justify-center gap-2 transition"
                        >
                          <XCircle className="w-5 h-5" /> Reject Write-Off
                        </button>
                        <button 
                          onClick={() => handleApprove(selectedAdjustment.id)}
                          className="flex-1 py-3 bg-emerald-600 text-white rounded-xl font-bold hover:bg-emerald-700 shadow-md flex items-center justify-center gap-2 transition"
                        >
                          <CheckCircle className="w-5 h-5" /> Approve & Adjust
                        </button>
                      </div>
                    ) : (
                      <div className="bg-amber-50 text-amber-800 border-l-4 border-amber-500 p-3 rounded text-xs font-semibold flex items-center gap-2">
                        <Clock className="w-4 h-4" /> Manager approval is required to process this adjustment.
                      </div>
                    )}
                  </div>
                )}
              </div>
            ) : (
              <div className="bg-slate-50 border-2 border-dashed border-slate-200 rounded-xl p-8 text-center text-slate-400 flex flex-col justify-center items-center h-[350px]">
                <Eye className="w-12 h-12 mb-2 stroke-1" />
                <p className="font-semibold text-slate-500">Select an adjustment entry</p>
                <p className="text-xs mt-1">Review its detailed adjustment lines, batch number, and manager status.</p>
              </div>
            )}
          </div>

        </div>
      ) : (
        
        /* Record New Adjustment Form Builders */
        <div className="bg-white border border-slate-200 shadow-sm rounded-xl p-6">
          <div className="flex justify-between items-center mb-6 border-b pb-4">
            <div>
              <h3 className="text-xl font-extrabold text-slate-800">Record New Adjustment</h3>
              <p className="text-xs text-slate-400 mt-1">Create a correction sheet for damaged, expired, or found stock.</p>
            </div>
            
            <button 
              onClick={handleSubmitAdjustment}
              className="px-6 py-2.5 bg-indigo-600 text-white rounded-lg shadow-md flex items-center font-bold hover:bg-indigo-700 transition"
            >
              <Save className="w-5 h-5 mr-2" /> Submit for Approval
            </button>
          </div>

          <div className="bg-orange-50 border-l-4 border-orange-500 p-4 mb-6 rounded-r-lg text-orange-800 text-sm flex items-start">
            <ShieldAlert className="w-5 h-5 mr-3 mt-0.5 flex-shrink-0 text-orange-500" />
            <div>
              <p className="font-bold">Manager Review Protocol</p>
              <p className="text-xs mt-0.5">All adjustment items are logged as PENDING. Quantity updates will not take effect on the Stock Ledger until a Manager validates and approves this request.</p>
            </div>
          </div>

          {/* Reason Code Selection */}
          <div className="mb-6 w-1/3">
            <label className="block text-sm font-bold text-slate-700 mb-2">Adjustment Reason Code</label>
            <select 
              value={reason} 
              onChange={(e) => setReason(e.target.value)}
              className="w-full p-2.5 border rounded-lg outline-none focus:ring-2 focus:ring-indigo-500 font-semibold text-slate-700 bg-slate-50"
            >
              <option value="DAMAGE">DAMAGE (Write-off damaged / broken goods)</option>
              <option value="EXPIRED">EXPIRED (Write-off expired goods)</option>
              <option value="THEFT">THEFT (Write-off shrinkage / theft)</option>
              <option value="FOUND">FOUND (Write-in found surplus stock)</option>
              <option value="MARKET_PURCHASE">MARKET_PURCHASE (New Purchase from market without PO)</option>
            </select>
          </div>

          <div className="mb-4 flex justify-between items-center border-t pt-4">
            <h4 className="text-md font-bold text-slate-800">Adjustment Lines</h4>
            <div className="flex items-center space-x-4">
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

          <table className="w-full border-collapse">
            <thead className="bg-slate-50 text-slate-500 text-xs font-bold uppercase tracking-wider">
              <tr>
                <th className="p-3 border text-left w-5/12">Product Search</th>
                <th className="p-3 border text-left w-3/12">Select Batch</th>
                <th className="p-3 border text-center w-1.5/12">Current Stock</th>
                <th className="p-3 border text-center w-1.5/12">Adjusted Qty (- / +)</th>
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
                    
                    {/* Product Search Input */}
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
                      
                      {/* Dropdown search overlay */}
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
                              <span className="text-indigo-600 font-bold">MRP: ₹{p.sellingPrice.toFixed(2)}</span>
                            </div>
                          ))}
                        </div>
                      )}
                    </td>

                    {/* Batch Selector */}
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

                    {/* Current Stock */}
                    <td className="p-3 border text-center font-bold text-slate-700 text-sm">
                      {item.productId ? item.currentStock : '-'}
                    </td>

                    {/* Delta adjustment quantity */}
                    <td className="p-3 border">
                      <input 
                        type="number"
                        placeholder="-1"
                        className={`w-full p-2 border rounded-lg text-center font-black text-sm outline-none focus:ring-2 focus:ring-indigo-500 ${
                          item.adjustedQuantity < 0 ? 'text-red-600 bg-red-50/50 focus:border-red-500' : 'text-green-600 bg-green-50/50 focus:border-green-500'
                        }`}
                        value={item.adjustedQuantity}
                        onChange={(e) => {
                          const newItems = [...formItems];
                          newItems[idx].adjustedQuantity = parseInt(e.target.value) || 0;
                          setFormItems(newItems);
                        }}
                      />
                    </td>

                    {/* Delete Line Row */}
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
