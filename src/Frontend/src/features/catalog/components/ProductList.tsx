import React, { useState, useEffect } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { searchProducts, deleteProduct } from '../api/catalog.api';
import { Search, Package, Plus, Edit2, Trash2, Printer } from 'lucide-react';
import JsBarcode from 'jsbarcode';

export const ProductList = ({ 
  onImportClick, 
  onNewProductClick,
  onEditClick
}: { 
  onImportClick: () => void; 
  onNewProductClick: () => void; 
  onEditClick: (product: any) => void;
}) => {
  const queryClient = useQueryClient();
  const [searchTerm, setSearchTerm] = useState('');
  
  // Custom quick debounce implementation for this snippet
  const [debouncedTerm, setDebouncedTerm] = useState(searchTerm);
  useEffect(() => {
    const timer = setTimeout(() => setDebouncedTerm(searchTerm), 300);
    return () => clearTimeout(timer);
  }, [searchTerm]);

  const { data: products, isLoading } = useQuery({
    queryKey: ['products', 'search', debouncedTerm],
    queryFn: () => searchProducts(debouncedTerm),
  });

  const deleteMutation = useMutation({
    mutationFn: deleteProduct,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['products'] });
    },
    onError: (err: any) => {
      alert("Failed to delete product: " + (err.response?.data?.message || err.message));
    }
  });

  const handleDelete = (id: string) => {
    if (window.confirm("Are you sure you want to delete this product?")) {
      deleteMutation.mutate(id);
    }
  };

  const handlePrintBarcode = (product: any) => {
    const barcodeValue = product.primaryBarcode || product.productCode;
    if (!barcodeValue) {
      alert("This product has no barcode or product code to print.");
      return;
    }

    // Create temporary container to generate barcode SVG
    const tempContainer = document.createElement('div');
    const svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
    tempContainer.appendChild(svg);
    
    try {
      JsBarcode(svg, barcodeValue, {
        format: "CODE128",
        width: 2,
        height: 60,
        displayValue: true,
        fontSize: 14,
        font: "monospace"
      });
    } catch (e) {
      console.error("Barcode generation failed", e);
      alert("Failed to generate barcode.");
      return;
    }

    const svgHtml = svg.outerHTML;

    const html = `<!DOCTYPE html>
<html>
<head>
  <meta charset="UTF-8"/>
  <title>Print Barcode - ${product.name}</title>
  <style>
    body {
      margin: 0;
      padding: 10px;
      font-family: 'Courier New', Courier, monospace;
      text-align: center;
      width: 50mm; /* Standard label size: 50mm x 25mm */
      height: 25mm;
      box-sizing: border-box;
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
    }
    .store-name {
      font-size: 8px;
      font-weight: bold;
      text-transform: uppercase;
      margin-bottom: 2px;
      letter-spacing: 0.5px;
    }
    .product-name {
      font-size: 9px;
      font-weight: bold;
      margin-bottom: 2px;
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
      width: 100%;
    }
    .price-tag {
      font-size: 10px;
      font-weight: 900;
      margin-bottom: 2px;
    }
    .barcode-svg svg {
      width: 100%;
      height: auto;
      max-height: 12mm;
    }
    @media print {
      body { width: 50mm; height: 25mm; }
    }
  </style>
</head>
<body>
  <div class="store-name">Apple Supermarket</div>
  <div class="product-name">${product.name}</div>
  <div class="price-tag">Price: Rs.${product.sellingPrice.toFixed(2)}</div>
  <div class="barcode-svg">${svgHtml}</div>
  <script>
    window.onload = function() {
      window.print();
      setTimeout(function() { window.close(); }, 1000);
    };
  </script>
</body>
</html>`;

    const printWindow = window.open('', '_blank', 'width=300,height=300');
    if (printWindow) {
      printWindow.document.open();
      printWindow.document.write(html);
      printWindow.document.close();
    } else {
      alert("Please allow popups for this site to print barcodes.");
    }
  };

  return (
    <div className="bg-white dark:bg-slate-800 shadow rounded-lg p-6">
      <div className="flex justify-between items-center mb-6">
        <h2 className="text-xl font-bold text-slate-800 dark:text-white flex items-center">
          <Package className="mr-2" /> Product Catalog
        </h2>
        <div className="flex space-x-2">
          <button 
            onClick={onImportClick}
            className="px-4 py-2 bg-emerald-600 text-white rounded-md text-sm hover:bg-emerald-700 transition"
          >
            Import CSV
          </button>
          <button 
            onClick={onNewProductClick}
            className="px-4 py-2 bg-blue-600 text-white rounded-md text-sm hover:bg-blue-700 transition flex items-center"
          >
            <Plus className="w-4 h-4 mr-1" /> New Product
          </button>
        </div>
      </div>

      <div className="relative mb-6">
        <div className="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none">
          <Search className="h-5 w-5 text-slate-400" />
        </div>
        <input
          type="text"
          className="block w-full pl-10 pr-3 py-2 border border-slate-300 dark:border-slate-700 rounded-md leading-5 bg-white dark:bg-slate-900 placeholder-slate-500 dark:placeholder-slate-400 text-slate-900 dark:text-white focus:outline-none focus:ring-blue-500 focus:border-blue-500 sm:text-sm"
          placeholder="Search by name, barcode, code, or Tamil name..."
          value={searchTerm}
          onChange={(e) => setSearchTerm(e.target.value)}
        />
      </div>

      <div className="overflow-x-auto">
        <table className="min-w-full divide-y divide-slate-200 dark:divide-slate-700">
          <thead className="bg-slate-50 dark:bg-slate-900">
            <tr>
              <th className="px-6 py-3 text-left text-xs font-medium text-slate-500 dark:text-slate-400 uppercase tracking-wider">Product</th>
              <th className="px-6 py-3 text-left text-xs font-medium text-slate-500 dark:text-slate-400 uppercase tracking-wider">Barcode</th>
              <th className="px-6 py-3 text-left text-xs font-medium text-slate-500 dark:text-slate-400 uppercase tracking-wider">Tamil Name</th>
              <th className="px-6 py-3 text-right text-xs font-medium text-slate-500 dark:text-slate-400 uppercase tracking-wider">Price (₹)</th>
              <th className="px-6 py-3 text-center text-xs font-medium text-slate-500 dark:text-slate-400 uppercase tracking-wider">Actions</th>
            </tr>
          </thead>
          <tbody className="bg-white dark:bg-slate-800 divide-y divide-slate-200 dark:divide-slate-700">
            {isLoading ? (
              <tr><td colSpan={5} className="text-center py-4 text-slate-500">Loading...</td></tr>
            ) : !Array.isArray(products) || products.length === 0 ? (
              <tr><td colSpan={5} className="text-center py-4 text-slate-500">No products found.</td></tr>
            ) : (
              products.map((p) => (
                <tr key={p.id} className="hover:bg-slate-50 dark:hover:bg-slate-700/50">
                  <td className="px-6 py-4 whitespace-nowrap text-sm font-medium text-slate-900 dark:text-white">
                    {p.name || 'Unnamed Product'}
                    <div className="text-xs text-slate-500">{p.productCode || 'N/A'}</div>
                  </td>
                  <td className="px-6 py-4 whitespace-nowrap text-sm text-slate-500 dark:text-slate-400">{p.primaryBarcode || '-'}</td>
                  <td className="px-6 py-4 whitespace-nowrap text-sm text-slate-500 dark:text-slate-400 font-tamil">{p.tamilName || '-'}</td>
                  <td className="px-6 py-4 whitespace-nowrap text-sm text-slate-900 dark:text-white text-right font-semibold">
                    {(p.sellingPrice ?? 0).toFixed(2)}
                  </td>
                  <td className="px-6 py-4 whitespace-nowrap text-sm font-medium text-center">
                    <div className="flex items-center justify-center space-x-3">
                      <button 
                        onClick={() => onEditClick(p)} 
                        className="text-blue-600 hover:text-blue-900 dark:text-blue-400 dark:hover:text-blue-300 flex items-center"
                        title="Edit Product"
                      >
                        <Edit2 className="w-4 h-4 mr-0.5" /> Edit
                      </button>
                      <button 
                        onClick={() => handleDelete(p.id)} 
                        className="text-red-600 hover:text-red-900 dark:text-red-400 dark:hover:text-red-300 flex items-center"
                        title="Delete Product"
                      >
                        <Trash2 className="w-4 h-4 mr-0.5" /> Delete
                      </button>
                      <button 
                        onClick={() => handlePrintBarcode(p)} 
                        className="text-emerald-600 hover:text-emerald-900 dark:text-emerald-400 dark:hover:text-emerald-300 flex items-center"
                        title="Print Barcode Label"
                      >
                        <Printer className="w-4 h-4 mr-0.5" /> Print Barcode
                      </button>
                    </div>
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
};
