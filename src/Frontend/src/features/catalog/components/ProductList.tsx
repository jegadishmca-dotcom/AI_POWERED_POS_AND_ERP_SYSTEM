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

  const downloadTemplate = () => {
    const headers = ['ProductCode', 'Name', 'TamilName', 'Description', 'Mrp', 'SellingPrice', 'PurchasePrice', 'Barcode', 'TaxSlabName', 'IsWeighable', 'HasExpiry'];
    const rows = [
      ['PROD-001', 'Sample Item 1', 'மாதிரி பொருள் 1', 'Sample description', '100.00', '80.00', '60.00', '2900000000001', 'GST 18%', 'FALSE', 'FALSE'],
      ['PROD-002', 'Sample Item 2', '', 'Another description', '50.00', '45.00', '35.00', '2900000000002', 'GST 5%', 'FALSE', 'FALSE']
    ];
    const csvContent = [headers.join(','), ...rows.map(r => r.join(','))].join('\n');
    const blob = new Blob([new Uint8Array([0xEF, 0xBB, 0xBF]), csvContent], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.setAttribute('download', 'Product_Import_Template.csv');
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
  };
  
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
        height: 40,
        displayValue: false, // Hide numeric text below barcode lines
        margin: 0
      });
    } catch (e) {
      console.error("Barcode generation failed", e);
      alert("Failed to generate barcode.");
      return;
    }

    const svgHtml = svg.outerHTML;

    // Date formatting: dd/mm/yyyy
    const formatDate = (date: Date): string => {
      const dd = String(date.getDate()).padStart(2, '0');
      const mm = String(date.getMonth() + 1).padStart(2, '0');
      const yyyy = date.getFullYear();
      return `${dd}/${mm}/${yyyy}`;
    };

    const today = new Date();
    const pkdDate = formatDate(today);
    
    // Default expiry date to 6 months later
    const exp = new Date();
    exp.setMonth(exp.getMonth() + 6);
    const expDate = formatDate(exp);

    const labelColHtml = `
      <div class="label-col">
        <div class="header-band">
          <svg viewBox="0 0 24 24" width="9" height="9" fill="#ffffff" style="margin-right: 2px;">
            <path d="M12 2C11.38 2 10.19 2.9 9.5 3.5C8.81 4.1 8.5 5.5 8.5 5.5C8.5 5.5 9.9 5.5 10.6 4.8C11.3 4.1 12 2 12 2Z" />
            <path d="M15.5 7.5C14.2 7.5 13.5 8.2 12.5 8.2C11.5 8.2 10.9 7.5 9.6 7.5C7.5 7.5 6 9.5 6 12.5C6 16.2 8.9 21.5 11 21.5C12.1 21.5 12.3 20.8 13.3 20.8C14.3 20.8 14.5 21.5 15.6 21.5C17.7 21.5 20 17 20 13.5C20 9.8 17.6 7.5 15.5 7.5Z" />
          </svg>
          <span class="header-title">ஆப்பிள் சூப்பர் மார்க்கெட்</span>
        </div>
        <div class="middle-row">
          <div class="code-vertical">${product.productCode || ''}</div>
          <div class="barcode-svg">${svgHtml}</div>
        </div>
        <div class="product-name">${product.name || ''}</div>
        <div class="price-row">₹ : ${(product.sellingPrice ?? 0).toFixed(2)}</div>
        <div class="dates-row">
          <span>PKD:${pkdDate}</span>
          <span>EXP:${expDate}</span>
        </div>
      </div>
    `;

    const html = `<!DOCTYPE html>
<html>
<head>
  <meta charset="UTF-8"/>
  <title>Print Barcode - ${product.name || ''}</title>
  <style>
    @page {
      size: 102mm 22mm;
      margin: 0;
    }
    * {
      margin: 0;
      padding: 0;
      box-sizing: border-box;
    }
    body {
      width: 102mm;
      height: 22mm;
      display: flex;
      flex-direction: row;
      justify-content: space-between;
      align-items: center;
      background: #fff;
      overflow: hidden;
      padding: 0 1.5mm;
    }
    .label-col {
      width: 31mm;
      height: 22mm;
      display: flex;
      flex-direction: column;
      justify-content: flex-start;
      overflow: hidden;
    }
    .header-band {
      background-color: #0b7a54;
      color: #fff;
      height: 4.5mm;
      display: flex;
      align-items: center;
      justify-content: center;
      border-radius: 2px;
      overflow: hidden;
    }
    .header-title {
      font-size: 7.5px;
      font-weight: bold;
      font-family: 'Latha', 'Arial', sans-serif;
      white-space: nowrap;
    }
    .middle-row {
      display: flex;
      flex-direction: row;
      align-items: center;
      justify-content: space-between;
      height: 9mm;
      margin-top: 0.5mm;
    }
    .code-vertical {
      font-size: 7px;
      font-weight: bold;
      writing-mode: vertical-rl;
      transform: rotate(180deg);
      white-space: nowrap;
      width: 3mm;
      text-align: center;
      font-family: Arial, sans-serif;
    }
    .barcode-svg {
      flex-grow: 1;
      display: flex;
      justify-content: center;
      align-items: center;
      height: 100%;
      overflow: hidden;
      padding-left: 1mm;
    }
    .barcode-svg svg {
      width: 100%;
      height: auto;
      max-height: 9mm;
    }
    .product-name {
      font-size: 7.5px;
      font-weight: bold;
      text-align: center;
      margin-top: 0.3mm;
      height: 3mm;
      line-height: 3mm;
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
      font-family: Arial, sans-serif;
    }
    .price-row {
      font-size: 9.5px;
      font-weight: bold;
      text-align: center;
      height: 3.2mm;
      line-height: 3.2mm;
      font-family: Arial, sans-serif;
    }
    .dates-row {
      display: flex;
      justify-content: space-between;
      font-size: 5.2px;
      font-family: monospace;
      height: 1.8mm;
      line-height: 1.8mm;
      padding: 0 0.5mm;
    }
    @media print {
      body {
        width: 102mm;
        height: 22mm;
      }
    }
  </style>
</head>
<body>
  ${labelColHtml}
  ${labelColHtml}
  ${labelColHtml}
  <script>
    window.onload = function() {
      window.print();
      setTimeout(function() { window.close(); }, 1000);
    };
  </script>
</body>
</html>`;

    const printWindow = window.open('', '_blank', 'width=450,height=250');
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
            onClick={downloadTemplate}
            className="px-3 py-2 border border-slate-350 dark:border-slate-700 hover:bg-slate-50 dark:hover:bg-slate-800 text-slate-700 dark:text-slate-300 rounded-md text-xs font-semibold transition"
            title="Download CSV Import Template"
          >
            Template CSV
          </button>
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
        <table className="min-w-full border-collapse">
          <thead className="bg-slate-50 dark:bg-slate-900 border-b-2 border-blue-200 dark:border-blue-800">
            <tr>
              <th className="px-6 py-3 text-left text-xs font-semibold text-slate-500 dark:text-slate-400 uppercase tracking-wider border-r-2 border-blue-200 dark:border-blue-800">Product</th>
              <th className="px-6 py-3 text-left text-xs font-semibold text-slate-500 dark:text-slate-400 uppercase tracking-wider border-r-2 border-blue-200 dark:border-blue-800">Barcode</th>
              <th className="px-6 py-3 text-left text-xs font-semibold text-slate-500 dark:text-slate-400 uppercase tracking-wider border-r-2 border-blue-200 dark:border-blue-800">Tamil Name</th>
              <th className="px-6 py-3 text-left text-xs font-semibold text-slate-500 dark:text-slate-400 uppercase tracking-wider border-r-2 border-blue-200 dark:border-blue-800">Tax Slab</th>
              <th className="px-6 py-3 text-right text-xs font-semibold text-slate-500 dark:text-slate-400 uppercase tracking-wider border-r-2 border-blue-200 dark:border-blue-800">Price (₹)</th>
              <th className="px-6 py-3 text-center text-xs font-semibold text-slate-500 dark:text-slate-400 uppercase tracking-wider">Actions</th>
            </tr>
          </thead>
          <tbody className="bg-white dark:bg-slate-800">
            {isLoading ? (
              <tr><td colSpan={6} className="text-center py-4 text-slate-500 border-b-2 border-blue-200 dark:border-blue-800/80">Loading...</td></tr>
            ) : !Array.isArray(products) || products.length === 0 ? (
              <tr><td colSpan={6} className="text-center py-4 text-slate-500 border-b-2 border-blue-200 dark:border-blue-800/80">No products found.</td></tr>
            ) : (
              products.map((p) => (
                <tr key={p.id} className="hover:bg-slate-50 dark:hover:bg-slate-700/50">
                  <td className="px-6 py-4 whitespace-nowrap text-sm font-medium text-slate-900 dark:text-white border-b-2 border-r-2 border-blue-200 dark:border-blue-800/80">
                    {p.name || 'Unnamed Product'}
                    <div className="text-xs text-slate-500">{p.productCode || 'N/A'}</div>
                  </td>
                  <td className="px-6 py-4 whitespace-nowrap text-sm text-slate-500 dark:text-slate-400 border-b-2 border-r-2 border-blue-200 dark:border-blue-800/80">{p.primaryBarcode || '-'}</td>
                  <td className="px-6 py-4 whitespace-nowrap text-sm text-slate-500 dark:text-slate-400 font-tamil border-b-2 border-r-2 border-blue-200 dark:border-blue-800/80">{p.tamilName || '-'}</td>
                  <td className="px-6 py-4 whitespace-nowrap text-sm text-slate-500 dark:text-slate-400 border-b-2 border-r-2 border-blue-200 dark:border-blue-800/80">
                    <span className="px-2 py-0.5 bg-slate-100 dark:bg-slate-900 border border-slate-200 dark:border-slate-800 rounded text-xs font-semibold text-slate-700 dark:text-slate-300">{p.taxSlabName || 'GST 0%'}</span>
                  </td>
                  <td className="px-6 py-4 whitespace-nowrap text-sm text-slate-900 dark:text-white text-right font-semibold border-b-2 border-r-2 border-blue-200 dark:border-blue-800/80">
                    {(p.sellingPrice ?? 0).toFixed(2)}
                  </td>
                  <td className="px-6 py-4 whitespace-nowrap text-sm font-medium text-center border-b-2 border-blue-200 dark:border-blue-800/80">
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
