import React, { useState } from 'react';
import { Supplier } from './SupplierList';

interface SupplierFormProps {
  supplier?: Supplier;
  onClose: () => void;
  onSaved: () => void;
}

export const SupplierForm: React.FC<SupplierFormProps> = ({ supplier, onClose, onSaved }) => {
  const [formData, setFormData] = useState<Partial<Supplier>>(
    supplier || {
      name: '',
      gstin: '',
      phone: '',
      paymentTerms: 'NET30',
      isActive: true
    }
  );
  
  const [loading, setLoading] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setLoading(true);

    const isEditing = !!supplier?.id;
    const url = isEditing ? `/api/suppliers/${supplier.id}` : '/api/suppliers';
    const method = isEditing ? 'PUT' : 'POST';

    try {
      const response = await fetch(url, {
        method,
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(formData),
      });

      if (response.ok) {
        onSaved();
      } else {
        console.error('Failed to save supplier');
      }
    } catch (error) {
      console.error('Error saving supplier', error);
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="fixed inset-0 bg-black/50 backdrop-blur-sm flex justify-center items-center z-50">
      <div className="bg-white rounded-xl shadow-xl w-full max-w-md overflow-hidden">
        <div className="p-6 border-b border-gray-100 flex justify-between items-center">
          <h2 className="text-xl font-bold text-gray-800">
            {supplier ? 'Edit Supplier' : 'New Supplier'}
          </h2>
          <button onClick={onClose} className="text-gray-400 hover:text-gray-600">✕</button>
        </div>

        <form onSubmit={handleSubmit} className="p-6 space-y-4">
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Company Name *</label>
            <input
              required
              type="text"
              value={formData.name}
              onChange={e => setFormData({ ...formData, name: e.target.value })}
              className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-blue-500"
            />
          </div>

          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">GSTIN</label>
            <input
              type="text"
              value={formData.gstin}
              onChange={e => setFormData({ ...formData, gstin: e.target.value })}
              className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-blue-500 uppercase"
              placeholder="e.g. 29ABCDE1234F1Z5"
            />
          </div>

          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Phone *</label>
            <input
              required
              type="tel"
              value={formData.phone}
              onChange={e => setFormData({ ...formData, phone: e.target.value })}
              className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-blue-500"
            />
          </div>

          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Payment Terms</label>
            <select
              value={formData.paymentTerms}
              onChange={e => setFormData({ ...formData, paymentTerms: e.target.value })}
              className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-blue-500"
            >
              <option value="PIA">Payment in Advance (PIA)</option>
              <option value="NET15">Net 15 Days</option>
              <option value="NET30">Net 30 Days</option>
              <option value="NET60">Net 60 Days</option>
              <option value="COD">Cash on Delivery (COD)</option>
            </select>
          </div>

          {supplier && (
            <div className="flex items-center gap-2 mt-4">
              <input
                type="checkbox"
                id="isActive"
                checked={formData.isActive}
                onChange={e => setFormData({ ...formData, isActive: e.target.checked })}
                className="w-4 h-4 text-blue-600 rounded"
              />
              <label htmlFor="isActive" className="text-sm text-gray-700">Active Vendor</label>
            </div>
          )}

          <div className="mt-6 flex justify-end gap-3 pt-4 border-t border-gray-100">
            <button
              type="button"
              onClick={onClose}
              className="px-4 py-2 text-gray-600 bg-gray-50 hover:bg-gray-100 rounded-lg transition-colors"
            >
              Cancel
            </button>
            <button
              type="submit"
              disabled={loading}
              className="px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded-lg transition-colors disabled:opacity-50"
            >
              {loading ? 'Saving...' : 'Save Supplier'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};
