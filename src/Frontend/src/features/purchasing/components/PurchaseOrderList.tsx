import React, { useState, useEffect } from 'react';
import { Plus, CheckCircle, Clock, Search, Bot, Sparkles } from 'lucide-react';
import { api } from '../../../utils/api';

export interface PurchaseOrder {
  id: string;
  poNumber: string;
  poDate: string;
  expectedDeliveryDate: string;
  totalAmount: number;
  status: string;
  supplierId: string;
  supplierName: string;
}

interface PurchaseOrderListProps {
  onAddNew: () => void;
  onEdit: (id: string) => void;
}

export const PurchaseOrderList: React.FC<PurchaseOrderListProps> = ({ onAddNew, onEdit }) => {
  const [purchaseOrders, setPurchaseOrders] = useState<PurchaseOrder[]>([]);
  const [searchTerm, setSearchTerm] = useState('');
  const [statusFilter, setStatusFilter] = useState('');
  const [loading, setLoading] = useState(true);
  const [autoGenLoading, setAutoGenLoading] = useState(false);

  const fetchPurchaseOrders = async () => {
    try {
      setLoading(true);
      const res = await api.get('/api/purchasing/purchase-orders');
      setPurchaseOrders(res.data);
    } catch (err) {
      console.error('Failed to load purchase orders', err);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchPurchaseOrders();
  }, []);

  const handleApprove = async (id: string) => {
    if (!confirm('Are you sure you want to approve this Purchase Order?')) return;
    try {
      await api.post(`/api/purchasing/purchase-orders/${id}/approve`);
      alert('Purchase Order approved successfully!');
      fetchPurchaseOrders();
    } catch (err: any) {
      console.error(err);
      alert(err.response?.data?.message || 'Failed to approve Purchase Order');
    }
  };

  const handleAutoGenerateReorder = async () => {
    try {
      setAutoGenLoading(true);
      const res = await api.post('/api/purchasing/purchase-orders/auto-generate-reorder');
      alert(res.data.Message || 'Auto-generated POs successfully!');
      fetchPurchaseOrders();
    } catch (err: any) {
      console.error(err);
      alert(err.response?.data?.message || 'Failed to auto-generate POs');
    } finally {
      setAutoGenLoading(false);
    }
  };

  const filteredOrders = purchaseOrders.filter((po) => {
    const matchesSearch = 
      po.poNumber.toLowerCase().includes(searchTerm.toLowerCase()) ||
      po.supplierName.toLowerCase().includes(searchTerm.toLowerCase());
    const matchesStatus = statusFilter === '' || po.status === statusFilter;
    return matchesSearch && matchesStatus;
  });

  return (
    <div className="bg-white shadow rounded-lg p-6 max-w-7xl mx-auto">
      <div className="flex justify-between items-center mb-6 border-b pb-4">
        <div>
          <h2 className="text-2xl font-bold text-slate-800">Purchase Orders</h2>
          <p className="text-gray-500 text-sm">Manage vendor purchase orders and automated reorder replenishment</p>
        </div>
        
        <div className="flex gap-3 items-center">
          <button 
            onClick={handleAutoGenerateReorder}
            disabled={autoGenLoading}
            className="px-4 py-2 bg-purple-700 hover:bg-purple-800 text-white rounded shadow flex items-center font-bold transition-all disabled:opacity-50"
            title="Auto-calculate low stock items & generate vendor Purchase Orders using 30-day sales velocity"
          >
            {autoGenLoading ? (
              <span className="animate-spin mr-2">⏳</span>
            ) : (
              <Sparkles className="w-5 h-5 mr-2" />
            )}
            {autoGenLoading ? 'Generating...' : '🤖 AI Auto-Reorder POs'}
          </button>

          <button 
            onClick={onAddNew}
            className="px-4 py-2 bg-blue-600 text-white rounded shadow flex items-center font-bold hover:bg-blue-700 transition-colors"
          >
            <Plus className="w-5 h-5 mr-2" /> New Purchase Order
          </button>
        </div>
      </div>

      <div className="flex gap-4 mb-6">
        <div className="relative flex-1 max-w-md">
          <Search className="absolute left-3 top-3 text-gray-400 w-4 h-4" />
          <input 
            type="text" 
            placeholder="Search PO Number or Supplier..." 
            value={searchTerm}
            onChange={(e) => setSearchTerm(e.target.value)}
            className="w-full pl-10 p-2 border rounded font-semibold text-slate-800" 
          />
        </div>
        <select 
          value={statusFilter}
          onChange={(e) => setStatusFilter(e.target.value)}
          className="p-2 border rounded bg-white font-semibold text-slate-800"
        >
          <option value="">All Statuses</option>
          <option value="DRAFT">Draft</option>
          <option value="APPROVED">Approved</option>
          <option value="PARTIAL_GRN">Partial GRN</option>
          <option value="CLOSED">Closed (GRN Complete)</option>
        </select>
      </div>

      {loading ? (
        <div className="text-center py-10 text-gray-500 font-semibold">Loading purchase orders...</div>
      ) : (
        <div className="overflow-x-auto">
          <table className="w-full text-left border-collapse">
            <thead className="bg-slate-100 border-b-2 border-slate-200 text-sm">
              <tr>
                <th className="p-3 font-bold text-slate-700 border-r border-slate-200">PO Number</th>
                <th className="p-3 font-bold text-slate-700 border-r border-slate-200">Supplier</th>
                <th className="p-3 font-bold text-slate-700 border-r border-slate-200">PO Date</th>
                <th className="p-3 font-bold text-slate-700 border-r border-slate-200 text-right">Total Amount (₹)</th>
                <th className="p-3 font-bold text-slate-700 border-r border-slate-200 text-center">Status</th>
                <th className="p-3 font-bold text-slate-700 text-center">Actions</th>
              </tr>
            </thead>
            <tbody className="divide-y text-sm">
              {filteredOrders.length === 0 ? (
                <tr>
                  <td colSpan={6} className="p-8 text-center text-gray-400 font-semibold">
                    No purchase orders found. Click "🤖 AI Auto-Reorder POs" or "New Purchase Order" to get started.
                  </td>
                </tr>
              ) : (
                filteredOrders.map((po) => (
                  <tr key={po.id} className="hover:bg-blue-50/50 transition-colors">
                    <td className="p-3 font-black text-slate-800">{po.poNumber}</td>
                    <td className="p-3 font-bold text-slate-700">{po.supplierName}</td>
                    <td className="p-3 font-semibold text-slate-600">
                      {new Date(po.poDate).toLocaleDateString('en-IN')}
                    </td>
                    <td className="p-3 font-extrabold text-slate-900 text-right">
                      ₹{po.totalAmount.toLocaleString('en-IN', { minimumFractionDigits: 2 })}
                    </td>
                    <td className="p-3 text-center">
                      <span className={`px-2.5 py-1 rounded text-xs font-black uppercase ${
                        po.status === 'APPROVED' ? 'bg-emerald-100 text-emerald-800 border border-emerald-300' :
                        po.status === 'CLOSED' ? 'bg-blue-100 text-blue-800 border border-blue-300' :
                        po.status === 'PARTIAL_GRN' ? 'bg-purple-100 text-purple-800 border border-purple-300' :
                        'bg-amber-100 text-amber-800 border border-amber-300'
                      }`}>
                        {po.status}
                      </span>
                    </td>
                    <td className="p-3 text-center space-x-2">
                      <button 
                        onClick={() => onEdit(po.id)}
                        className="px-3 py-1 bg-slate-100 hover:bg-slate-200 text-slate-700 rounded font-bold text-xs border border-slate-300"
                      >
                        View / Edit
                      </button>
                      {po.status === 'DRAFT' && (
                        <button 
                          onClick={() => handleApprove(po.id)}
                          className="px-3 py-1 bg-emerald-600 hover:bg-emerald-700 text-white rounded font-bold text-xs shadow-xs"
                        >
                          Approve
                        </button>
                      )}
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
