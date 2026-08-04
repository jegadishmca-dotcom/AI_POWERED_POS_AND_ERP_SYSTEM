import React, { useState, useRef, useEffect } from 'react';
import {
  Tag, Search, TrendingUp, History, AlertTriangle, CheckCircle2,
  Loader2, IndianRupee, ChevronDown, X, ArrowRight, Info, Package
} from 'lucide-react';
import { searchProducts } from '../../catalog/api/catalog.api';
import { submitPriceChange, getProductPriceHistory } from '../../settings/api/settings.api';

// ── Types ─────────────────────────────────────────────────────────────────────

interface ProductSearchResult {
  id: string;
  name: string;
  barcode?: string;
  sellingPrice: number;
  mrp: number;
  productCode?: string;
}

interface PriceHistoryBatch {
  id: string;
  batchNumber: string;
  mrp: number;
  costPrice: number;
  availableQuantity: number;
  grnReference?: string;
  createdAt: string;
  isActive: boolean;
  isPriceChangeBatch: boolean;
}

interface PriceHistory {
  productId: string;
  productName: string;
  currentMrp: number;
  currentSellingPrice: number;
  batches: PriceHistoryBatch[];
}

// ── Main Component ─────────────────────────────────────────────────────────────

export const PriceChangeModule: React.FC = () => {
  // Product search
  const [searchQuery, setSearchQuery] = useState('');
  const [searchResults, setSearchResults] = useState<ProductSearchResult[]>([]);
  const [searchLoading, setSearchLoading] = useState(false);
  const [showDropdown, setShowDropdown] = useState(false);
  const [selectedProduct, setSelectedProduct] = useState<ProductSearchResult | null>(null);
  const searchRef = useRef<HTMLDivElement>(null);
  const searchDebounce = useRef<ReturnType<typeof setTimeout> | null>(null);

  // Price history
  const [priceHistory, setPriceHistory] = useState<PriceHistory | null>(null);
  const [historyLoading, setHistoryLoading] = useState(false);

  // Form fields
  const [newMrp, setNewMrp] = useState('');
  const [newSellingPrice, setNewSellingPrice] = useState('');
  const [reason, setReason] = useState('');

  // Submit state
  const [confirming, setConfirming] = useState(false);
  const [saving, setSaving] = useState(false);
  const [message, setMessage] = useState<{ text: string; type: 'success' | 'error' } | null>(null);

  // Close dropdown on outside click
  useEffect(() => {
    const handler = (e: MouseEvent) => {
      if (searchRef.current && !searchRef.current.contains(e.target as Node)) {
        setShowDropdown(false);
      }
    };
    document.addEventListener('mousedown', handler);
    return () => document.removeEventListener('mousedown', handler);
  }, []);

  // ── Product Search ───────────────────────────────────────────────────────────

  const handleSearchChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const q = e.target.value;
    setSearchQuery(q);
    setSelectedProduct(null);
    setPriceHistory(null);
    setNewMrp('');
    setNewSellingPrice('');
    setReason('');
    setMessage(null);

    if (searchDebounce.current) clearTimeout(searchDebounce.current);
    if (!q.trim()) { setSearchResults([]); setShowDropdown(false); return; }

    searchDebounce.current = setTimeout(async () => {
      setSearchLoading(true);
      try {
        const results = await searchProducts(q, 10);
        setSearchResults(results);
        setShowDropdown(results.length > 0);
      } catch {
        setSearchResults([]);
      } finally {
        setSearchLoading(false);
      }
    }, 300);
  };

  const handleSelectProduct = async (product: ProductSearchResult) => {
    setSelectedProduct(product);
    setSearchQuery(product.name);
    setShowDropdown(false);
    setNewMrp(product.mrp?.toString() || '');
    setNewSellingPrice(product.sellingPrice?.toString() || '');
    setMessage(null);

    setHistoryLoading(true);
    try {
      const history = await getProductPriceHistory(product.id);
      setPriceHistory(history as any);
    } catch {
      setPriceHistory(null);
    } finally {
      setHistoryLoading(false);
    }
  };

  const handleClearProduct = () => {
    setSelectedProduct(null);
    setSearchQuery('');
    setSearchResults([]);
    setPriceHistory(null);
    setNewMrp('');
    setNewSellingPrice('');
    setReason('');
    setMessage(null);
    setConfirming(false);
  };

  // ── Validation ────────────────────────────────────────────────────────────────

  const parsedMrp = parseFloat(newMrp);
  const parsedSP = parseFloat(newSellingPrice);
  const isValid =
    selectedProduct !== null &&
    !isNaN(parsedMrp) && parsedMrp > 0 &&
    !isNaN(parsedSP) && parsedSP > 0 &&
    parsedSP <= parsedMrp &&
    reason.trim().length >= 3;

  // ── Submit ────────────────────────────────────────────────────────────────────

  const handleSave = async () => {
    if (!selectedProduct || !isValid) return;
    setSaving(true);
    setMessage(null);
    try {
      await submitPriceChange({
        productId: selectedProduct.id,
        newMrp: parsedMrp,
        newSellingPrice: parsedSP,
        reason: reason.trim(),
      });
      setMessage({
        text: `✓ Price updated for "${selectedProduct.name}". New MRP: ₹${parsedMrp.toFixed(2)}, Selling Price: ₹${parsedSP.toFixed(2)}. Changes are live at POS immediately.`,
        type: 'success',
      });
      setConfirming(false);
      // Refresh history
      const history = await getProductPriceHistory(selectedProduct.id);
      setPriceHistory(history as any);
    } catch (err: any) {
      setMessage({
        text: err?.response?.data?.message || 'Failed to save price change. Please try again.',
        type: 'error',
      });
      setConfirming(false);
    } finally {
      setSaving(false);
    }
  };

  // ── Render ───────────────────────────────────────────────────────────────────

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="bg-white p-6 rounded-xl border border-slate-100 shadow-sm">
        <div className="flex items-center gap-3 mb-1">
          <div className="p-2 bg-violet-50 text-violet-600 rounded-lg">
            <Tag className="w-5 h-5" />
          </div>
          <div>
            <h3 className="font-bold text-slate-800">Price Change Module</h3>
            <p className="text-xs text-slate-400">
              Update product pricing by adding a new price record. Changes take effect at POS immediately.
            </p>
          </div>
        </div>

        {/* Cart-Price Freeze Notice */}
        <div className="mt-4 flex items-start gap-2 bg-amber-50 border border-amber-200 rounded-lg p-3">
          <Info className="w-4 h-4 text-amber-600 mt-0.5 shrink-0" />
          <p className="text-xs text-amber-700 font-medium">
            <strong>Cart Price Behaviour:</strong> Items already added to an open billing cart retain the price at
            which they were scanned. The cashier must remove and re-scan the item to apply the new price.
            This is by design — it prevents mid-transaction price surprises.
          </p>
        </div>
      </div>

      {/* Product Search */}
      <div className="bg-white p-6 rounded-xl border border-slate-100 shadow-sm">
        <label className="block text-sm font-bold text-slate-700 mb-2">
          Search Product
        </label>
        <div className="relative" ref={searchRef}>
          <div className="relative">
            <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-400" />
            <input
              id="price-change-product-search"
              type="text"
              value={searchQuery}
              onChange={handleSearchChange}
              placeholder="Type product name or barcode..."
              className="w-full pl-10 pr-10 py-2.5 border border-slate-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-violet-500 focus:border-violet-400"
            />
            {selectedProduct && (
              <button
                onClick={handleClearProduct}
                className="absolute right-3 top-1/2 -translate-y-1/2 text-slate-400 hover:text-slate-600"
              >
                <X className="w-4 h-4" />
              </button>
            )}
            {searchLoading && !selectedProduct && (
              <Loader2 className="absolute right-3 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-400 animate-spin" />
            )}
          </div>

          {/* Dropdown */}
          {showDropdown && searchResults.length > 0 && (
            <div className="absolute z-50 w-full mt-1 bg-white border border-slate-200 rounded-xl shadow-xl overflow-hidden">
              {searchResults.map((product) => (
                <button
                  key={product.id}
                  onClick={() => handleSelectProduct(product)}
                  className="w-full flex items-center gap-3 px-4 py-3 hover:bg-violet-50 transition-colors text-left border-b border-slate-50 last:border-0"
                >
                  <Package className="w-4 h-4 text-slate-400 shrink-0" />
                  <div className="flex-1 min-w-0">
                    <p className="text-sm font-semibold text-slate-800 truncate">{product.name}</p>
                    <p className="text-xs text-slate-400">{product.barcode || product.productCode || '—'}</p>
                  </div>
                  <div className="text-right shrink-0">
                    <p className="text-xs font-bold text-violet-700">₹{(product.sellingPrice || 0).toFixed(2)}</p>
                    <p className="text-xs text-slate-400">MRP ₹{(product.mrp || 0).toFixed(2)}</p>
                  </div>
                </button>
              ))}
            </div>
          )}
        </div>
      </div>

      {/* Price Change Form — shown after product selected */}
      {selectedProduct && (
        <>
          {/* Current Prices & Form */}
          <div className="bg-white p-6 rounded-xl border border-slate-100 shadow-sm">
            <div className="flex items-center gap-2 mb-5">
              <TrendingUp className="w-4 h-4 text-violet-600" />
              <h4 className="font-bold text-slate-700 text-sm">Update Price for: <span className="text-violet-700">{selectedProduct.name}</span></h4>
            </div>

            {/* Current → New Price visual */}
            <div className="grid grid-cols-1 md:grid-cols-2 gap-4 mb-6">
              {/* MRP */}
              <div>
                <label className="block text-xs font-bold text-slate-600 mb-1.5 uppercase tracking-wide">
                  Current MRP → New MRP
                </label>
                <div className="flex items-center gap-2">
                  <div className="flex-1 bg-slate-50 border border-slate-200 rounded-lg px-3 py-2.5 text-sm font-bold text-slate-500 select-none">
                    ₹{(priceHistory?.currentMrp ?? selectedProduct.mrp).toFixed(2)}
                  </div>
                  <ArrowRight className="w-4 h-4 text-slate-400 shrink-0" />
                  <div className="flex-1 relative">
                    <IndianRupee className="absolute left-3 top-1/2 -translate-y-1/2 w-3.5 h-3.5 text-slate-400" />
                    <input
                      id="price-change-new-mrp"
                      type="number"
                      min="0.01"
                      step="0.01"
                      value={newMrp}
                      onChange={(e) => setNewMrp(e.target.value)}
                      className="w-full pl-8 pr-3 py-2.5 border border-violet-300 rounded-lg text-sm font-bold text-violet-800 focus:outline-none focus:ring-2 focus:ring-violet-500 bg-violet-50"
                      placeholder="0.00"
                    />
                  </div>
                </div>
              </div>

              {/* Selling Price */}
              <div>
                <label className="block text-xs font-bold text-slate-600 mb-1.5 uppercase tracking-wide">
                  Current Selling Price → New Selling Price
                </label>
                <div className="flex items-center gap-2">
                  <div className="flex-1 bg-slate-50 border border-slate-200 rounded-lg px-3 py-2.5 text-sm font-bold text-slate-500 select-none">
                    ₹{(priceHistory?.currentSellingPrice ?? selectedProduct.sellingPrice).toFixed(2)}
                  </div>
                  <ArrowRight className="w-4 h-4 text-slate-400 shrink-0" />
                  <div className="flex-1 relative">
                    <IndianRupee className="absolute left-3 top-1/2 -translate-y-1/2 w-3.5 h-3.5 text-slate-400" />
                    <input
                      id="price-change-new-selling-price"
                      type="number"
                      min="0.01"
                      step="0.01"
                      value={newSellingPrice}
                      onChange={(e) => setNewSellingPrice(e.target.value)}
                      className="w-full pl-8 pr-3 py-2.5 border border-violet-300 rounded-lg text-sm font-bold text-violet-800 focus:outline-none focus:ring-2 focus:ring-violet-500 bg-violet-50"
                      placeholder="0.00"
                    />
                  </div>
                </div>
                {!isNaN(parsedSP) && !isNaN(parsedMrp) && parsedSP > parsedMrp && (
                  <p className="mt-1 text-xs text-red-600 font-medium flex items-center gap-1">
                    <AlertTriangle className="w-3 h-3" /> Selling price cannot exceed MRP
                  </p>
                )}
              </div>
            </div>

            {/* Reason */}
            <div className="mb-5">
              <label className="block text-xs font-bold text-slate-600 mb-1.5 uppercase tracking-wide">
                Reason for Price Change <span className="text-red-500">*</span>
              </label>
              <input
                id="price-change-reason"
                type="text"
                value={reason}
                onChange={(e) => setReason(e.target.value)}
                className="w-full border border-slate-200 rounded-lg px-3 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-violet-500"
                placeholder="e.g. Supplier rate revision, seasonal promotion, GST change..."
              />
            </div>

            {/* Status message */}
            {message && (
              <div className={`mb-4 flex items-start gap-3 p-4 rounded-xl border text-sm font-medium ${
                message.type === 'success'
                  ? 'bg-emerald-50 border-emerald-200 text-emerald-800'
                  : 'bg-red-50 border-red-200 text-red-700'
              }`}>
                {message.type === 'success'
                  ? <CheckCircle2 className="w-5 h-5 shrink-0 mt-0.5" />
                  : <AlertTriangle className="w-5 h-5 shrink-0 mt-0.5" />
                }
                <span>{message.text}</span>
              </div>
            )}

            {/* Confirm / Save */}
            {!confirming ? (
              <button
                id="price-change-request-confirm"
                onClick={() => setConfirming(true)}
                disabled={!isValid}
                className="bg-violet-600 hover:bg-violet-700 text-white font-bold px-6 py-2.5 rounded-lg text-sm transition disabled:opacity-40 disabled:cursor-not-allowed flex items-center gap-2"
              >
                <Tag className="w-4 h-4" />
                Apply Price Change
              </button>
            ) : (
              <div className="bg-amber-50 border border-amber-300 rounded-xl p-4">
                <p className="text-sm font-bold text-amber-800 mb-1 flex items-center gap-2">
                  <AlertTriangle className="w-4 h-4" />
                  Confirm Price Change
                </p>
                <p className="text-xs text-amber-700 mb-4">
                  You are about to update <strong>{selectedProduct.name}</strong>:
                  MRP <strong>₹{parsedMrp.toFixed(2)}</strong> &nbsp;|&nbsp;
                  Selling Price <strong>₹{parsedSP.toFixed(2)}</strong>.<br />
                  This will take effect at POS <strong>immediately</strong> on the next product scan.
                </p>
                <div className="flex gap-3">
                  <button
                    id="price-change-confirm"
                    onClick={handleSave}
                    disabled={saving}
                    className="bg-amber-600 hover:bg-amber-700 text-white font-bold px-5 py-2 rounded-lg text-sm transition flex items-center gap-2 disabled:opacity-50"
                  >
                    {saving ? <Loader2 className="w-4 h-4 animate-spin" /> : <CheckCircle2 className="w-4 h-4" />}
                    {saving ? 'Saving...' : 'Yes, Apply'}
                  </button>
                  <button
                    onClick={() => setConfirming(false)}
                    disabled={saving}
                    className="bg-white border border-slate-200 text-slate-700 font-bold px-5 py-2 rounded-lg text-sm hover:bg-slate-50 transition disabled:opacity-50"
                  >
                    Cancel
                  </button>
                </div>
              </div>
            )}
          </div>

          {/* Price History */}
          <div className="bg-white p-6 rounded-xl border border-slate-100 shadow-sm">
            <div className="flex items-center gap-2 mb-4">
              <History className="w-4 h-4 text-slate-500" />
              <h4 className="font-bold text-slate-700 text-sm">Price History</h4>
            </div>

            {historyLoading ? (
              <div className="flex items-center gap-2 text-slate-400 text-sm py-6 justify-center">
                <Loader2 className="w-5 h-5 animate-spin" />
                Loading history...
              </div>
            ) : priceHistory && priceHistory.batches.length > 0 ? (
              <div className="overflow-x-auto">
                <table className="w-full text-xs border-collapse">
                  <thead>
                    <tr className="bg-slate-50 border-b border-slate-200">
                      <th className="px-3 py-2 text-left font-bold text-slate-600 uppercase tracking-wide">Batch / Label</th>
                      <th className="px-3 py-2 text-right font-bold text-slate-600 uppercase tracking-wide">MRP</th>
                      <th className="px-3 py-2 text-right font-bold text-slate-600 uppercase tracking-wide">Stock</th>
                      <th className="px-3 py-2 text-left font-bold text-slate-600 uppercase tracking-wide">Type</th>
                      <th className="px-3 py-2 text-left font-bold text-slate-600 uppercase tracking-wide">Date</th>
                      <th className="px-3 py-2 text-left font-bold text-slate-600 uppercase tracking-wide">Ref / Reason</th>
                    </tr>
                  </thead>
                  <tbody>
                    {priceHistory.batches.map((b, i) => (
                      <tr key={b.id} className={`border-b border-slate-100 ${i === 0 ? 'bg-violet-50' : ''}`}>
                        <td className="px-3 py-2.5 font-semibold text-slate-700">
                          {b.batchNumber}
                          {i === 0 && <span className="ml-2 text-[10px] bg-violet-200 text-violet-700 px-1.5 py-0.5 rounded-full font-bold">LATEST</span>}
                        </td>
                        <td className="px-3 py-2.5 text-right font-bold text-slate-800">₹{b.mrp.toFixed(2)}</td>
                        <td className="px-3 py-2.5 text-right text-slate-500">{b.availableQuantity.toFixed(0)}</td>
                        <td className="px-3 py-2.5">
                          {b.isPriceChangeBatch ? (
                            <span className="bg-amber-100 text-amber-700 text-[10px] font-bold px-1.5 py-0.5 rounded-full">PRICE CHANGE</span>
                          ) : (
                            <span className="bg-emerald-100 text-emerald-700 text-[10px] font-bold px-1.5 py-0.5 rounded-full">GRN BATCH</span>
                          )}
                        </td>
                        <td className="px-3 py-2.5 text-slate-500">
                          {new Date(b.createdAt).toLocaleDateString('en-IN', { day: '2-digit', month: 'short', year: 'numeric' })}
                        </td>
                        <td className="px-3 py-2.5 text-slate-400 max-w-xs truncate" title={b.grnReference || '—'}>
                          {b.grnReference
                            ? b.grnReference.replace(/^PRICE-CHANGE \| /, '').substring(0, 60)
                            : '—'}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            ) : (
              <p className="text-slate-400 text-sm text-center py-6">No price history found for this product.</p>
            )}
          </div>
        </>
      )}
    </div>
  );
};
