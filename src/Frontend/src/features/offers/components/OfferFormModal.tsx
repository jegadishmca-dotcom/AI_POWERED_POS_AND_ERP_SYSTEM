import React, { useState, useEffect } from 'react';
import { X, Save, Eye } from 'lucide-react';
import { createOffer, updateOffer } from '../services/offers.api';
import { api } from '../../../utils/api';

export const OfferFormModal = ({ offer, onClose, onSave }: { offer?: any, onClose: () => void, onSave: () => void }) => {
  const [formData, setFormData] = useState({
    name: '',
    description: '',
    offerType: 'PERCENTAGE', // PERCENTAGE, FLAT, BOGO, COMBO
    promoCode: '',
    priority: 0,
    isStackable: false,
    isExclusive: false,
    startDate: new Date().toISOString().slice(0, 16),
    endDate: new Date(new Date().getTime() + 30 * 24 * 60 * 60 * 1000).toISOString().slice(0, 16),
    isActive: true,
    storeId: '',
    
    // Rule specifics
    applyTo: 'BILL', // BILL, LINE
    discountType: 'Percentage', // Percentage, FlatAmount, FreeProduct
    discountValue: 0,
    maxDiscountAmount: '',
    
    // Conditions
    minCartValue: '',
    minQuantity: '',
    requiredProductIds: '',
    requiredCategoryIds: '',
    requiredCustomerTier: ''
  });

  const [categories, setCategories] = useState<any[]>([]);
  const [products, setProducts] = useState<any[]>([]);
  const [isSaving, setIsSaving] = useState(false);

  useEffect(() => {
    const fetchLookups = async () => {
      try {
        const [catRes, prodRes] = await Promise.all([
          api.get('/api/catalog/categories'),
          api.get('/api/catalog/products')
        ]);
        setCategories(catRes.data);
        setProducts(prodRes.data);
      } catch (err) {
        console.error('Failed to load lookups', err);
      }
    };
    fetchLookups();

    if (offer) {
      let rules = { Conditions: {}, Reward: {} } as any;
      try { rules = JSON.parse(offer.rulesJson || '{}'); } catch(e) {}

      setFormData({
        name: offer.name,
        description: offer.description,
        offerType: offer.offerType,
        promoCode: offer.promoCode || '',
        priority: offer.priority,
        isStackable: offer.isStackable,
        isExclusive: offer.isExclusive,
        startDate: new Date(offer.startDate).toISOString().slice(0, 16),
        endDate: new Date(offer.endDate).toISOString().slice(0, 16),
        isActive: offer.isActive,
        storeId: offer.storeId || '',
        
        applyTo: rules.Reward?.ApplyTo || 'BILL',
        discountType: rules.Reward?.DiscountType || 'Percentage',
        discountValue: rules.Reward?.Value || 0,
        maxDiscountAmount: rules.Reward?.MaxDiscountAmount || '',
        
        minCartValue: rules.Conditions?.MinCartValue || '',
        minQuantity: rules.Conditions?.MinQuantity || '',
        requiredProductIds: rules.Conditions?.RequiredProductIds?.join(',') || '',
        requiredCategoryIds: rules.Conditions?.RequiredCategoryIds?.join(',') || '',
        requiredCustomerTier: rules.Conditions?.RequiredCustomerTier || ''
      });
    }
  }, [offer]);

  const handleChange = (e: any) => {
    const { name, value, type, checked } = e.target;
    setFormData(prev => ({
      ...prev,
      [name]: type === 'checkbox' ? checked : value
    }));
  };

  const handleSave = async () => {
    if (new Date(formData.endDate) < new Date(formData.startDate)) {
      alert('End Date cannot be before Start Date');
      return;
    }
    if (formData.discountType === 'Percentage' && Number(formData.discountValue) > 100) {
      alert('Percentage discount cannot exceed 100%');
      return;
    }
    if (Number(formData.discountValue) < 0) {
      alert('Discount value cannot be negative');
      return;
    }

    setIsSaving(true);

    const rulesJson = {
      Conditions: {
        MinCartValue: formData.minCartValue ? Number(formData.minCartValue) : null,
        MinQuantity: formData.minQuantity ? Number(formData.minQuantity) : null,
        RequiredProductIds: formData.requiredProductIds ? formData.requiredProductIds.split(',').map(s=>s.trim()) : null,
        RequiredCategoryIds: formData.requiredCategoryIds ? formData.requiredCategoryIds.split(',').map(s=>s.trim()) : null,
        RequiredCustomerTier: formData.requiredCustomerTier || null
      },
      Reward: {
        ApplyTo: formData.applyTo,
        DiscountType: formData.discountType,
        Value: Number(formData.discountValue),
        MaxDiscountAmount: formData.maxDiscountAmount ? Number(formData.maxDiscountAmount) : null
      }
    };

    const payload = {
      ...formData,
      rulesJson: JSON.stringify(rulesJson),
      storeId: formData.storeId || null
    };

    try {
      if (offer?.id) {
        await updateOffer(offer.id, payload);
      } else {
        await createOffer(payload);
      }
      onSave();
    } catch (err: any) {
      console.error(err);
      alert('Failed to save offer: ' + (err.response?.data?.message || err.message));
    } finally {
      setIsSaving(false);
    }
  };

  // Preview Logic (Simulated)
  const renderPreview = () => {
    let exampleCart = 1500;
    let expectedDiscount = 0;

    if (formData.applyTo === 'BILL') {
      if (formData.minCartValue && exampleCart < Number(formData.minCartValue)) {
        return <div className="text-rose-500 font-bold">Cart does not meet minimum value of ₹{formData.minCartValue}</div>;
      }
      if (formData.discountType === 'Percentage') {
        expectedDiscount = exampleCart * (Number(formData.discountValue) / 100);
      } else if (formData.discountType === 'FlatAmount') {
        expectedDiscount = Number(formData.discountValue);
      }
    } else {
      expectedDiscount = Number(formData.discountValue); // Simplified simulation for items
    }

    if (formData.maxDiscountAmount && expectedDiscount > Number(formData.maxDiscountAmount)) {
      expectedDiscount = Number(formData.maxDiscountAmount);
    }

    return (
      <div className="bg-indigo-50 p-4 rounded-xl border border-indigo-100">
        <h4 className="font-bold text-indigo-800 flex items-center gap-2 mb-2"><Eye className="w-4 h-4"/> Simulation Preview</h4>
        <div className="space-y-1 text-sm text-indigo-900">
          <div className="flex justify-between"><span>Example Cart Value:</span> <span className="font-mono">₹{exampleCart.toFixed(2)}</span></div>
          <div className="flex justify-between font-bold text-emerald-600"><span>Expected Discount:</span> <span className="font-mono">-₹{expectedDiscount.toFixed(2)}</span></div>
          <div className="flex justify-between border-t border-indigo-200 mt-2 pt-2 font-black"><span>Net Amount:</span> <span className="font-mono">₹{(exampleCart - expectedDiscount).toFixed(2)}</span></div>
        </div>
      </div>
    );
  };

  return (
    <div className="fixed inset-0 bg-slate-900/50 flex items-center justify-center p-4 z-50 overflow-y-auto">
      <div className="bg-white rounded-3xl shadow-2xl w-full max-w-4xl flex flex-col max-h-screen">
        <div className="p-6 border-b border-slate-100 flex justify-between items-center bg-slate-50 rounded-t-3xl sticky top-0 z-10">
          <div>
            <h2 className="text-2xl font-black text-slate-800">{offer ? 'Edit Offer' : 'Create New Offer'}</h2>
            <p className="text-slate-500 text-sm">Configure dynamic pricing and promotions</p>
          </div>
          <button onClick={onClose} className="p-2 hover:bg-slate-200 rounded-full transition-colors"><X className="w-6 h-6 text-slate-500" /></button>
        </div>

        <div className="p-8 overflow-y-auto flex-1 grid grid-cols-1 md:grid-cols-2 gap-8">
          {/* Left Column: Basic Details */}
          <div className="space-y-6">
            <h3 className="font-bold text-slate-800 border-b pb-2">Basic Details</h3>
            
            <div>
              <label className="block text-sm font-bold text-slate-700 mb-1">Offer Name</label>
              <input type="text" name="name" value={formData.name} onChange={handleChange} className="w-full p-3 border border-slate-200 rounded-xl focus:ring-2 focus:ring-indigo-500" placeholder="e.g., Diwali Special" required />
            </div>

            <div>
              <label className="block text-sm font-bold text-slate-700 mb-1">Description</label>
              <textarea name="description" value={formData.description} onChange={handleChange} className="w-full p-3 border border-slate-200 rounded-xl focus:ring-2 focus:ring-indigo-500" rows={2} />
            </div>

            <div className="grid grid-cols-2 gap-4">
              <div>
                <label className="block text-sm font-bold text-slate-700 mb-1">Promo Code (Optional)</label>
                <input type="text" name="promoCode" value={formData.promoCode} onChange={handleChange} className="w-full p-3 border border-slate-200 rounded-xl focus:ring-2 focus:ring-indigo-500" placeholder="DIWALI10" />
              </div>
              <div>
                <label className="block text-sm font-bold text-slate-700 mb-1">Priority (Higher runs first)</label>
                <input type="number" name="priority" value={formData.priority} onChange={handleChange} className="w-full p-3 border border-slate-200 rounded-xl focus:ring-2 focus:ring-indigo-500" />
              </div>
            </div>

            <div className="grid grid-cols-2 gap-4">
              <div>
                <label className="block text-sm font-bold text-slate-700 mb-1">Start Date</label>
                <input type="datetime-local" name="startDate" value={formData.startDate} onChange={handleChange} className="w-full p-3 border border-slate-200 rounded-xl" />
              </div>
              <div>
                <label className="block text-sm font-bold text-slate-700 mb-1">End Date</label>
                <input type="datetime-local" name="endDate" value={formData.endDate} onChange={handleChange} className="w-full p-3 border border-slate-200 rounded-xl" />
              </div>
            </div>

            <div className="flex gap-6 pt-2">
              <label className="flex items-center gap-2 cursor-pointer">
                <input type="checkbox" name="isStackable" checked={formData.isStackable} onChange={handleChange} className="w-5 h-5 text-indigo-600 rounded" />
                <span className="font-medium text-slate-700">Allow Stacking</span>
              </label>
              <label className="flex items-center gap-2 cursor-pointer">
                <input type="checkbox" name="isExclusive" checked={formData.isExclusive} onChange={handleChange} className="w-5 h-5 text-indigo-600 rounded" />
                <span className="font-medium text-slate-700">Exclusive (Overrides others)</span>
              </label>
            </div>
          </div>

          {/* Right Column: Rule Builder */}
          <div className="space-y-6">
            <h3 className="font-bold text-slate-800 border-b pb-2">Offer Configuration</h3>

            <div className="grid grid-cols-2 gap-4">
              <div>
                <label className="block text-sm font-bold text-slate-700 mb-1">Apply To</label>
                <select name="applyTo" value={formData.applyTo} onChange={handleChange} className="w-full p-3 border border-slate-200 rounded-xl bg-slate-50">
                  <option value="BILL">Entire Bill</option>
                  <option value="LINE">Specific Items</option>
                </select>
              </div>
              <div>
                <label className="block text-sm font-bold text-slate-700 mb-1">Discount Type</label>
                <select name="discountType" value={formData.discountType} onChange={handleChange} className="w-full p-3 border border-slate-200 rounded-xl bg-slate-50">
                  <option value="Percentage">Percentage (%)</option>
                  <option value="FlatAmount">Flat Amount (₹)</option>
                </select>
              </div>
            </div>

            <div className="grid grid-cols-2 gap-4">
              <div>
                <label className="block text-sm font-bold text-slate-700 mb-1">Discount Value</label>
                <input type="number" name="discountValue" value={formData.discountValue} onChange={handleChange} className="w-full p-3 border border-slate-200 rounded-xl font-bold text-emerald-600 text-lg" min="0" step="0.01" />
              </div>
              <div>
                <label className="block text-sm font-bold text-slate-700 mb-1">Max Discount (Cap)</label>
                <input type="number" name="maxDiscountAmount" value={formData.maxDiscountAmount} onChange={handleChange} className="w-full p-3 border border-slate-200 rounded-xl" placeholder="No Limit" />
              </div>
            </div>

            <div className="bg-slate-50 p-4 rounded-xl border border-slate-200 space-y-4">
              <h4 className="font-bold text-slate-700">Conditions</h4>
              
              {formData.applyTo === 'BILL' && (
                <div>
                  <label className="block text-xs font-bold text-slate-500 mb-1">Minimum Cart Value (₹)</label>
                  <input type="number" name="minCartValue" value={formData.minCartValue} onChange={handleChange} className="w-full p-2 border border-slate-200 rounded-lg" placeholder="e.g. 1000" />
                </div>
              )}

              {formData.applyTo === 'LINE' && (
                <>
                  <div>
                    <label className="block text-xs font-bold text-slate-500 mb-1">Minimum Quantity (for BOGO/Slab)</label>
                    <input type="number" name="minQuantity" value={formData.minQuantity} onChange={handleChange} className="w-full p-2 border border-slate-200 rounded-lg" />
                  </div>
                  <div>
                    <label className="block text-xs font-bold text-slate-500 mb-1">Required Category ID</label>
                    <select name="requiredCategoryIds" value={formData.requiredCategoryIds} onChange={handleChange} className="w-full p-2 border border-slate-200 rounded-lg">
                      <option value="">Any Category</option>
                      {categories.map(c => <option key={c.id} value={c.id}>{c.name}</option>)}
                    </select>
                  </div>
                  <div>
                    <label className="block text-xs font-bold text-slate-500 mb-1">Required Product ID</label>
                    <select name="requiredProductIds" value={formData.requiredProductIds} onChange={handleChange} className="w-full p-2 border border-slate-200 rounded-lg">
                      <option value="">Any Product</option>
                      {products.map(p => <option key={p.id} value={p.id}>{p.name}</option>)}
                    </select>
                  </div>
                </>
              )}

              <div>
                <label className="block text-xs font-bold text-slate-500 mb-1">Customer Tier</label>
                <select name="requiredCustomerTier" value={formData.requiredCustomerTier} onChange={handleChange} className="w-full p-2 border border-slate-200 rounded-lg">
                  <option value="">All Customers</option>
                  <option value="Gold">Gold Members</option>
                  <option value="Premium">Premium</option>
                </select>
              </div>
            </div>

            {renderPreview()}

          </div>
        </div>

        <div className="p-6 border-t border-slate-100 bg-slate-50 rounded-b-3xl flex justify-end gap-4 sticky bottom-0 z-10">
          <button onClick={onClose} className="px-6 py-3 font-bold text-slate-600 hover:bg-slate-200 rounded-xl transition-colors">Cancel</button>
          <button onClick={handleSave} disabled={isSaving} className="px-8 py-3 bg-indigo-600 text-white font-bold rounded-xl shadow-lg hover:bg-indigo-700 disabled:opacity-50 flex items-center gap-2">
            <Save className="w-5 h-5" />
            {isSaving ? 'Saving...' : 'Save Offer'}
          </button>
        </div>
      </div>
    </div>
  );
};
