import React, { useState } from 'react';
import { ProductList } from '../components/ProductList';
import { CsvImportModal } from '../components/CsvImportModal';
import { CreateProductModal } from '../components/CreateProductModal';

export const Products = () => {
  const [isImportModalOpen, setIsImportModalOpen] = useState(false);
  const [isNewProductModalOpen, setIsNewProductModalOpen] = useState(false);

  return (
    <div className="p-6 max-w-7xl mx-auto">
      <ProductList 
        onImportClick={() => setIsImportModalOpen(true)} 
        onNewProductClick={() => setIsNewProductModalOpen(true)} 
      />
      <CsvImportModal isOpen={isImportModalOpen} onClose={() => setIsImportModalOpen(false)} />
      <CreateProductModal isOpen={isNewProductModalOpen} onClose={() => setIsNewProductModalOpen(false)} />
    </div>
  );
};
