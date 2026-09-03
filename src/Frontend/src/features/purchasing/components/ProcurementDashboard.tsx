import React, { useState, useEffect } from 'react';
import { AreaChart, Area, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer, BarChart, Bar, Legend, PieChart, Pie, Cell } from 'recharts';
import { api } from '../../../utils/api';
import { Modal } from '../../../components/common/Modal';

export function ProcurementDashboard() {
  const [recommendations, setRecommendations] = useState<any[]>([]);
  const [addedProductIds, setAddedProductIds] = useState<string[]>([]);
  const [isGenerating, setIsGenerating] = useState(false);
  const [showConfirmModal, setShowConfirmModal] = useState(false);
  const [feedbackMessage, setFeedbackMessage] = useState<string | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  useEffect(() => {
    // Mocking procurement data based on the newly designed IPurchaseRecommendationEngine
    setRecommendations([
      { ProductId: '1', ProductName: 'Apple Premium', CurrentStock: 50, RecommendedQuantity: 500, Priority: 'Critical', DaysUntilStockout: 2, SupplierName: 'Fresh Farms Ltd' },
      { ProductId: '2', ProductName: 'Milk 1L', CurrentStock: 120, RecommendedQuantity: 1000, Priority: 'Critical', DaysUntilStockout: 4, SupplierName: 'Dairy Co' },
      { ProductId: '3', ProductName: 'Whole Wheat Bread', CurrentStock: 40, RecommendedQuantity: 200, Priority: 'High', DaysUntilStockout: 9, SupplierName: 'Daily Bakes' },
      { ProductId: '4', ProductName: 'Basmati Rice 5kg', CurrentStock: 80, RecommendedQuantity: 300, Priority: 'Medium', DaysUntilStockout: 22, SupplierName: 'Agro Suppliers' },
      { ProductId: '5', ProductName: 'Olive Oil 1L', CurrentStock: 15, RecommendedQuantity: 60, Priority: 'Low', DaysUntilStockout: 45, SupplierName: 'Premium Imports' },
    ]);
  }, []);

  const handleConfirmGenerateDraftPOs = async () => {
    if (isGenerating) return; // Immediate double-click lock
    setIsGenerating(true);
    setShowConfirmModal(false);
    setFeedbackMessage(null);
    setErrorMessage(null);
    try {
      const response = await api.post<any>('/api/Purchasing/purchase-orders/auto-generate-reorder');
      const msg = response.data?.message || response.data?.Message || 'Successfully auto-generated draft purchase orders in PostgreSQL.';
      setFeedbackMessage(msg);
    } catch (err: any) {
      const errorDetail = err.response?.data?.message || err.response?.data || err.message || 'Failed to auto-generate draft purchase orders.';
      setErrorMessage(typeof errorDetail === 'string' ? errorDetail : 'Failed to auto-generate draft purchase orders.');
    } finally {
      setIsGenerating(false);
    }
  };

  // NOTE: Add to PO operates on local UI toggle state for now.
  // The recommendations list above currently uses mock product IDs;
  // wiring to backend requires creating a real recommendation query endpoint first.
  const handleAddToPO = (productId: string) => {
    if (addedProductIds.includes(productId)) {
      setAddedProductIds(prev => prev.filter(id => id !== productId));
    } else {
      setAddedProductIds(prev => [...prev, productId]);
    }
  };

  const getPriorityBadge = (priority: string) => {
    switch (priority) {
      case 'Critical': return <span className="px-2 py-1 bg-red-900 text-red-300 text-xs font-medium rounded-full border border-red-700">Critical (≤ 7 days)</span>;
      case 'High': return <span className="px-2 py-1 bg-orange-900 text-orange-300 text-xs font-medium rounded-full border border-orange-700">High (≤ 14 days)</span>;
      case 'Medium': return <span className="px-2 py-1 bg-yellow-900 text-yellow-300 text-xs font-medium rounded-full border border-yellow-700">Medium (≤ 30 days)</span>;
      default: return <span className="px-2 py-1 bg-slate-700 text-slate-300 text-xs font-medium rounded-full border border-slate-600">Low Monitor</span>;
    }
  };

  return (
    <div className="p-6 space-y-6 bg-slate-900 min-h-screen text-slate-200">
      {errorMessage && (
        <div className="bg-rose-950/80 border border-rose-500/50 text-rose-300 px-4 py-3 rounded-xl flex items-center justify-between shadow-lg">
          <span className="text-sm font-medium">✕ {errorMessage}</span>
          <button onClick={() => setErrorMessage(null)} className="text-rose-400 hover:text-rose-200 text-xs font-bold cursor-pointer">
            Dismiss
          </button>
        </div>
      )}

      {feedbackMessage && (
        <div className="bg-emerald-950/80 border border-emerald-500/50 text-emerald-300 px-4 py-3 rounded-xl flex items-center justify-between shadow-lg">
          <span className="text-sm font-medium">✓ {feedbackMessage}</span>
          <button onClick={() => setFeedbackMessage(null)} className="text-emerald-400 hover:text-emerald-200 text-xs font-bold cursor-pointer">
            Dismiss
          </button>
        </div>
      )}

      <div className="flex justify-between items-center">
        <div>
          <h1 className="text-2xl font-bold text-white">Procurement Intelligence</h1>
          <p className="text-xs text-slate-400 mt-1">Automated stock velocity and EOQ purchase forecasting</p>
        </div>
        <button 
          type="button"
          onClick={() => setShowConfirmModal(true)}
          disabled={isGenerating}
          className="bg-blue-600 hover:bg-blue-700 text-white px-4 py-2 rounded-lg text-sm font-medium transition-colors cursor-pointer shadow-lg shadow-blue-600/30 disabled:opacity-50 disabled:pointer-events-none"
        >
          {isGenerating ? 'Generating POs...' : 'Generate Draft POs'}
        </button>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
        <div className="bg-slate-800 p-4 rounded-xl border border-slate-700 shadow-sm relative overflow-hidden">
          <div className="absolute top-0 left-0 w-full h-1 bg-red-500"></div>
          <p className="text-sm text-slate-400 mb-1">Critical Reorders</p>
          <p className="text-3xl font-bold text-red-500">12</p>
          <p className="text-xs text-slate-500 mt-2">Action required today</p>
        </div>
        <div className="bg-slate-800 p-4 rounded-xl border border-slate-700 shadow-sm relative overflow-hidden">
          <div className="absolute top-0 left-0 w-full h-1 bg-orange-500"></div>
          <p className="text-sm text-slate-400 mb-1">High Priority Reorders</p>
          <p className="text-3xl font-bold text-orange-500">28</p>
          <p className="text-xs text-slate-500 mt-2">Stockout in 8-14 days</p>
        </div>
        <div className="bg-slate-800 p-4 rounded-xl border border-slate-700 shadow-sm relative overflow-hidden">
          <div className="absolute top-0 left-0 w-full h-1 bg-blue-500"></div>
          <p className="text-sm text-slate-400 mb-1">Pending Purchase Orders</p>
          <p className="text-3xl font-bold text-blue-500">5</p>
          <p className="text-xs text-slate-500 mt-2">Awaiting supplier confirmation</p>
        </div>
      </div>

      <div className="bg-slate-800 rounded-xl border border-slate-700 overflow-hidden">
        <div className="px-6 py-4 border-b border-slate-700 flex justify-between items-center bg-slate-800">
          <h3 className="text-lg font-semibold text-white">Smart Reorder Recommendations</h3>
          <span className="text-sm text-slate-400">Calculated using 30-day velocity + EOQ</span>
        </div>
        <div className="overflow-x-auto">
          <table className="w-full text-sm text-left text-slate-300">
            <thead className="text-xs text-slate-400 uppercase bg-slate-900/50">
              <tr>
                <th className="px-6 py-3 font-medium">Product</th>
                <th className="px-6 py-3 font-medium">Priority</th>
                <th className="px-6 py-3 font-medium text-right">Current Stock</th>
                <th className="px-6 py-3 font-medium text-right text-blue-400">Recommended Qty</th>
                <th className="px-6 py-3 font-medium">Est. Stockout</th>
                <th className="px-6 py-3 font-medium">Preferred Supplier</th>
                <th className="px-6 py-3 text-right">Action</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-700/50">
              {recommendations.map((item, idx) => (
                <tr key={idx} className="hover:bg-slate-700/30 transition-colors">
                  <td className="px-6 py-4 font-medium text-white">{item.ProductName}</td>
                  <td className="px-6 py-4">{getPriorityBadge(item.Priority)}</td>
                  <td className="px-6 py-4 text-right">{item.CurrentStock}</td>
                  <td className="px-6 py-4 text-right font-bold text-blue-400">{item.RecommendedQuantity}</td>
                  <td className="px-6 py-4">{item.DaysUntilStockout} days</td>
                  <td className="px-6 py-4 text-slate-400">{item.SupplierName}</td>
                  <td className="px-6 py-4 text-right">
                    <button 
                      type="button"
                      onClick={() => handleAddToPO(item.ProductId)}
                      className={`font-medium text-xs border px-3 py-1 rounded transition-colors cursor-pointer ${
                        addedProductIds.includes(item.ProductId)
                          ? 'bg-emerald-950 text-emerald-300 border-emerald-600 font-bold'
                          : 'text-blue-500 hover:text-blue-400 border-blue-500/30 hover:border-blue-400'
                      }`}
                    >
                      {addedProductIds.includes(item.ProductId) ? '✓ In Draft PO' : 'Add to PO'}
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>

      {/* Confirmation Modal */}
      <Modal isOpen={showConfirmModal} onClose={() => !isGenerating && setShowConfirmModal(false)} title="Generate Draft Purchase Orders?">
        <div className="space-y-4">
          <p className="text-sm text-slate-300">
            Generate draft purchase orders for all low-stock items at or below their reorder point? Products that already have open or pending purchase orders will be automatically skipped.
          </p>
          <div className="flex justify-end gap-3 pt-3 border-t border-slate-700/60">
            <button
              type="button"
              onClick={() => setShowConfirmModal(false)}
              disabled={isGenerating}
              className="px-4 py-2 bg-slate-700 hover:bg-slate-600 text-slate-200 rounded-lg text-sm font-medium transition cursor-pointer disabled:opacity-50"
            >
              Cancel
            </button>
            <button
              type="button"
              onClick={handleConfirmGenerateDraftPOs}
              disabled={isGenerating}
              className="px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded-lg text-sm font-bold transition cursor-pointer shadow-lg shadow-blue-600/30 disabled:opacity-50 disabled:pointer-events-none"
            >
              {isGenerating ? 'Generating...' : 'Confirm & Generate'}
            </button>
          </div>
        </div>
      </Modal>
    </div>
  );
}
