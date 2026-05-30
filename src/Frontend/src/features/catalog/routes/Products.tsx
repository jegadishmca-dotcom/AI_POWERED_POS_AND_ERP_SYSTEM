import React, { useState } from 'react';
import { ProductList } from '../components/ProductList';
import { CsvImportModal } from '../components/CsvImportModal';
import { CreateProductModal } from '../components/CreateProductModal';

export const Products = () => {
  const [isImportModalOpen, setIsImportModalOpen] = useState(false);
  const [isNewProductModalOpen, setIsNewProductModalOpen] = useState(false);
  const [editingProduct, setEditingProduct] = useState<any>(null);

  const handleEditProduct = (product: any) => {
    setEditingProduct(product);
    setIsNewProductModalOpen(true);
  };

  const handleCloseModal = () => {
    setIsNewProductModalOpen(false);
    setEditingProduct(null);
  };

  return (
    <div className="p-6 max-w-7xl mx-auto">
      <ProductList 
        onImportClick={() => setIsImportModalOpen(true)} 
        onNewProductClick={() => { setEditingProduct(null); setIsNewProductModalOpen(true); }} 
        onEditClick={handleEditProduct}
      />
      <CsvImportModal isOpen={isImportModalOpen} onClose={() => setIsImportModalOpen(false)} />
      <CreateProductModal 
        isOpen={isNewProductModalOpen} 
        onClose={handleCloseModal} 
        editingProduct={editingProduct}
      />
    </div>
  );
};
