import React, { useState } from 'react';
import { PurchaseOrderList } from '../components/PurchaseOrderList';
import { PurchaseOrderForm } from '../components/PurchaseOrderForm';

export const PurchaseOrders: React.FC = () => {
  const [isFormOpen, setIsFormOpen] = useState(false);
  const [selectedPoId, setSelectedPoId] = useState<string | null>(null);
  const [refreshKey, setRefreshKey] = useState(0);

  const handleAddNew = () => {
    setSelectedPoId(null);
    setIsFormOpen(true);
  };

  const handleEdit = (id: string) => {
    setSelectedPoId(id);
    setIsFormOpen(true);
  };

  const handleSaved = () => {
    setIsFormOpen(false);
    setSelectedPoId(null);
    setRefreshKey(prev => prev + 1);
  };

  return (
    <div className="h-full bg-slate-50 dark:bg-slate-900 p-6">
      {isFormOpen ? (
        <PurchaseOrderForm 
          purchaseOrderId={selectedPoId}
          onClose={() => {
            setIsFormOpen(false);
            setSelectedPoId(null);
          }}
          onSaved={handleSaved}
        />
      ) : (
        <PurchaseOrderList 
          key={refreshKey}
          onAddNew={handleAddNew}
          onEdit={handleEdit}
        />
      )}
    </div>
  );
};
