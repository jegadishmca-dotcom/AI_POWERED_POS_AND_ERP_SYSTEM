import React, { useState, useEffect } from 'react';
import { Save, Search, Plus, Trash2, ArrowLeft } from 'lucide-react';
import { Supplier } from './SupplierList';
import { api } from '../../../utils/api';

interface PurchaseOrderFormProps {
  onClose: () => void;
  onSaved: () => void;
}

interface POItem {
  productId: string;
  name: string;
  productCode: string;
  orderedQty: number;
  unitCost: number;
}

interface ProductSearchResult {
  id: string;
  productCode: string;
  name: string;
  sellingPrice: number;
  primaryBarcode: string;
}

export const PurchaseOrderForm: React.FC<PurchaseOrderFormProps> = ({ onClose, onSaved }) => {
  const [items, setItems] = useState<POItem[]>([]);
  const [supplierId, setSupplierId] = useState('');
  const [suppliers, setSuppliers] = useState<Supplier[]>([]);
  const [expectedDeliveryDate, setExpectedDeliveryDate] = useState(
    new Date(Date.now() + 7 * 24 * 60 * 60 * 1000).toISOString().slice(0, 10)
  );

  // Product Search State
  const [productQuery, setProductQuery] = useState('');
  const [searchResults, setSearchResults] = useState<ProductSearchResult[]>([]);
  const [showDropdown, setShowDropdown] = useState(false);
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    api.get('/api/suppliers')
      .then(res => setSuppliers(res.data.filter((s: Supplier) => s.isActive)))
      .catch(err => console.error('Failed to load suppliers', err));
  }, []);

  // Search products handler
  useEffect(() => {
    if (productQuery.trim().length < 2) {
      setSearchResults([]);
      setShowDropdown(false);
      return;
    }

    const delayDebounceFn = setTimeout(async () => {
      try {
        const res = await api.get(`/api/catalog/search?q=${productQuery}`);
        setSearchResults(res.data);
        setShowDropdown(res.data.length > 0);
      } catch (err) {
        console.error('Failed to search products', err);
      }
    }, 300);

    return () => clearTimeout(delayDebounceFn);
  }, [productQuery]);

  const handleSelectProduct = (product: ProductSearchResult) => {
    // Check if product already exists in items
    const existing = items.find(item => item.productId === product.id);
    if (existing) {
      alert(`${product.name} is already in the list.`);
      setProductQuery('');
      setShowDropdown(false);
      return;
    }

    // Default cost to 80% of selling price as estimated cost
    const defaultCost = Number((product.sellingPrice * 0.8).toFixed(2)) || 0;

    setItems([...items, {
      productId: product.id,
      name: product.name,
      productCode: product.productCode,
      orderedQty: 1,
      unitCost: defaultCost
    }]);

    setProductQuery('');
    setShowDropdown(false);
  };

  const handleRemoveItem = (index: number) => {
    setItems(items.filter((_, i) => i !== index));
  };

  const handleQtyChange = (index: number, val: number) => {
    const updated = [...items];
    updated[index].orderedQty = Math.max(0, val);
    setItems(updated);
  };

  const handleCostChange = (index: number, val: number) => {
    const updated = [...items];
    updated[index].unitCost = Math.max(0, val);
    setItems(updated);
  };

  const totalAmount = items.reduce((sum, item) => sum + (item.orderedQty * item.unitCost), 0);

  const handleSaveDraft = async () => {
    if (!supplierId) {
      alert("Please select a supplier");
      return;
    }
    if (items.length === 0) {
      alert("Please add at least one product to the purchase order");
      return;
    }
    const invalidItem = items.find(item => item.orderedQty <= 0 || item.unitCost <= 0);
    if (invalidItem) {
      alert("Quantity and Unit Cost must be greater than zero for all items.");
      return;
    }

    try {
      setSubmitting(true);
      const payload = {
        storeId: null,
        supplierId: supplierId,
        expectedDeliveryDate: new Date(expectedDeliveryDate).toISOString(),
        items: items.map(item => ({
          productId: item.productId,
          orderedQuantity: item.orderedQty,
          unitCost: item.unitCost
        })),
        userId: null
      };

      await api.post('/api/purchasing/purchase-orders', payload);
      alert("Purchase Order created successfully as DRAFT!");
      onSaved();
    } catch (err: any) {
      console.error(err);
      alert(err.response?.data?.message || "Failed to save Purchase Order");
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="bg-white shadow rounded-lg p-6 max-w-6xl mx-auto">
      <div className="flex justify-between items-center mb-6 border-b pb-4">
        <div className="flex items-center gap-3">
          <button 
            onClick={onClose}
            className="p-2 hover:bg-slate-100 rounded-lg text-slate-500 transition-colors"
          >
            <ArrowLeft className="w-5 h-5" />
          </button>
          <h2 className="text-2xl font-bold text-slate-800">Create Purchase Order</h2>
        </div>
        <button 
          onClick={handleSaveDraft} 
          disabled={submitting}
          className="px-6 py-2 bg-blue-600 text-white rounded shadow flex items-center font-bold hover:bg-blue-700 disabled:opacity-50 transition-colors"
        >
          <Save className="w-5 h-5 mr-2" /> Save PO (Draft)
        </button>
      </div>

      <div className="grid grid-cols-2 gap-6 mb-8">
        <div>
          <label className="block text-sm font-bold text-gray-700 mb-2">Supplier</label>
          <select 
            className="w-full p-2 border rounded bg-white focus:outline-none focus:ring-2 focus:ring-blue-500/20" 
            value={supplierId} 
            onChange={(e) => setSupplierId(e.target.value)}
          >
            <option value="">-- Select Supplier --</option>
            {suppliers.map(s => (
              <option key={s.id} value={s.id}>{s.name} ({s.paymentTerms})</option>
            ))}
          </select>
        </div>
        <div>
          <label className="block text-sm font-bold text-gray-700 mb-2">Expected Delivery Date</label>
          <input 
            type="date" 
            value={expectedDeliveryDate}
            onChange={(e) => setExpectedDeliveryDate(e.target.value)}
            className="w-full p-2 border rounded focus:outline-none focus:ring-2 focus:ring-blue-500/20" 
          />
        </div>
      </div>

      {/* Product Search Add Component */}
      <div className="mb-6 relative">
        <label className="block text-sm font-bold text-gray-700 mb-2">Search & Add Product</label>
        <div className="relative">
          <Search className="absolute left-3 top-3 text-gray-400 w-5 h-5" />
          <input 
            type="text" 
            placeholder="Type barcode, code, or name to add product..." 
            value={productQuery}
            onChange={(e) => setProductQuery(e.target.value)}
            className="w-full pl-10 pr-4 py-2 border-2 border-slate-200 rounded-lg focus:outline-none focus:border-blue-500"
          />
        </div>
        
        {showDropdown && (
          <div className="absolute z-10 w-full bg-white border border-slate-200 rounded-lg shadow-lg mt-1 max-h-60 overflow-y-auto">
            {searchResults.map(p => (
              <div 
                key={p.id}
                onClick={() => handleSelectProduct(p)}
                className="p-3 hover:bg-blue-50 cursor-pointer flex justify-between items-center border-b last:border-0"
              >
                <div>
                  <div className="font-bold text-slate-800">{p.name}</div>
                  <div className="text-xs text-slate-500">Code: {p.productCode} • Barcode: {p.primaryBarcode}</div>
                </div>
                <div className="text-right">
                  <div className="font-bold text-slate-700">MRP: ₹{p.sellingPrice.toFixed(2)}</div>
                </div>
              </div>
            ))}
          </div>
        )}
      </div>

      <div className="mb-4">
        <h3 className="text-lg font-bold text-slate-800 border-b pb-2">PO Line Items</h3>
      </div>

      <table className="w-full text-left border-collapse mb-6">
        <thead className="bg-slate-100 text-sm">
          <tr>
            <th className="p-3 border">Product Details</th>
            <th className="p-3 border text-right w-36">Ordered Quantity</th>
            <th className="p-3 border text-right w-36">Unit Cost (₹)</th>
            <th className="p-3 border text-right w-36">Total (₹)</th>
            <th className="p-3 border text-center w-12"></th>
          </tr>
        </thead>
        <tbody>
          {items.map((item, idx) => (
            <tr key={item.productId} className="border-b hover:bg-slate-50/50">
              <td className="p-3">
                <div className="font-bold text-slate-800">{item.name}</div>
                <div className="text-xs text-slate-500">Code: {item.productCode}</div>
              </td>
              <td className="p-3">
                <input 
                  type="number" 
                  min="1"
                  step="any"
                  className="w-full p-1.5 border rounded text-right focus:outline-none focus:ring-2 focus:ring-blue-500/20" 
                  value={item.orderedQty} 
                  onChange={(e) => handleQtyChange(idx, parseFloat(e.target.value) || 0)}
                />
              </td>
              <td className="p-3">
                <input 
                  type="number" 
                  min="0.01"
                  step="any"
                  className="w-full p-1.5 border rounded text-right focus:outline-none focus:ring-2 focus:ring-blue-500/20" 
                  value={item.unitCost} 
                  onChange={(e) => handleCostChange(idx, parseFloat(e.target.value) || 0)}
                />
              </td>
              <td className="p-3 text-right font-black text-slate-700">
                ₹{(item.orderedQty * item.unitCost).toFixed(2)}
              </td>
              <td className="p-3 text-center">
                <button 
                  onClick={() => handleRemoveItem(idx)} 
                  className="p-1.5 text-slate-400 hover:text-red-600 hover:bg-red-50 rounded-lg transition-colors"
                >
                  <Trash2 className="w-5 h-5" />
                </button>
              </td>
            </tr>
          ))}
          {items.length === 0 && (
            <tr>
              <td colSpan={5} className="p-8 text-center text-gray-500">
                No items added yet. Search and select a product above to add it.
              </td>
            </tr>
          )}
        </tbody>
      </table>

      <div className="flex justify-end border-t pt-4">
        <div className="text-right">
          <p className="text-gray-500 font-bold mb-1 text-sm uppercase tracking-wider">Total PO Amount</p>
          <p className="text-4xl font-black text-slate-800">₹{totalAmount.toFixed(2)}</p>
        </div>
      </div>
    </div>
  );
};
