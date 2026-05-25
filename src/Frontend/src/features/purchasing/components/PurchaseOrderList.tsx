import React, { useState, useEffect } from 'react';
import { Plus, CheckCircle, Clock, Search } from 'lucide-react';
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
}

export const PurchaseOrderList: React.FC<PurchaseOrderListProps> = ({ onAddNew }) => {
  const [purchaseOrders, setPurchaseOrders] = useState<PurchaseOrder[]>([]);
  const [searchTerm, setSearchTerm] = useState('');
  const [statusFilter, setStatusFilter] = useState('');
  const [loading, setLoading] = useState(true);

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
          <p className="text-gray-500 text-sm">Manage vendor purchase orders and approvals</p>
        </div>
        <button 
          onClick={onAddNew}
          className="px-4 py-2 bg-blue-600 text-white rounded shadow flex items-center font-bold hover:bg-blue-700 transition-colors"
        >
          <Plus className="w-5 h-5 mr-2" /> New Purchase Order
        </button>
      </div>

      <div className="flex gap-4 mb-6">
        <div className="relative flex-1 max-w-md">
          <Search className="absolute left-3 top-3 text-gray-400 w-4 h-4" />
          <input 
            type="text" 
            placeholder="Search PO Number or Supplier..." 
            value={searchTerm}
            onChange={(e) => setSearchTerm(e.target.value)}
            className="w-full pl-10 p-2 border rounded" 
          />
        </div>
        <select 
          value={statusFilter}
          onChange={(e) => setStatusFilter(e.target.value)}
          className="p-2 border rounded bg-white"
        >
          <option value="">All Statuses</option>
          <option value="DRAFT">Draft</option>
          <option value="APPROVED">Approved</option>
          <option value="PARTIAL_GRN">Partial GRN</option>
          <option value="CLOSED">Closed (GRN Complete)</option>
        </select>
      </div>

      {loading ? (
        <div className="text-center py-10 text-gray-500">Loading purchase orders...</div>
      ) : (
        <div className="overflow-x-auto">
          <table className="w-full text-left border-collapse">
            <thead className="bg-slate-100 text-sm">
              <tr>
                <th className="p-3 border">PO Number</th>
                <th className="p-3 border">Supplier</th>
                <th className="p-3 border">PO Date</th>
                <th className="p-3 border">Expected Delivery</th>
                <th className="p-3 border text-right">Total Amount</th>
                <th className="p-3 border text-center">Status</th>
                <th className="p-3 border text-center">Actions</th>
              </tr>
            </thead>
            <tbody>
              {filteredOrders.map((po) => (
                <tr key={po.id} className="border-b hover:bg-slate-50">
                  <td className="p-3 font-bold text-blue-600">{po.poNumber}</td>
                  <td className="p-3 text-slate-800">{po.supplierName}</td>
                  <td className="p-3 text-gray-600">{new Date(po.poDate).toLocaleDateString('en-IN')}</td>
                  <td className="p-3 text-gray-600">{new Date(po.expectedDeliveryDate).toLocaleDateString('en-IN')}</td>
                  <td className="p-3 text-right font-bold">₹{po.totalAmount.toFixed(2)}</td>
                  <td className="p-3 text-center">
                    <span className={`px-2 py-1 rounded text-xs font-bold ${
                      po.status === 'APPROVED' 
                        ? 'bg-green-100 text-green-800' 
                        : po.status === 'DRAFT' 
                        ? 'bg-amber-100 text-amber-800' 
                        : 'bg-blue-100 text-blue-800'
                    }`}>
                      {po.status.replace('_', ' ')}
                    </span>
                  </td>
                  <td className="p-3 text-center">
                    {po.status === 'DRAFT' && (
                      <button 
                        onClick={() => handleApprove(po.id)}
                        className="text-emerald-600 hover:text-emerald-800 font-bold flex items-center justify-center mx-auto bg-emerald-50 px-3 py-1 rounded border border-emerald-200 transition-colors"
                      >
                        <CheckCircle className="w-4 h-4 mr-1" /> Approve
                      </button>
                    )}
                    {po.status === 'APPROVED' && (
                      <span className="text-gray-500 text-sm flex items-center justify-center gap-1 font-medium">
                        <Clock className="w-4 h-4 text-gray-400" /> Awaiting GRN
                      </span>
                    )}
                    {po.status !== 'DRAFT' && po.status !== 'APPROVED' && (
                      <span className="text-blue-600 text-sm font-semibold">
                        Received ({po.status.replace('_', ' ')})
                      </span>
                    )}
                  </td>
                </tr>
              ))}
              {filteredOrders.length === 0 && (
                <tr>
                  <td colSpan={7} className="p-8 text-center text-gray-500">
                    No purchase orders found.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
};
