import React, { useState } from 'react';
import { ProductList } from '../components/ProductList';
import { CsvImportModal } from '../components/CsvImportModal';

export const Products = () => {
  const [isImportModalOpen, setIsImportModalOpen] = useState(false);

  return (
    <div className="p-6 max-w-7xl mx-auto">
      <ProductList onImportClick={() => setIsImportModalOpen(true)} />
      <CsvImportModal isOpen={isImportModalOpen} onClose={() => setIsImportModalOpen(false)} />
    </div>
  );
};
