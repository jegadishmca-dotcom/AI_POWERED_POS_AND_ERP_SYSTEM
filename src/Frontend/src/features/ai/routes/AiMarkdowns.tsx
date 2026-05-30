import React, { useState, useEffect } from 'react';
import { Sparkles, Loader2, Check, RefreshCw, AlertCircle, TrendingDown, Info, ShieldCheck, CheckSquare, Square, Search, Edit3 } from 'lucide-react';
import { getNearExpiryMarkdowns, applyMarkdowns, ExpiryMarkdown } from '../api/ai.api';
import { Link } from 'react-router-dom';

export const AiMarkdowns = () => {
  const [markdowns, setMarkdowns] = useState<ExpiryMarkdown[]>([]);
  const [loading, setLoading] = useState(false);
  const [applying, setApplying] = useState(false);
  const [selectedKeys, setSelectedKeys] = useState<string[]>([]); // "productId:batchId"
  const [editedPrices, setEditedPrices] = useState<Record<string, number>>({}); // key: "productId:batchId" -> newPrice
  const [editedDiscounts, setEditedDiscounts] = useState<Record<string, number>>({}); // key: "productId:batchId" -> discountPercent
  const [searchTerm, setSearchTerm] = useState('');
  const [success, setSuccess] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const fetchMarkdowns = async () => {
    try {
      setLoading(true);
      setError(null);
      setSuccess(false);
      const data = await getNearExpiryMarkdowns();
      setMarkdowns(data);
      
      // Select all by default
      const keys = data.map(m => `${m.productId}:${m.batchId}`);
      setSelectedKeys(keys);
      
      // Initialize edited prices and discounts
      const prices: Record<string, number> = {};
      const discounts: Record<string, number> = {};
      data.forEach(m => {
        const key = `${m.productId}:${m.batchId}`;
        prices[key] = m.suggestedPrice;
        discounts[key] = m.discountPercent;
      });
      setEditedPrices(prices);
      setEditedDiscounts(discounts);
    } catch (err: any) {
      console.error(err);
      setError('Failed to fetch expiry markdowns. Please ensure the database has active batches expiring within 30 days.');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchMarkdowns();
  }, []);

  const handleSelectToggle = (key: string) => {
    setSelectedKeys(prev =>
      prev.includes(key)
        ? prev.filter(k => k !== key)
        : [...prev, key]
    );
  };

  const handleSelectAllToggle = () => {
    const visibleKeys = filteredMarkdowns.map(m => `${m.productId}:${m.batchId}`);
    const allVisibleSelected = visibleKeys.every(k => selectedKeys.includes(k));

    if (allVisibleSelected) {
      setSelectedKeys(prev => prev.filter(k => !visibleKeys.includes(k)));
    } else {
      setSelectedKeys(prev => {
        const union = new Set([...prev, ...visibleKeys]);
        return Array.from(union);
      });
    }
  };

  // When discount changes, update price
  const handleDiscountChange = (markdown: ExpiryMarkdown, discountVal: number) => {
    const key = `${markdown.productId}:${markdown.batchId}`;
    const disc = Math.min(100, Math.max(0, parseFloat(discountVal.toFixed(1)) || 0));
    const newPrice = Math.round(markdown.originalPrice * (1.0 - (disc / 100.0)) * 100) / 100;
    
    setEditedDiscounts(prev => ({ ...prev, [key]: disc }));
    setEditedPrices(prev => ({ ...prev, [key]: newPrice }));
  };

  // When price changes, update discount
  const handlePriceChange = (markdown: ExpiryMarkdown, priceVal: number) => {
    const key = `${markdown.productId}:${markdown.batchId}`;
    const price = Math.min(markdown.originalPrice, Math.max(0, parseFloat(priceVal.toFixed(2)) || 0));
    
    let disc = 0;
    if (markdown.originalPrice > 0) {
      disc = Math.round(((markdown.originalPrice - price) / markdown.originalPrice) * 1000) / 10;
    }

    setEditedPrices(prev => ({ ...prev, [key]: price }));
    setEditedDiscounts(prev => ({ ...prev, [key]: disc }));
  };

  const handleApplyMarkdowns = async () => {
    if (selectedKeys.length === 0) {
      alert('Please select at least one batch to apply markdown pricing.');
      return;
    }

    try {
      setApplying(true);
      setError(null);

      const payload = markdowns
        .filter(m => selectedKeys.includes(`${m.productId}:${m.batchId}`))
        .map(m => {
          const key = `${m.productId}:${m.batchId}`;
          return {
            productId: m.productId,
            batchId: m.batchId,
            newPrice: editedPrices[key] ?? m.suggestedPrice
          };
        });

      const res = await applyMarkdowns(payload);
      if (res.success) {
        setSuccess(true);
        // Refresh markdowns
        const updated = markdowns.filter(m => !selectedKeys.includes(`${m.productId}:${m.batchId}`));
        setMarkdowns(updated);
        setSelectedKeys([]);
      }
    } catch (err: any) {
      console.error(err);
      setError(err?.response?.data || 'Failed to apply markdown prices.');
    } finally {
      setApplying(false);
    }
  };

  const filteredMarkdowns = markdowns.filter(m => 
    m.productName.toLowerCase().includes(searchTerm.toLowerCase()) ||
    m.productCode.toLowerCase().includes(searchTerm.toLowerCase()) ||
    m.batchNumber.toLowerCase().includes(searchTerm.toLowerCase())
  );

  return (
    <div className="max-w-7xl mx-auto p-6 space-y-6">
      {/* Title Header */}
      <div className="flex flex-col md:flex-row justify-between items-start md:items-center gap-4 bg-slate-900 border border-slate-800 p-6 rounded-2xl shadow-xl">
        <div className="flex items-center space-x-3">
          <div className="w-12 h-12 bg-gradient-to-tr from-indigo-600 to-violet-500 rounded-xl flex items-center justify-center shadow-lg shadow-indigo-600/30">
            <TrendingDown className="w-6 h-6 text-white animate-pulse" />
          </div>
          <div>
            <h2 className="text-xl font-extrabold text-white">AI Near-Expiry Markdown Pricing</h2>
            <p className="text-xs text-indigo-400 font-semibold flex items-center gap-1.5 mt-0.5">
              <span>Detect expiring batches & apply targeted discounts to accelerate sell-through</span>
            </p>
          </div>
        </div>
        <div className="flex items-center gap-2">
          <button 
            onClick={fetchMarkdowns}
            disabled={loading}
            className="p-2.5 bg-slate-800 text-slate-300 hover:text-white rounded-xl border border-slate-700 transition flex items-center"
            title="Refresh Markdown Listings"
          >
            <RefreshCw className={`w-4 h-4 ${loading ? 'animate-spin text-indigo-400' : ''}`} />
          </button>
        </div>
      </div>

      {/* Rules Explainer Card */}
      <div className="bg-slate-950 border border-slate-800 p-4 rounded-xl flex items-start gap-3 text-slate-350 text-xs">
        <Info className="w-5 h-5 text-indigo-400 shrink-0 mt-0.5" />
        <div className="space-y-1">
          <p className="font-bold text-slate-200">Tiered Near-Expiry Pricing Strategy:</p>
          <div className="grid grid-cols-1 sm:grid-cols-3 gap-2 pt-1 font-semibold text-slate-300">
            <div className="bg-slate-900 p-2 rounded border border-indigo-500/10">
              <span className="text-amber-400">15-30 Days Remaining:</span>
              <p className="text-[10px] text-slate-400 font-normal mt-0.5">Suggests a 10% markdown discount</p>
            </div>
            <div className="bg-slate-900 p-2 rounded border border-indigo-500/10">
              <span className="text-orange-400">7-14 Days Remaining:</span>
              <p className="text-[10px] text-slate-400 font-normal mt-0.5">Suggests a 25% markdown discount</p>
            </div>
            <div className="bg-slate-900 p-2 rounded border border-indigo-500/10">
              <span className="text-red-400">&lt; 7 Days Remaining:</span>
              <p className="text-[10px] text-slate-400 font-normal mt-0.5">Suggests a 50% markdown discount</p>
            </div>
          </div>
        </div>
      </div>

      {/* Success Notification */}
      {success && (
        <div className="bg-emerald-950/40 border border-emerald-500/30 p-4 rounded-xl flex items-center gap-3 text-emerald-300 text-xs font-bold animate-in fade-in duration-300">
          <ShieldCheck className="w-5 h-5 text-emerald-400 shrink-0" />
          <span>Success! Markdown pricing applied. Selected products' catalog selling prices have been updated! Check the Product Catalog.</span>
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
            placeholder="Search by product, batch..."
            value={searchTerm}
            onChange={(e) => setSearchTerm(e.target.value)}
          />
        </div>

        {/* Selected count & apply button */}
        <div className="flex flex-col sm:flex-row items-center gap-4 w-full md:w-auto">
          <div className="text-center sm:text-right shrink-0">
            <span className="text-xs text-slate-450 font-bold block">
              {selectedKeys.length} of {filteredMarkdowns.length} batches selected
            </span>
            <span className="text-xs text-slate-400 block mt-0.5">
              Updates will change base product selling price
            </span>
          </div>
          <button
            onClick={handleApplyMarkdowns}
            disabled={applying || selectedKeys.length === 0 || loading}
            className="w-full sm:w-auto px-6 py-3 bg-indigo-605 bg-indigo-605 bg-indigo-600 hover:bg-indigo-700 text-white font-bold rounded-xl shadow-lg shadow-indigo-600/10 transition-all flex items-center justify-center gap-2 disabled:opacity-40 disabled:hover:bg-indigo-600"
          >
            {applying ? (
              <>
                <Loader2 className="w-4 h-4 animate-spin text-white" />
                <span>Applying Markdowns...</span>
              </>
            ) : (
              <>
                <Check className="w-4 h-4" />
                <span>Apply Markdown Prices</span>
              </>
            )}
          </button>
        </div>
      </div>

      {/* Near-Expiry Markdown Table */}
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
                    {filteredMarkdowns.length > 0 && filteredMarkdowns.every(m => selectedKeys.includes(`${m.productId}:${m.batchId}`)) ? (
                      <CheckSquare className="w-5 h-5" />
                    ) : (
                      <Square className="w-5 h-5 text-slate-400" />
                    )}
                  </button>
                </th>
                <th className="p-4">Product details</th>
                <th className="p-4">Batch Number</th>
                <th className="p-4 text-center">Expiry Date</th>
                <th className="p-4 text-center">Shelf Life Remaining</th>
                <th className="p-4 text-center">Current Stock</th>
                <th className="p-4 text-right">Original Price</th>
                <th className="p-4 text-center w-28">Discount (%)</th>
                <th className="p-4 text-right w-36">New Selling Price</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100 text-sm">
              {loading ? (
                <tr>
                  <td colSpan={9} className="p-12 text-center">
                    <div className="flex flex-col items-center justify-center space-y-2 text-slate-500 font-bold">
                      <Loader2 className="w-8 h-8 animate-spin text-indigo-500" />
                      <span>Identifying expiring batches & strategies...</span>
                    </div>
                  </td>
                </tr>
              ) : filteredMarkdowns.length === 0 ? (
                <tr>
                  <td colSpan={9} className="p-12 text-center text-slate-500 font-semibold">
                    No batches expiring within the next 30 days. Perfect inventory rotation!
                  </td>
                </tr>
              ) : (
                filteredMarkdowns.map((m) => {
                  const key = `${m.productId}:${m.batchId}`;
                  const isSelected = selectedKeys.includes(key);
                  const price = editedPrices[key] ?? m.suggestedPrice;
                  const discount = editedDiscounts[key] ?? m.discountPercent;

                  // Expiry badge color logic
                  let daysColor = 'bg-emerald-50 text-emerald-700 border-emerald-100';
                  if (m.daysLeft < 7) {
                    daysColor = 'bg-rose-50 text-rose-700 border border-rose-100 font-black animate-pulse';
                  } else if (m.daysLeft <= 14) {
                    daysColor = 'bg-orange-50 text-orange-700 border border-orange-100';
                  } else if (m.daysLeft <= 30) {
                    daysColor = 'bg-amber-50 text-amber-700 border border-amber-100';
                  }

                  return (
                    <tr 
                      key={key} 
                      className={`hover:bg-slate-50/80 transition-colors ${isSelected ? 'bg-indigo-50/20' : ''}`}
                    >
                      {/* Checkbox */}
                      <td className="p-4 text-center">
                        <button 
                          onClick={() => handleSelectToggle(key)}
                          className="p-1 rounded hover:bg-slate-100 transition inline-block text-indigo-655"
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
                        <div className="font-bold text-slate-800">{m.productName}</div>
                        <div className="text-xs text-slate-450 font-semibold font-mono mt-0.5">{m.productCode}</div>
                      </td>

                      {/* Batch Number */}
                      <td className="p-4 font-semibold text-slate-700">
                        {m.batchNumber}
                      </td>

                      {/* Expiry Date */}
                      <td className="p-4 text-center font-bold text-slate-600 font-mono">
                        {m.expiryDate}
                      </td>

                      {/* Shelf life remaining */}
                      <td className="p-4 text-center">
                        <span className={`px-2.5 py-1.5 rounded-lg text-xs font-bold ${daysColor}`}>
                          {m.daysLeft} days left
                        </span>
                      </td>

                      {/* Current batch stock */}
                      <td className="p-4 text-center">
                        <span className="px-2 py-1 bg-slate-100 text-slate-700 rounded font-semibold text-xs">
                          {m.currentStock} units
                        </span>
                      </td>

                      {/* Original Price */}
                      <td className="p-4 text-right font-semibold text-slate-500 line-through">
                        ₹{m.originalPrice.toFixed(2)}
                      </td>

                      {/* Discount input (%) */}
                      <td className="p-4 text-center">
                        <div className="relative inline-block">
                          <input
                            type="number"
                            min="0"
                            max="100"
                            step="0.5"
                            className="w-20 pl-2 pr-5 py-1.5 border border-slate-200 focus:border-indigo-500 text-center font-bold text-slate-800 rounded-lg outline-none transition"
                            value={discount}
                            onChange={(e) => handleDiscountChange(m, parseFloat(e.target.value) || 0)}
                          />
                          <span className="absolute right-2 top-2 text-xs font-bold text-slate-400">%</span>
                        </div>
                      </td>

                      {/* New Selling Price input */}
                      <td className="p-4 text-right">
                        <div className="relative inline-flex items-center justify-end">
                          <span className="absolute left-2.5 text-xs font-bold text-slate-400">₹</span>
                          <input
                            type="number"
                            min="0"
                            step="0.01"
                            className="w-28 pl-6 pr-2 py-1.5 border border-slate-205 focus:border-indigo-500 text-right font-extrabold text-indigo-750 bg-indigo-50/10 rounded-lg outline-none transition"
                            value={price}
                            onChange={(e) => handlePriceChange(m, parseFloat(e.target.value) || 0)}
                          />
                        </div>
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
