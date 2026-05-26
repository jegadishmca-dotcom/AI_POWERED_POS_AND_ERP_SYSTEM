import React, { useState, useEffect } from 'react';
import { PackageCheck, Save, AlertCircle } from 'lucide-react';
import { api } from '../../../utils/api';

export const GrnForm = () => {
  const [purchaseOrders, setPurchaseOrders] = useState<any[]>([]);
  const [selectedPoId, setSelectedPoId] = useState<string>('');
  const [selectedPo, setSelectedPo] = useState<any>(null);
  const [grnItems, setGrnItems] = useState<any[]>([]);
  const [invoiceNumber, setInvoiceNumber] = useState('');
  const [receivedDate, setReceivedDate] = useState(new Date().toISOString().slice(0,10));

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
        <button 
          onClick={handleConfirmGrn}
          className="px-6 py-2 bg-emerald-600 text-white rounded shadow flex items-center font-bold hover:bg-emerald-700"
        >
          <Save className="w-5 h-5 mr-2" /> Confirm GRN
        </button>
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
          <label className="block text-sm font-bold text-gray-700 mb-2">Supplier Invoice No.</label>
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
            <thead className="bg-slate-100 text-sm">
              <tr>
                <th className="p-3 border">Product</th>
                <th className="p-3 border text-center">Pending Qty</th>
                <th className="p-3 border bg-blue-50 text-center text-blue-800">Accepted Qty</th>
                <th className="p-3 border bg-red-50 text-center text-red-800">Rejected Qty</th>
                <th className="p-3 border w-48">Batch / Expiry</th>
              </tr>
            </thead>
            <tbody>
              {grnItems.map((item, idx) => (
                <tr key={item.id} className="border-b">
                  <td className="p-3">
                    <p className="font-bold">{item.name}</p>
                    <p className="text-xs text-gray-500">Cost: ₹{item.unitCost.toFixed(2)}</p>
                  </td>
                  <td className="p-3 text-center text-lg font-bold text-gray-600">{item.pending}</td>
                  <td className="p-3 bg-blue-50/30">
                    <input 
                      type="number" 
                      className="w-full p-2 border border-blue-200 rounded text-center" 
                      value={item.accepted} 
                      onChange={(e) => handleQuantityChange(idx, 'accepted', parseFloat(e.target.value) || 0)}
                      onFocus={(e) => e.target.select()}
                    />
                  </td>
                  <td className="p-3 bg-red-50/30">
                    <input 
                      type="number" 
                      className="w-full p-2 border border-red-200 rounded text-center mb-1" 
                      value={item.rejected} 
                      onChange={(e) => handleQuantityChange(idx, 'rejected', parseFloat(e.target.value) || 0)}
                      onFocus={(e) => e.target.select()}
                    />
                    {item.rejected > 0 && (
                      <input 
                        type="text" 
                        placeholder="Reason" 
                        className="w-full p-1 text-xs border border-red-200 rounded"
                        onChange={(e) => handleQuantityChange(idx, 'rejectionReason', e.target.value)}
                      />
                    )}
                  </td>
                  <td className="p-3">
                    <input 
                      type="text" 
                      placeholder="Batch No (Optional)" 
                      className="w-full p-2 border rounded text-sm mb-2"
                      value={item.batch}
                      onChange={(e) => handleQuantityChange(idx, 'batch', e.target.value)}
                    />
                    <div className="flex items-center">
                      <input 
                        type="date" 
                        className={`w-full p-2 border rounded text-sm ${item.hasExpiry ? 'border-orange-300' : ''}`}
                        value={item.expiry}
                        onChange={(e) => handleQuantityChange(idx, 'expiry', e.target.value)}
                      />
                      {item.hasExpiry && !item.expiry && <AlertCircle className="w-4 h-4 text-orange-500 ml-1" title="Expiry Date is mandatory" />}
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
};
