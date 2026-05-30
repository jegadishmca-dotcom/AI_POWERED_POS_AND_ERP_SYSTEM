import React, { useState, useEffect } from 'react';
import { Sparkles, Loader2, Check, ArrowRight, RefreshCw, AlertCircle, ShoppingCart, Info, CheckSquare, Square, Search } from 'lucide-react';
import { getForecastReplenishment, generatePurchaseOrders, ForecastRecommendation } from '../api/ai.api';
import { Link } from 'react-router-dom';

export const AiForecaster = () => {
  const [recommendations, setRecommendations] = useState<ForecastRecommendation[]>([]);
  const [loading, setLoading] = useState(false);
  const [generating, setGenerating] = useState(false);
  const [selectedIds, setSelectedIds] = useState<string[]>([]);
  const [editedQuantities, setEditedQuantities] = useState<Record<string, number>>({});
  const [searchTerm, setSearchTerm] = useState('');
  const [successInfo, setSuccessInfo] = useState<{ poNumbers: string[] } | null>(null);
  const [error, setError] = useState<string | null>(null);

  const fetchRecommendations = async () => {
    try {
      setLoading(true);
      setError(null);
      setSuccessInfo(null);
      const data = await getForecastReplenishment();
      setRecommendations(data);
      setSelectedIds(data.map(r => r.productId)); // Select all by default
      
      // Initialize edited quantities
      const qtys: Record<string, number> = {};
      data.forEach(r => {
        qtys[r.productId] = r.recommendedOrderQty;
      });
      setEditedQuantities(qtys);
    } catch (err: any) {
      console.error(err);
      setError('Failed to fetch replenishment forecasts. Please ensure the backend is running.');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchRecommendations();
  }, []);

  const handleSelectToggle = (productId: string) => {
    setSelectedIds(prev =>
      prev.includes(productId)
        ? prev.filter(id => id !== productId)
        : [...prev, productId]
    );
  };

  const handleSelectAllToggle = () => {
    if (selectedIds.length === filteredRecs.length) {
      setSelectedIds([]);
    } else {
      setSelectedIds(filteredRecs.map(r => r.productId));
    }
  };

  const handleQtyChange = (productId: string, val: number) => {
    const parsed = Math.max(1, Math.floor(val));
    setEditedQuantities(prev => ({
      ...prev,
      [productId]: parsed
    }));
  };

  const handleGeneratePOs = async () => {
    if (selectedIds.length === 0) {
      alert('Please select at least one item to generate a Purchase Order.');
      return;
    }

    try {
      setGenerating(true);
      setError(null);
      
      const itemsToOrder = recommendations
        .filter(r => selectedIds.includes(r.productId))
        .map(r => ({
          productId: r.productId,
          supplierId: r.supplierId,
          quantity: editedQuantities[r.productId] || r.recommendedOrderQty,
          unitCost: r.unitCost
        }));

      const res = await generatePurchaseOrders(itemsToOrder);
      if (res.success) {
        setSuccessInfo({ poNumbers: res.poNumbers });
        // Refresh recommendations
        const updatedRecs = recommendations.filter(r => !selectedIds.includes(r.productId));
        setRecommendations(updatedRecs);
        setSelectedIds([]);
      }
    } catch (err: any) {
      console.error(err);
      setError(err?.response?.data || 'Failed to auto-generate purchase orders.');
    } finally {
      setGenerating(false);
    }
  };

  const filteredRecs = recommendations.filter(r => 
    r.productName.toLowerCase().includes(searchTerm.toLowerCase()) ||
    r.productCode.toLowerCase().includes(searchTerm.toLowerCase()) ||
    r.supplierName.toLowerCase().includes(searchTerm.toLowerCase())
  );

  const calculateTotalSelectedCost = () => {
    return recommendations
      .filter(r => selectedIds.includes(r.productId))
      .reduce((sum, r) => {
        const qty = editedQuantities[r.productId] ?? r.recommendedOrderQty;
        return sum + (qty * r.unitCost);
      }, 0);
  };

  return (
    <div className="max-w-7xl mx-auto p-6 space-y-6">
      {/* Title Header */}
      <div className="flex flex-col md:flex-row justify-between items-start md:items-center gap-4 bg-slate-900 border border-slate-800 p-6 rounded-2xl shadow-xl">
        <div className="flex items-center space-x-3">
          <div className="w-12 h-12 bg-gradient-to-tr from-indigo-600 to-violet-500 rounded-xl flex items-center justify-center shadow-lg shadow-indigo-600/30">
            <Sparkles className="w-6 h-6 text-white animate-pulse" />
          </div>
          <div>
            <h2 className="text-xl font-extrabold text-white">AI Demand Forecaster & Replenishment</h2>
            <p className="text-xs text-indigo-400 font-semibold flex items-center gap-1.5 mt-0.5">
              <span>Automatic 15-day stock demand prediction + safety buffer</span>
            </p>
          </div>
        </div>
        <div className="flex items-center gap-2">
          <button 
            onClick={fetchRecommendations}
            disabled={loading}
            className="p-2.5 bg-slate-800 text-slate-300 hover:text-white rounded-xl border border-slate-700 transition flex items-center"
            title="Refresh Forecast Data"
          >
            <RefreshCw className={`w-4 h-4 ${loading ? 'animate-spin text-indigo-400' : ''}`} />
          </button>
        </div>
      </div>

      {/* Equation Explainer Card */}
      <div className="bg-slate-950 border border-slate-800 p-4 rounded-xl flex items-start gap-3 text-slate-350 text-xs">
        <Info className="w-5 h-5 text-indigo-400 shrink-0 mt-0.5" />
        <div className="space-y-1">
          <p className="font-bold text-slate-200">How replenishment logic works:</p>
          <p className="leading-relaxed">
            The AI analyzes total sales velocity over the last 30 days to compute the average daily sales. 
            It predicts stock requirements for a <span className="text-indigo-300 font-semibold">15-day restock cycle</span> plus a <span className="text-indigo-300 font-semibold">5-day safety buffer</span>.
            If the predicted demand exceeds the current stock levels, it suggests an order quantity to balance the deficit.
          </p>
        </div>
      </div>

      {/* Main Flow: Alerts */}
      {successInfo && (
        <div className="bg-emerald-950/40 border border-emerald-500/30 p-5 rounded-xl space-y-3 animate-in fade-in duration-300">
          <div className="flex items-center gap-2 text-emerald-400 font-extrabold text-sm">
            <Check className="w-5 h-5 bg-emerald-500/20 rounded-full p-0.5" />
            <span>Success! Draft Purchase Orders generated successfully.</span>
          </div>
          <p className="text-xs text-emerald-300/80">
            We grouped the products by supplier and created draft purchase orders for review:
          </p>
          <div className="flex flex-wrap gap-2 pt-1">
            {successInfo.poNumbers.map((poNum, idx) => (
              <span key={idx} className="px-3 py-1.5 bg-slate-900 border border-emerald-500/20 text-emerald-300 rounded-lg text-xs font-mono font-bold">
                {poNum}
              </span>
            ))}
          </div>
          <div className="pt-2">
            <Link 
              to="/purchase-orders" 
              className="inline-flex items-center gap-1 text-xs font-bold text-emerald-400 hover:text-emerald-300 hover:underline"
            >
              Go to Purchase Orders list <ArrowRight className="w-3.5 h-3.5" />
            </Link>
          </div>
        </div>
      )}

      {error && (
        <div className="bg-rose-950/40 border border-rose-500/30 p-4 rounded-xl flex items-center gap-3 text-rose-300 text-xs font-bold">
          <AlertCircle className="w-5 h-5 text-rose-400 shrink-0" />
          <span>{error}</span>
        </div>
      )}

      {/* Filters & Actions Panel */}
      <div className="bg-white shadow-md border border-slate-100 rounded-xl p-5 flex flex-col md:flex-row justify-between items-center gap-4">
        {/* Search */}
        <div className="relative w-full md:w-80">
          <Search className="absolute left-3.5 top-3.5 w-4 h-4 text-slate-400" />
          <input
            type="text"
            className="w-full pl-10 pr-4 py-2.5 border border-slate-200 focus:border-indigo-500 rounded-xl text-slate-800 text-sm font-semibold outline-none transition"
            placeholder="Search by product, supplier..."
            value={searchTerm}
            onChange={(e) => setSearchTerm(e.target.value)}
          />
        </div>

        {/* Selected Items summary & CTA */}
        <div className="flex flex-col sm:flex-row items-center gap-4 w-full md:w-auto">
          <div className="text-center sm:text-right shrink-0">
            <span className="text-xs text-slate-450 block font-bold">
              {selectedIds.length} of {filteredRecs.length} items selected
            </span>
            <span className="text-sm font-black text-slate-800 block mt-0.5">
              Total Order: ₹{calculateTotalSelectedCost().toLocaleString('en-IN', { minimumFractionDigits: 2 })}
            </span>
          </div>
          <button
            onClick={handleGeneratePOs}
            disabled={generating || selectedIds.length === 0 || loading}
            className="w-full sm:w-auto px-6 py-3 bg-indigo-600 hover:bg-indigo-700 text-white font-bold rounded-xl shadow-lg shadow-indigo-600/10 transition-all flex items-center justify-center gap-2 disabled:opacity-40 disabled:hover:bg-indigo-600"
          >
            {generating ? (
              <>
                <Loader2 className="w-4 h-4 animate-spin text-white" />
                <span>Generating Draft POs...</span>
              </>
            ) : (
              <>
                <ShoppingCart className="w-4 h-4" />
                <span>Auto-Generate Draft POs</span>
              </>
            )}
          </button>
        </div>
      </div>

      {/* Forecast Recommendations Table */}
      <div className="bg-white border border-slate-150 rounded-xl shadow-md overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full text-left border-collapse">
            <thead className="bg-slate-550 border-b border-slate-200 text-slate-650 text-xs font-bold uppercase">
              <tr>
                <th className="p-4 w-12 text-center">
                  <button 
                    onClick={handleSelectAllToggle}
                    className="p-1 rounded hover:bg-slate-100 transition inline-block text-indigo-600"
                  >
                    {selectedIds.length === filteredRecs.length && filteredRecs.length > 0 ? (
                      <CheckSquare className="w-5 h-5" />
                    ) : (
                      <Square className="w-5 h-5 text-slate-400" />
                    )}
                  </button>
                </th>
                <th className="p-4">Product Details</th>
                <th className="p-4 text-center">Sales Velocity (30d)</th>
                <th className="p-4 text-center">Forecast Demand (15d)</th>
                <th className="p-4 text-center">Current Stock</th>
                <th className="p-4 w-40 text-center">Recommended Order</th>
                <th className="p-4">Preferred Supplier</th>
                <th className="p-4 text-right">Unit Cost</th>
                <th className="p-4 text-right">Total Est. Cost</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100 text-sm">
              {loading ? (
                <tr>
                  <td colSpan={9} className="p-12 text-center">
                    <div className="flex flex-col items-center justify-center space-y-2 text-slate-500 font-bold">
                      <Loader2 className="w-8 h-8 animate-spin text-indigo-500" />
                      <span>Computing AI demand projections...</span>
                    </div>
                  </td>
                </tr>
              ) : filteredRecs.length === 0 ? (
                <tr>
                  <td colSpan={9} className="p-12 text-center text-slate-500 font-semibold">
                    No replenishment recommendations at the moment. All stocks are healthy!
                  </td>
                </tr>
              ) : (
                filteredRecs.map((rec) => {
                  const isSelected = selectedIds.includes(rec.productId);
                  const orderQty = editedQuantities[rec.productId] ?? rec.recommendedOrderQty;
                  const itemTotalCost = orderQty * rec.unitCost;

                  return (
                    <tr 
                      key={rec.productId} 
                      className={`hover:bg-slate-50/80 transition-colors ${isSelected ? 'bg-indigo-50/20' : ''}`}
                    >
                      {/* Checkbox */}
                      <td className="p-4 text-center">
                        <button 
                          onClick={() => handleSelectToggle(rec.productId)}
                          className="p-1 rounded hover:bg-slate-100 transition inline-block text-indigo-650"
                        >
                          {isSelected ? (
                            <CheckSquare className="w-5 h-5" />
                          ) : (
                            <Square className="w-5 h-5 text-slate-350" />
                          )}
                        </button>
                      </td>

                      {/* Product details */}
                      <td className="p-4">
                        <div className="font-bold text-slate-800">{rec.productName}</div>
                        <div className="text-xs text-slate-450 font-semibold font-mono mt-0.5">{rec.productCode}</div>
                      </td>

                      {/* Avg Daily Sales */}
                      <td className="p-4 text-center">
                        <div className="font-bold text-slate-700">{rec.avgDailySales} / day</div>
                        <div className="text-[10px] text-slate-450 font-semibold mt-0.5">30-day velocity</div>
                      </td>

                      {/* Forecasted Demand */}
                      <td className="p-4 text-center">
                        <div className="font-bold text-indigo-600">{rec.forecastedDemand} units</div>
                        <div className="text-[10px] text-slate-450 font-semibold mt-0.5">+5d safety stock</div>
                      </td>

                      {/* Current stock */}
                      <td className="p-4 text-center">
                        <span className={`px-2 py-1 rounded text-xs font-extrabold ${rec.currentStock < 10 ? 'bg-red-50 text-red-700 border border-red-100' : 'bg-slate-100 text-slate-700'}`}>
                          {rec.currentStock} units
                        </span>
                      </td>

                      {/* Recommended Qty (Input editable) */}
                      <td className="p-4 text-center">
                        <input
                          type="number"
                          min="1"
                          className="w-24 px-2 py-1.5 border border-slate-200 focus:border-indigo-500 text-center font-bold text-slate-800 rounded-lg outline-none transition"
                          value={orderQty}
                          onChange={(e) => handleQtyChange(rec.productId, parseInt(e.target.value) || 0)}
                        />
                      </td>

                      {/* Supplier */}
                      <td className="p-4">
                        <div className="font-bold text-slate-750">{rec.supplierName}</div>
                      </td>

                      {/* Unit Cost */}
                      <td className="p-4 text-right font-semibold text-slate-600">
                        ₹{rec.unitCost.toFixed(2)}
                      </td>

                      {/* Total cost */}
                      <td className="p-4 text-right font-black text-slate-850">
                        ₹{itemTotalCost.toLocaleString('en-IN', { minimumFractionDigits: 2 })}
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
  );
};
