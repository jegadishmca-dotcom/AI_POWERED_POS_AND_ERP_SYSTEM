import React, { useState, useEffect } from 'react';
import { X, Loader2, Save } from 'lucide-react';
import { createProduct, updateProduct, getTaxSlabs, TaxSlab, getUoms, getCategories, Category, UnitOfMeasure } from '../api/catalog.api';
import { useQueryClient } from '@tanstack/react-query';

interface CreateProductModalProps {
  isOpen: boolean;
  onClose: () => void;
  editingProduct?: any;
}

export const CreateProductModal: React.FC<CreateProductModalProps> = ({ isOpen, onClose, editingProduct }) => {
  const queryClient = useQueryClient();
  const [productCode, setProductCode] = useState('');
  const [name, setName] = useState('');
  const [tamilName, setTamilName] = useState('');
  const [description, setDescription] = useState('');
  const [mrp, setMrp] = useState('');
  const [sellingPrice, setSellingPrice] = useState('');
  const [purchasePrice, setPurchasePrice] = useState('');
  const [barcodeValue, setBarcodeValue] = useState('');
  const [taxSlabId, setTaxSlabId] = useState('');
  const [taxSlabs, setTaxSlabs] = useState<TaxSlab[]>([]);
  const [categoryId, setCategoryId] = useState('');
  const [unitOfMeasureId, setUnitOfMeasureId] = useState('');
  const [categories, setCategories] = useState<Category[]>([]);
  const [uoms, setUoms] = useState<UnitOfMeasure[]>([]);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    getTaxSlabs().then(setTaxSlabs).catch(console.error);
    getCategories().then(setCategories).catch(console.error);
    getUoms().then(setUoms).catch(console.error);
  }, []);

  useEffect(() => {
    if (isOpen) {
      if (editingProduct) {
        setProductCode(editingProduct.productCode || '');
        setName(editingProduct.name || '');
        setTamilName(editingProduct.tamilName || '');
        setDescription(editingProduct.description || '');
        setMrp(editingProduct.mrp?.toString() || '');
        setSellingPrice(editingProduct.sellingPrice?.toString() || '');
        setPurchasePrice(editingProduct.purchasePrice?.toString() || '');
        setBarcodeValue(editingProduct.primaryBarcode || '');
        setTaxSlabId(editingProduct.taxSlabId || '');
        setCategoryId(editingProduct.categoryId || '');
        setUnitOfMeasureId(editingProduct.unitOfMeasureId || '');
      } else {
        setProductCode('');
        setName('');
        setTamilName('');
        setDescription('');
        setMrp('');
        setSellingPrice('');
        setPurchasePrice('');
        setBarcodeValue('');
        setTaxSlabId('');
        setCategoryId('');
        
        const defaultUom = uoms.find(u => u.symbol === 'Pcs') || uoms[0];
        setUnitOfMeasureId(defaultUom?.id || 'a0000000-0000-0000-0000-000000000001');
      }
      setError(null);
    }
  }, [isOpen, editingProduct, uoms]);

  const renderCategoryOptions = () => {
    const roots = categories.filter(c => !c.parentCategoryId);
    const options: React.ReactNode[] = [];
    
    roots.forEach(root => {
      options.push(
        <option key={root.id} value={root.id}>
          {root.name}
        </option>
      );
      const children = categories.filter(c => c.parentCategoryId === root.id);
      children.forEach(child => {
        options.push(
          <option key={child.id} value={child.id}>
            &nbsp;&nbsp;↳ {child.name}
          </option>
        );
      });
    });
    
    categories.forEach(c => {
      if (!roots.some(r => r.id === c.id) && !categories.some(parent => parent.id === c.parentCategoryId && roots.some(r => r.id === parent.id))) {
        if (!options.some((o: any) => o.key === c.id)) {
          options.push(
            <option key={c.id} value={c.id}>
              {c.name}
            </option>
          );
        }
      }
    });
    
    return options;
  };

  if (!isOpen) return null;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!productCode || !name || !mrp || !sellingPrice || !purchasePrice) {
      setError('Please fill in all required fields.');
      return;
    }
    
    setIsSubmitting(true);
    setError(null);
    try {
      if (editingProduct) {
        await updateProduct(editingProduct.id, {
          id: editingProduct.id,
          productCode,
          name,
          tamilName: tamilName || undefined,
          description: description || undefined,
          mrp: parseFloat(mrp),
          sellingPrice: parseFloat(sellingPrice),
          purchasePrice: parseFloat(purchasePrice),
          barcodeValue: barcodeValue,
          taxSlabId: taxSlabId || undefined,
          categoryId: categoryId || undefined,
          unitOfMeasureId: unitOfMeasureId || undefined
        });
      } else {
        await createProduct({
          productCode,
          name,
          tamilName: tamilName || undefined,
          description: description || undefined,
          mrp: parseFloat(mrp),
          sellingPrice: parseFloat(sellingPrice),
          purchasePrice: parseFloat(purchasePrice),
          barcodeValue: barcodeValue,
          taxSlabId: taxSlabId || undefined,
          categoryId: categoryId || undefined,
          unitOfMeasureId: unitOfMeasureId || undefined
        });
      }
      queryClient.invalidateQueries({ queryKey: ['products'] });
      onClose();
    } catch (err: any) {
      const msg = err?.response?.data?.message || err?.response?.data?.Message || err?.response?.data?.Detailed || 'Failed to save product. Check that your product code and barcodes are unique.';
      setError(msg);
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-sm">
      <div className="bg-slate-900 border border-slate-800 rounded-xl shadow-2xl w-full max-w-lg overflow-hidden animate-in fade-in zoom-in-95 duration-200">
        {/* Header */}
        <div className="flex justify-between items-center px-6 py-4 border-b border-slate-800">
          <h2 className="text-lg font-semibold text-white">{editingProduct ? 'Edit Product' : 'Create New Product'}</h2>
          <button onClick={onClose} className="text-slate-400 hover:text-white transition">
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Form */}
        <form onSubmit={handleSubmit} className="p-6 space-y-4 max-h-[75vh] overflow-y-auto">
          {error && (
            <div className="bg-red-900/20 border border-red-800/40 text-red-400 p-3 rounded-lg text-sm">
              {error}
            </div>
          )}

          <div className="grid grid-cols-2 gap-4">
            <div>
              <label className="block text-xs font-semibold text-slate-400 uppercase tracking-wider mb-1">Product Code *</label>
              <input
                type="text"
                required
                className="w-full px-3 py-2 bg-slate-950 border border-slate-800 rounded-lg text-white placeholder-slate-600 focus:outline-none focus:border-blue-500 text-sm"
                value={productCode}
                onChange={(e) => setProductCode(e.target.value)}
              />
            </div>
            <div>
              <label className="block text-xs font-semibold text-slate-400 uppercase tracking-wider mb-1">Barcode Value</label>
              <div className="flex space-x-2">
                <input
                  type="text"
                  className="flex-1 px-3 py-2 bg-slate-950 border border-slate-800 rounded-lg text-white placeholder-slate-600 focus:outline-none focus:border-blue-500 text-sm"
                  value={barcodeValue}
                  onChange={(e) => setBarcodeValue(e.target.value)}
                  placeholder="Scan or enter barcode"
                />
                <button
                  type="button"
                  onClick={() => {
                    const ticks = Date.now().toString();
                    setBarcodeValue("29" + ticks.slice(-11));
                  }}
                  className="px-3 py-2 bg-slate-800 hover:bg-slate-700 text-xs font-bold text-white rounded-lg border border-slate-750 transition"
                  title="Auto-Generate Barcode"
                >
                  Gen
                </button>
              </div>
            </div>
          </div>

          <div>
            <label className="block text-xs font-semibold text-slate-400 uppercase tracking-wider mb-1">Product Name *</label>
            <input
              type="text"
              required
              className="w-full px-3 py-2 bg-slate-950 border border-slate-800 rounded-lg text-white placeholder-slate-600 focus:outline-none focus:border-blue-500 text-sm"
              value={name}
              onChange={(e) => setName(e.target.value)}
            />
          </div>

          <div>
            <label className="block text-xs font-semibold text-slate-400 uppercase tracking-wider mb-1">Tamil Name (Optional)</label>
            <input
              type="text"
              className="w-full px-3 py-2 bg-slate-950 border border-slate-800 rounded-lg text-white placeholder-slate-600 focus:outline-none focus:border-blue-500 text-sm font-tamil"
              value={tamilName}
              onChange={(e) => setTamilName(e.target.value)}
            />
          </div>

          <div className="grid grid-cols-4 gap-4">
            <div>
              <label className="block text-xs font-semibold text-slate-400 uppercase tracking-wider mb-1">MRP (₹) *</label>
              <input
                type="number"
                step="0.01"
                required
                className="w-full px-3 py-2 bg-slate-950 border border-slate-800 rounded-lg text-white placeholder-slate-600 focus:outline-none focus:border-blue-500 text-sm"
                value={mrp}
                onChange={(e) => setMrp(e.target.value)}
              />
            </div>
            <div>
              <label className="block text-xs font-semibold text-slate-400 uppercase tracking-wider mb-1">Selling (₹) *</label>
              <input
                type="number"
                step="0.01"
                required
                className="w-full px-3 py-2 bg-slate-950 border border-slate-800 rounded-lg text-white placeholder-slate-600 focus:outline-none focus:border-blue-500 text-sm"
                value={sellingPrice}
                onChange={(e) => setSellingPrice(e.target.value)}
              />
            </div>
            <div>
              <label className="block text-xs font-semibold text-slate-400 uppercase tracking-wider mb-1">Cost (₹) *</label>
              <input
                type="number"
                step="0.01"
                required
                className="w-full px-3 py-2 bg-slate-950 border border-slate-800 rounded-lg text-white placeholder-slate-600 focus:outline-none focus:border-blue-500 text-sm"
                value={purchasePrice}
                onChange={(e) => setPurchasePrice(e.target.value)}
              />
            </div>
            <div>
              <label className="block text-xs font-semibold text-slate-400 uppercase tracking-wider mb-1">Tax Slab *</label>
              <select
                required
                className="w-full px-3 py-2 bg-slate-950 border border-slate-800 rounded-lg text-white focus:outline-none focus:border-blue-500 text-sm"
                value={taxSlabId}
                onChange={(e) => setTaxSlabId(e.target.value)}
              >
                <option value="">-- Select --</option>
                {taxSlabs.map(ts => (
                  <option key={ts.id} value={ts.id}>{ts.name}</option>
                ))}
              </select>
            </div>
          </div>

          <div className="grid grid-cols-2 gap-4">
            <div>
              <label className="block text-xs font-semibold text-slate-400 uppercase tracking-wider mb-1">Category</label>
              <select
                className="w-full px-3 py-2 bg-slate-950 border border-slate-800 rounded-lg text-white focus:outline-none focus:border-blue-500 text-sm"
                value={categoryId}
                onChange={(e) => setCategoryId(e.target.value)}
              >
                <option value="">-- No Category --</option>
                {renderCategoryOptions()}
              </select>
            </div>
            <div>
              <label className="block text-xs font-semibold text-slate-400 uppercase tracking-wider mb-1">Unit of Measure (UOM) *</label>
              <select
                required
                className="w-full px-3 py-2 bg-slate-950 border border-slate-800 rounded-lg text-white focus:outline-none focus:border-blue-500 text-sm"
                value={unitOfMeasureId}
                onChange={(e) => setUnitOfMeasureId(e.target.value)}
              >
                <option value="">-- Select UOM --</option>
                {uoms.map(u => (
                  <option key={u.id} value={u.id}>{u.name} ({u.symbol})</option>
                ))}
              </select>
            </div>
          </div>

          <div>
            <label className="block text-xs font-semibold text-slate-400 uppercase tracking-wider mb-1">Description</label>
            <textarea
              className="w-full px-3 py-2 bg-slate-950 border border-slate-800 rounded-lg text-white placeholder-slate-600 focus:outline-none focus:border-blue-500 text-sm h-20 resize-none"
              value={description}
              onChange={(e) => setDescription(e.target.value)}
            />
          </div>

          <div className="flex justify-end space-x-3 pt-4 border-t border-slate-800">
            <button
              type="button"
              onClick={onClose}
              className="px-4 py-2 border border-slate-800 text-slate-300 rounded-lg text-sm hover:bg-slate-850 hover:text-white transition"
            >
              Cancel
            </button>
            <button
              type="submit"
              disabled={isSubmitting}
              className="px-4 py-2 bg-blue-600 text-white rounded-lg text-sm hover:bg-blue-700 transition flex items-center disabled:opacity-50"
            >
              {isSubmitting ? (
                <>
                  <Loader2 className="w-4 h-4 mr-2 animate-spin" /> Saving...
                </>
              ) : (
                <>
                  <Save className="w-4 h-4 mr-2" /> Save Product
                </>
              )}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};
