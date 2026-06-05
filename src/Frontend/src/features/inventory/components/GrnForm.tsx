import React, { useState, useEffect } from 'react';
import { PackageCheck, Save, AlertCircle, Sparkles } from 'lucide-react';
import { api } from '../../../utils/api';
import { matchSupplierProducts, SupplierProductMatch } from '../../ai/api/ai.api';

export const GrnForm = () => {
  const [purchaseOrders, setPurchaseOrders] = useState<any[]>([]);
  const [selectedPoId, setSelectedPoId] = useState<string>('');
  const [selectedPo, setSelectedPo] = useState<any>(null);
  const [grnItems, setGrnItems] = useState<any[]>([]);
  const [invoiceNumber, setInvoiceNumber] = useState('');
  const [receivedDate, setReceivedDate] = useState(new Date().toISOString().slice(0,10));
  
  // AI Smart Matcher States
  const [showAiModal, setShowAiModal] = useState(false);
  const [aiInput, setAiInput] = useState('');
  const [aiLoading, setAiLoading] = useState(false);
  const [aiMatches, setAiMatches] = useState<SupplierProductMatch[]>([]);

  const handleAiMatch = async () => {
    const lines = aiInput.split('\n').map(l => l.trim()).filter(l => l.length > 0);
    if (lines.length === 0) return;
    
    setAiLoading(true);
    try {
      const results = await matchSupplierProducts({ supplierProductNames: lines });
      setAiMatches(results);
    } catch (err) {
      console.error(err);
      alert('Failed to get AI matches.');
    } finally {
      setAiLoading(false);
    }
  };

  const fetchPurchaseOrders = () => {
    api.get('/api/purchasing/purchase-orders')
      .then(res => {
        const data = res.data;
        setPurchaseOrders(data.filter((po: any) => po.status === 'APPROVED' || po.status === 'PARTIAL_GRN'));
      })
      .catch(err => console.error(err));
  };

  useEffect(() => {
    fetchPurchaseOrders();
  }, []);

  const handleReset = () => {
    setSelectedPoId('');
    setSelectedPo(null);
    setGrnItems([]);
    setInvoiceNumber('');
    setReceivedDate(new Date().toISOString().slice(0,10));
    fetchPurchaseOrders();
  };

  const fetchPoLines = async (poId: string) => {
    if (!poId) {
      setGrnItems([]);
      setSelectedPo(null);
      return;
    }
    try {
      const res = await api.get(`/api/purchasing/purchase-orders/${poId}`);
      const data = res.data;
      setSelectedPo(data);
      setGrnItems(data.items.map((item: any) => ({
        id: item.id,
        purchaseOrderItemId: item.id,
        productId: item.productId,
        name: item.productName,
        ordered: item.orderedQuantity,
        received: item.receivedQuantity,
        pending: item.orderedQuantity - item.receivedQuantity,
        accepted: 0,
        rejected: 0,
        rejectionReason: '',
        batch: '',
        expiry: '',
        hasExpiry: item.hasExpiry,
        unitCost: item.unitCost
      })));
    } catch (err) {
      console.error(err);
    }
  };

  const handleQuantityChange = (idx: number, field: string, value: any) => {
    const updated = [...grnItems];
    updated[idx][field] = value;
    setGrnItems(updated);
  };

  const handleConfirmGrn = async () => {
    // 1. Validate PO is selected
    if (!selectedPo) {
      alert('Please select a Purchase Order first.');
      return;
    }

    // 1.5. Validate Supplier Invoice Number is filled
    if (!invoiceNumber.trim()) {
      alert('Supplier Invoice No. is mandatory.');
      return;
    }

    // 2. Validate at least one item has accepted quantity > 0
    const itemsWithAccepted = grnItems.filter(i => i.accepted > 0);
    if (itemsWithAccepted.length === 0) {
      alert('Please enter Accepted Quantity for at least one item before confirming GRN.');
      return;
    }

    // 3. Validate accepted quantity does not exceed pending quantity
    const overReceivedItem = grnItems.find(i => (i.accepted + i.rejected) > i.pending);
    if (overReceivedItem) {
      alert(`Total received (Accepted + Rejected) for "${overReceivedItem.name}" exceeds Pending Qty of ${overReceivedItem.pending}.`);
      return;
    }

    // 4. Validate Expiry for items with accepted qty > 0
    const invalidItem = grnItems.find(i => i.hasExpiry && i.accepted > 0 && !i.expiry);
    if (invalidItem) {
      alert(`Expiry Date is mandatory for "${invalidItem.name}". Please enter expiry date.`);
      return;
    }

    // 5. Confirmation dialog
    const totalAccepted = itemsWithAccepted.reduce((sum, i) => sum + i.accepted, 0);
    const totalRejected = grnItems.reduce((sum, i) => sum + i.rejected, 0);
    const confirmMsg = `Confirm GRN?\n\n` +
      `PO: ${selectedPo.poNumber}\n` +
      `Items with accepted qty: ${itemsWithAccepted.length}\n` +
      `Total Accepted: ${totalAccepted}\n` +
      `Total Rejected: ${totalRejected}\n\n` +
      `This will update the Stock Ledger. Continue?`;
    if (!window.confirm(confirmMsg)) return;

    try {
      // 1. Create GRN
      const grnPayload = {
        purchaseOrderHeaderId: selectedPo.id,
        supplierId: selectedPo.supplierId,
        supplierInvoiceNumber: invoiceNumber,
        receivedDate: receivedDate,
        items: grnItems.filter(i => i.accepted > 0 || i.rejected > 0).map(i => ({
          purchaseOrderItemId: i.purchaseOrderItemId,
          productId: i.productId,
          batchNumber: i.batch,
          mfgDate: null,
          expiryDate: i.expiry ? i.expiry : null,
          receivedQuantity: i.accepted + i.rejected,
          acceptedQuantity: i.accepted,
          rejectedQuantity: i.rejected,
          rejectionReason: i.rejectionReason,
          unitCost: i.unitCost
        }))
      };

      const res = await api.post('/api/inventory/grn', grnPayload);
      const { id } = res.data;
      
      // 2. Confirm GRN
      await api.post(`/api/inventory/grn/${id}/confirm`);
      
      alert('GRN Confirmed and Stock Ledger updated successfully!');
      handleReset();
    } catch (e: any) {
      console.error(e);
      alert(e.response?.data?.message || 'Error saving GRN');
    }
  };


  return (
    <div className="bg-white shadow rounded-lg p-6 max-w-7xl mx-auto">
      <div className="flex justify-between items-center mb-6 border-b pb-4">
        <h2 className="text-2xl font-bold text-slate-800 flex items-center">
          <PackageCheck className="mr-3 text-emerald-600" /> Goods Receipt Note (GRN)
        </h2>
        <div className="flex space-x-3">
          <button 
            onClick={() => setShowAiModal(true)}
            className="px-4 py-2 bg-indigo-50 text-indigo-700 border border-indigo-200 rounded shadow-sm flex items-center font-bold hover:bg-indigo-100 transition"
          >
            <Sparkles className="w-5 h-5 mr-2" /> AI Smart Match
          </button>
          <button 
            onClick={handleConfirmGrn}
            className="px-6 py-2 bg-emerald-600 text-white rounded shadow flex items-center font-bold hover:bg-emerald-700"
          >
            <Save className="w-5 h-5 mr-2" /> Confirm GRN
          </button>
        </div>
      </div>

      <div className="mb-6 grid grid-cols-3 gap-6">
        <div>
          <label className="block text-sm font-bold text-gray-700 mb-2">Select Purchase Order</label>
          <select 
            className="w-full p-2 border rounded"
            value={selectedPoId}
            onChange={(e) => {
              setSelectedPoId(e.target.value);
              fetchPoLines(e.target.value);
            }}
          >
            <option value="">-- Select PO --</option>
            {purchaseOrders.map(po => (
              <option key={po.id} value={po.id}>{po.poNumber} ({po.supplierName})</option>
            ))}
          </select>
        </div>
        <div>
          <label className="block text-sm font-bold text-gray-700 mb-2">Supplier Invoice No. *</label>
          <input type="text" className="w-full p-2 border rounded" placeholder="Enter Invoice No." value={invoiceNumber} onChange={e => setInvoiceNumber(e.target.value)} />
        </div>
        <div>
          <label className="block text-sm font-bold text-gray-700 mb-2">Received Date</label>
          <input type="date" className="w-full p-2 border rounded" value={receivedDate} onChange={e => setReceivedDate(e.target.value)} />
        </div>
      </div>

      {grnItems.length > 0 && (
        <div className="overflow-x-auto">
          <table className="w-full text-left border-collapse">
            <thead className="bg-slate-50 dark:bg-slate-900 border-b-2 border-blue-200 dark:border-blue-800 text-sm">
              <tr>
                <th className="p-3 font-semibold text-slate-500 dark:text-slate-400">Product</th>
                <th className="p-3 font-semibold text-slate-500 dark:text-slate-400 text-center">Pending Qty</th>
                <th className="p-3 font-semibold text-blue-700 dark:text-blue-300 text-center">Accepted Qty</th>
                <th className="p-3 font-semibold text-red-700 dark:text-red-300 text-center">Rejected Qty</th>
                <th className="p-3 font-semibold text-slate-500 dark:text-slate-400 w-48">Batch / Expiry</th>
              </tr>
            </thead>
            <tbody className="bg-white dark:bg-slate-800">
              {grnItems.map((item, idx) => (
                <tr key={item.id} className="hover:bg-slate-50 dark:hover:bg-slate-700/50 transition">
                  <td className="p-3 border-b-2 border-blue-200 dark:border-blue-800/80">
                    <p className="font-bold text-slate-800 dark:text-white">{item.name}</p>
                    <p className="text-xs text-gray-500 dark:text-slate-400">Cost: ₹{item.unitCost.toFixed(2)}</p>
                  </td>
                  <td className="p-3 text-center text-lg font-bold text-gray-600 dark:text-slate-300 border-b-2 border-blue-200 dark:border-blue-800/80">{item.pending}</td>
                  <td className="p-3 bg-blue-50/30 dark:bg-blue-950/20 border-b-2 border-blue-200 dark:border-blue-800/80">
                    <input 
                      type="number" 
                      className="w-full p-2 border border-blue-200 dark:border-blue-900 rounded text-center bg-white dark:bg-slate-950 text-slate-900 dark:text-white font-semibold" 
                      value={item.accepted} 
                      onChange={(e) => handleQuantityChange(idx, 'accepted', parseFloat(e.target.value) || 0)}
                      onFocus={(e) => e.target.select()}
                    />
                  </td>
                  <td className="p-3 bg-red-50/30 dark:bg-red-950/20 border-b-2 border-blue-200 dark:border-blue-800/80">
                    <input 
                      type="number" 
                      className="w-full p-2 border border-red-200 dark:border-red-900 rounded text-center mb-1 bg-white dark:bg-slate-950 text-slate-900 dark:text-white font-semibold" 
                      value={item.rejected} 
                      onChange={(e) => handleQuantityChange(idx, 'rejected', parseFloat(e.target.value) || 0)}
                      onFocus={(e) => e.target.select()}
                    />
                    {item.rejected > 0 && (
                      <input 
                        type="text" 
                        placeholder="Reason" 
                        className="w-full p-1 text-xs border border-red-200 dark:border-red-900 rounded bg-white dark:bg-slate-950 text-slate-900 dark:text-white"
                        onChange={(e) => handleQuantityChange(idx, 'rejectionReason', e.target.value)}
                      />
                    )}
                  </td>
                  <td className="p-3 border-b-2 border-blue-200 dark:border-blue-800/80">
                    <input 
                      type="text" 
                      placeholder="Batch No (Optional)" 
                      className="w-full p-2 border border-slate-200 dark:border-slate-700 rounded text-sm mb-2 bg-white dark:bg-slate-950 text-slate-900 dark:text-white font-semibold"
                      value={item.batch}
                      onChange={(e) => handleQuantityChange(idx, 'batch', e.target.value)}
                    />
                    <div className="flex items-center">
                      <input 
                        type="date" 
                        className={`w-full p-2 border rounded text-sm bg-white dark:bg-slate-950 text-slate-900 dark:text-white ${item.hasExpiry ? 'border-orange-300 dark:border-orange-900' : 'border-slate-200 dark:border-slate-700'}`}
                        value={item.expiry}
                        onChange={(e) => handleQuantityChange(idx, 'expiry', e.target.value)}
                      />
                      {item.hasExpiry && !item.expiry && <span title="Expiry Date is mandatory"><AlertCircle className="w-4 h-4 text-orange-500 ml-1" /></span>}
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {/* AI Smart Match Modal */}
      {showAiModal && (
        <div className="fixed inset-0 bg-black/50 z-50 flex items-center justify-center p-4">
          <div className="bg-white rounded-xl shadow-2xl max-w-2xl w-full flex flex-col max-h-[90vh]">
            <div className="p-4 border-b flex justify-between items-center bg-indigo-50 rounded-t-xl">
              <h3 className="font-bold text-lg flex items-center text-indigo-900">
                <Sparkles className="w-5 h-5 mr-2 text-indigo-600" /> AI Smart Product Matcher
              </h3>
              <button onClick={() => setShowAiModal(false)} className="text-gray-500 hover:text-gray-800 text-xl font-bold">&times;</button>
            </div>
            
            <div className="p-6 overflow-y-auto flex-1">
              <p className="text-sm text-gray-600 mb-4">
                Paste supplier product names from their invoice (one per line). The AI will resolve spelling variations and match them to our catalog.
              </p>
              
              <textarea 
                className="w-full h-40 p-3 border border-gray-300 rounded-lg focus:ring-2 focus:ring-indigo-500 outline-none text-sm font-mono"
                placeholder="e.g.&#10;Br. Bond Tea 250g&#10;Nestle Maggi 70g"
                value={aiInput}
                onChange={e => setAiInput(e.target.value)}
              />

              {aiMatches.length > 0 && (
                <div className="mt-6 border border-slate-200 dark:border-slate-800 rounded-lg overflow-hidden">
                  <table className="w-full text-sm text-left border-collapse">
                    <thead className="bg-slate-50 dark:bg-slate-900 border-b-2 border-blue-200 dark:border-blue-800">
                      <tr>
                        <th className="p-2.5 font-semibold text-slate-500 dark:text-slate-400">Supplier Product</th>
                        <th className="p-2.5 font-semibold text-slate-500 dark:text-slate-400">Matched Internal Product</th>
                        <th className="p-2.5 font-semibold text-slate-500 dark:text-slate-400">Confidence</th>
                      </tr>
                    </thead>
                    <tbody className="bg-white dark:bg-slate-800">
                      {aiMatches.map((m, i) => (
                        <tr key={i} className="hover:bg-slate-50 dark:hover:bg-slate-700/50 transition">
                          <td className="p-2.5 font-medium text-slate-800 dark:text-white border-b-2 border-blue-200 dark:border-blue-800/80">{m.supplierProductName}</td>
                          <td className="p-2.5 text-indigo-700 dark:text-indigo-400 font-bold border-b-2 border-blue-200 dark:border-blue-800/80">{m.matchedProductName || 'No match found'}</td>
                          <td className="p-2.5 border-b-2 border-blue-200 dark:border-blue-800/80">
                            <span className={`px-2 py-0.5 rounded text-xs font-bold ${
                              m.confidence === 'High' ? 'bg-green-100 text-green-800 dark:bg-green-950 dark:text-green-300' :
                              m.confidence === 'Medium' ? 'bg-yellow-100 text-yellow-800 dark:bg-yellow-950 dark:text-yellow-300' : 'bg-red-100 text-red-800 dark:bg-red-950 dark:text-red-300'
                            }`}>
                              {m.confidence}
                            </span>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </div>

            <div className="p-4 border-t flex justify-end gap-3 bg-gray-50 rounded-b-xl">
              <button 
                onClick={() => setShowAiModal(false)}
                className="px-4 py-2 text-gray-600 font-medium hover:bg-gray-200 rounded-lg transition"
              >
                Close
              </button>
              <button 
                onClick={handleAiMatch}
                disabled={!aiInput.trim() || aiLoading}
                className="px-6 py-2 bg-indigo-600 text-white font-bold rounded-lg hover:bg-indigo-700 transition flex items-center disabled:opacity-50"
              >
                {aiLoading ? (
                  <span className="flex items-center">
                    <div className="animate-spin w-4 h-4 border-2 border-white border-t-transparent rounded-full mr-2"></div>
                    Matching...
                  </span>
                ) : (
                  <>
                    <Sparkles className="w-4 h-4 mr-2" /> Match Products
                  </>
                )}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
