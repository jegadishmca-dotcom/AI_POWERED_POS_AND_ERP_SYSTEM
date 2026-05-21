import React, { useState } from 'react';
import { SupplierList, Supplier } from '../components/SupplierList';
import { SupplierForm } from '../components/SupplierForm';

export const Suppliers: React.FC = () => {
  const [isFormOpen, setIsFormOpen] = useState(false);
  const [selectedSupplier, setSelectedSupplier] = useState<Supplier | undefined>();

  const handleEdit = (supplier: Supplier) => {
    setSelectedSupplier(supplier);
    setIsFormOpen(true);
  };

  const handleAddNew = () => {
    setSelectedSupplier(undefined);
    setIsFormOpen(true);
  };

  const handleSaved = () => {
    setIsFormOpen(false);
    // Reload the page or trigger a re-fetch in SupplierList
    // For simplicity here, we'll let SupplierList fetch on mount
    window.location.reload();
  };

  return (
    <div className="h-full bg-slate-50 dark:bg-slate-900">
      <SupplierList 
        onEdit={handleEdit} 
        onAddNew={handleAddNew} 
      />
      {isFormOpen && (
        <SupplierForm 
          supplier={selectedSupplier}
          onClose={() => setIsFormOpen(false)}
          onSaved={handleSaved}
        />
      )}
    </div>
  );
};
