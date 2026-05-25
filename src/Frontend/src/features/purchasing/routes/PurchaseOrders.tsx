import React, { useState } from 'react';
import { PurchaseOrderList } from '../components/PurchaseOrderList';
import { PurchaseOrderForm } from '../components/PurchaseOrderForm';

export const PurchaseOrders: React.FC = () => {
  const [isFormOpen, setIsFormOpen] = useState(false);
  const [refreshKey, setRefreshKey] = useState(0);

  const handleAddNew = () => {
    setIsFormOpen(true);
  };

  const handleSaved = () => {
    setIsFormOpen(false);
    setRefreshKey(prev => prev + 1);
  };

  return (
    <div className="h-full bg-slate-50 dark:bg-slate-900 p-6">
      {isFormOpen ? (
        <PurchaseOrderForm 
          onClose={() => setIsFormOpen(false)}
          onSaved={handleSaved}
        />
      ) : (
        <PurchaseOrderList 
          key={refreshKey}
          onAddNew={handleAddNew}
        />
      )}
    </div>
  );
};
