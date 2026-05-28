import React, { useState, useEffect } from 'react';
import { 
  Percent, 
  DollarSign, 
  TrendingUp, 
  Layers, 
  AlertTriangle, 
  Clock, 
  Calendar, 
  Download, 
  Search, 
  FileText 
} from 'lucide-react';
import { 
  getGstReport, 
  getMarginReport, 
  getInventoryInsights, 
  GstReportRow, 
  MarginReport, 
  InventoryInsights 
} from '../api/reports.api';

export const ReportsHub: React.FC = () => {
  const [activeTab, setActiveTab] = useState<'margins' | 'gst' | 'inventory'>('margins');
  const [fromDate, setFromDate] = useState<string>(
    new Date(Date.now() - 30 * 24 * 60 * 60 * 1000).toISOString().split('T')[0]
  );
  const [toDate, setToDate] = useState<string>(
    new Date().toISOString().split('T')[0]
  );
  
  const [marginData, setMarginData] = useState<MarginReport | null>(null);
  const [gstData, setGstData] = useState<GstReportRow[]>([]);
  const [inventoryData, setInventoryData] = useState<InventoryInsights | null>(null);
  
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const fetchReportsData = async () => {
    try {
      setLoading(true);
      setError(null);
      
      if (activeTab === 'margins') {
        const data = await getMarginReport(fromDate, toDate);
        setMarginData(data);
      } else if (activeTab === 'gst') {
        const data = await getGstReport(fromDate, toDate);
        setGstData(data);
      } else if (activeTab === 'inventory') {
        const data = await getInventoryInsights();
        setInventoryData(data);
      }
    } catch (err: any) {
      console.error('Failed to load reports:', err);
      setError('Failed to load report data. Please check connection and try again.');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchReportsData();
  }, [activeTab, fromDate, toDate]);

  const handleExportCSV = (type: string) => {
    let headers: string[] = [];
    let rows: string[][] = [];
    let filename = '';

    if (type === 'gst') {
      filename = `GST_Tax_Report_${fromDate}_to_${toDate}.csv`;
      headers = ['Tax Rate %', 'Taxable Amount', 'CGST Collected', 'SGST Collected', 'Cess Collected', 'Total Tax Collected'];
      rows = gstData.map(r => [
        `${r.taxRate}%`,
        r.taxableAmount.toFixed(2),
        r.cgstCollected.toFixed(2),
        r.sgstCollected.toFixed(2),
        r.cessCollected.toFixed(2),
        r.totalTax.toFixed(2)
      ]);
    } else if (type === 'category-margins' && marginData) {
      filename = `Category_Margins_Report_${fromDate}_to_${toDate}.csv`;
      headers = ['Category Name', 'Qty Sold', 'Revenue', 'Cost of Sales', 'Gross Profit', 'Margin %'];
      rows = marginData.categoryMargins.map(c => [
        c.categoryName,
        c.quantitySold.toString(),
        c.revenue.toFixed(2),
        c.cost.toFixed(2),
        c.profit.toFixed(2),
        `${c.marginPercentage.toFixed(2)}%`
      ]);
    } else if (type === 'product-margins' && marginData) {
      filename = `Product_Margins_Report_${fromDate}_to_${toDate}.csv`;
      headers = ['Product Name', 'Qty Sold', 'Revenue', 'Cost of Sales', 'Gross Profit', 'Margin %'];
      rows = marginData.productMargins.map(p => [
        p.productName,
        p.quantitySold.toString(),
        p.revenue.toFixed(2),
        p.cost.toFixed(2),
        p.profit.toFixed(2),
        `${p.marginPercentage.toFixed(2)}%`
      ]);
    } else if (type === 'low-stock' && inventoryData) {
      filename = `Low_Stock_Alerts.csv`;
      headers = ['Product Code', 'Product Name', 'Current Stock', 'Reorder Point'];
      rows = inventoryData.lowStockItems.map(i => [
        i.productCode,
        i.productName,
        i.currentStock.toString(),
        i.reorderPoint.toString()
      ]);
    } else if (type === 'expiring' && inventoryData) {
      filename = `Expiring_Batches_Report.csv`;
      headers = ['Product Code', 'Product Name', 'Batch Number', 'Expiry Date', 'Days Remaining'];
      rows = inventoryData.nearExpiryBatches.map(b => [
        b.productCode,
        b.productName,
        b.batchNumber,
        b.expiryDate ? new Date(b.expiryDate).toLocaleDateString() : 'N/A',
        b.daysRemaining.toString()
      ]);
    }

    if (rows.length === 0) {
      alert('No data available to export.');
      return;
    }

    const csvContent = [
      headers.join(','),
      ...rows.map(row => row.map(cell => `"${cell.replace(/"/g, '""')}"`).join(','))
    ].join('\n');

    const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
    const link = document.createElement('a');
    link.href = URL.createObjectURL(blob);
    link.setAttribute('download', filename);
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
  };

  return (
    <div className="p-8 bg-slate-50 min-h-screen">
      {/* Header */}
      <div className="flex justify-between items-center mb-8">
        <div>
          <h1 className="text-3xl font-black text-slate-800">Reports & Insights</h1>
          <p className="text-gray-500">Supermarket gross margins, tax filings, and stock metrics</p>
        </div>

        {/* Date Filters (Hidden on Inventory Health since it is live snapshot) */}
        {activeTab !== 'inventory' && (
          <div className="flex items-center gap-4 bg-white p-3 rounded-xl shadow-sm border border-slate-100">
            <div className="flex items-center text-gray-500">
              <Calendar className="w-4 h-4 mr-2" />
              <span className="text-xs font-bold uppercase">Date Filter</span>
            </div>
            <div className="flex items-center gap-2">
              <input 
                type="date" 
                value={fromDate}
                onChange={(e) => setFromDate(e.target.value)}
                className="px-3 py-1.5 border rounded-lg text-sm font-semibold text-slate-700 bg-slate-50 focus:outline-none focus:ring-2 focus:ring-indigo-500"
              />
              <span className="text-gray-400">to</span>
              <input 
                type="date" 
                value={toDate}
                onChange={(e) => setToDate(e.target.value)}
                className="px-3 py-1.5 border rounded-lg text-sm font-semibold text-slate-700 bg-slate-50 focus:outline-none focus:ring-2 focus:ring-indigo-500"
              />
            </div>
          </div>
        )}
      </div>

      {/* Tabs Menu */}
      <div className="flex border-b border-slate-200 mb-8 gap-6">
        <button 
          onClick={() => setActiveTab('margins')}
          className={`pb-4 text-sm font-bold border-b-2 transition-all ${
            activeTab === 'margins' 
              ? 'border-indigo-600 text-indigo-600 font-extrabold' 
              : 'border-transparent text-slate-400 hover:text-slate-600'
          }`}
        >
          Sales & Profit Margins
        </button>
        <button 
          onClick={() => setActiveTab('gst')}
          className={`pb-4 text-sm font-bold border-b-2 transition-all ${
            activeTab === 'gst' 
              ? 'border-indigo-600 text-indigo-600 font-extrabold' 
              : 'border-transparent text-slate-400 hover:text-slate-600'
          }`}
        >
          GST Tax Filings (Sales)
        </button>
        <button 
          onClick={() => setActiveTab('inventory')}
          className={`pb-4 text-sm font-bold border-b-2 transition-all ${
            activeTab === 'inventory' 
              ? 'border-indigo-600 text-indigo-600 font-extrabold' 
              : 'border-transparent text-slate-400 hover:text-slate-600'
          }`}
        >
          Inventory Health & Alerts
        </button>
      </div>

      {/* Loading & Error Overlays */}
      {loading && (
        <div className="h-[400px] bg-white rounded-2xl flex flex-col justify-center items-center shadow-sm border border-slate-100">
          <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-indigo-600 mb-4"></div>
          <p className="text-gray-500 font-medium animate-pulse">Generating report summary...</p>
        </div>
      )}

      {error && !loading && (
        <div className="h-[400px] bg-white rounded-2xl flex flex-col justify-center items-center shadow-sm border border-slate-100 p-8 text-center">
          <div className="bg-red-50 border border-red-200 text-red-700 px-6 py-5 rounded-xl max-w-md">
            <h3 className="font-bold text-lg mb-2">Failed to Load Report</h3>
            <p className="text-sm text-red-600 mb-4">{error}</p>
            <button onClick={fetchReportsData} className="px-6 py-2 bg-red-600 hover:bg-red-700 text-white rounded-lg font-bold text-sm transition shadow-sm">
              Retry
            </button>
          </div>
        </div>
      )}

      {/* Reports Content Panels */}
      {!loading && !error && (
        <div>
          {/* TAB 1: PROFIT MARGINS */}
          {activeTab === 'margins' && marginData && (
            <div className="space-y-8">
              {/* Summary KPIs */}
              <div className="grid grid-cols-1 md:grid-cols-4 gap-6">
                <div className="bg-white p-6 rounded-xl shadow-sm border border-slate-100">
                  <p className="text-sm font-bold text-gray-500 mb-1">Total Sales Revenue</p>
                  <h2 className="text-3xl font-black text-slate-800">₹{(marginData.summary?.totalRevenue || 0).toLocaleString()}</h2>
                  <p className="text-xs text-slate-400 mt-2">Gross invoice amounts</p>
                </div>
                <div className="bg-white p-6 rounded-xl shadow-sm border border-slate-100">
                  <p className="text-sm font-bold text-gray-500 mb-1">Cost of Goods Sold (COGS)</p>
                  <h2 className="text-3xl font-black text-slate-800">₹{(marginData.summary?.totalCost || 0).toLocaleString()}</h2>
                  <p className="text-xs text-slate-400 mt-2">Total purchase cost basis</p>
                </div>
                <div className="bg-white p-6 rounded-xl shadow-sm border border-slate-100">
                  <p className="text-sm font-bold text-gray-500 mb-1">Gross Profit</p>
                  <h2 className="text-3xl font-black text-slate-800 text-indigo-600">₹{(marginData.summary?.totalProfit || 0).toLocaleString()}</h2>
                  <p className="text-xs text-slate-400 mt-2">Revenue minus product cost</p>
                </div>
                <div className="bg-white p-6 rounded-xl shadow-sm border border-slate-100 flex justify-between items-center">
                  <div>
                    <p className="text-sm font-bold text-gray-500 mb-1">Gross Profit Margin</p>
                    <h2 className="text-3xl font-black text-emerald-500">{(marginData.summary?.marginPercentage || 0).toFixed(2)}%</h2>
                    <p className="text-xs text-slate-400 mt-2">Average markup percentage</p>
                  </div>
                  <div className="w-12 h-12 rounded-full bg-emerald-50 text-emerald-600 flex items-center justify-center">
                    <Percent className="w-6 h-6" />
                  </div>
                </div>
              </div>

              {/* Detail Panels */}
              <div className="grid grid-cols-1 lg:grid-cols-2 gap-8">
                {/* Category Profitability */}
                <div className="bg-white p-6 rounded-xl shadow-sm border border-slate-100">
                  <div className="flex justify-between items-center mb-6">
                    <h3 className="text-lg font-bold text-slate-800">Category Profitability</h3>
                    <button onClick={() => handleExportCSV('category-margins')} className="text-xs text-indigo-600 font-bold hover:underline flex items-center">
                      <Download className="w-3.5 h-3.5 mr-1" /> Export CSV
                    </button>
                  </div>
                  <div className="overflow-x-auto">
                    <table className="w-full text-left text-sm">
                      <thead>
                        <tr className="border-b text-slate-400 uppercase text-xs font-bold">
                          <th className="pb-3">Category</th>
                          <th className="pb-3 text-right">Qty</th>
                          <th className="pb-3 text-right">Revenue</th>
                          <th className="pb-3 text-right">Margin %</th>
                        </tr>
                      </thead>
                      <tbody className="divide-y text-slate-700">
                        {marginData.categoryMargins.map((cat, idx) => (
                          <tr key={idx} className="hover:bg-slate-50 transition-colors">
                            <td className="py-4 font-bold text-slate-800">{cat.categoryName}</td>
                            <td className="py-4 text-right font-medium">{cat.quantitySold}</td>
                            <td className="py-4 text-right font-bold">₹{cat.revenue.toLocaleString()}</td>
                            <td className="py-4 text-right font-extrabold text-emerald-500">{cat.marginPercentage.toFixed(1)}%</td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                </div>

                {/* Top Profitable Products */}
                <div className="bg-white p-6 rounded-xl shadow-sm border border-slate-100">
                  <div className="flex justify-between items-center mb-6">
                    <h3 className="text-lg font-bold text-slate-800">Top Profitable Products</h3>
                    <button onClick={() => handleExportCSV('product-margins')} className="text-xs text-indigo-600 font-bold hover:underline flex items-center">
                      <Download className="w-3.5 h-3.5 mr-1" /> Export CSV
                    </button>
                  </div>
                  <div className="space-y-4">
                    {marginData.productMargins.map((prod, idx) => (
                      <div key={idx} className="flex flex-col border-b pb-3 last:border-0">
                        <div className="flex justify-between items-center mb-2">
                          <div className="max-w-[70%]">
                            <p className="font-bold text-slate-800 text-sm truncate" title={prod.productName}>{prod.productName}</p>
                            <p className="text-xs text-gray-400">{prod.quantitySold} Units Sold • Sales ₹{prod.revenue.toLocaleString()}</p>
                          </div>
                          <div className="text-right">
                            <p className="font-extrabold text-indigo-600 text-sm">₹{prod.profit.toLocaleString()}</p>
                            <p className="text-xs font-bold text-emerald-500">{prod.marginPercentage.toFixed(1)}% margin</p>
                          </div>
                        </div>
                        {/* Margin Bar */}
                        <div className="w-full bg-slate-100 rounded-full h-1.5">
                          <div 
                            className="bg-indigo-600 h-1.5 rounded-full" 
                            style={{ width: `${Math.min(100, Math.max(0, prod.marginPercentage))}%` }}
                          ></div>
                        </div>
                      </div>
                    ))}
                  </div>
                </div>
              </div>
            </div>
          )}

          {/* TAB 2: GST TAX FILINGS */}
          {activeTab === 'gst' && (
            <div className="space-y-8">
              {/* Summary GST Card */}
              <div className="bg-white p-6 rounded-xl shadow-sm border border-slate-100 max-w-md flex justify-between items-center">
                <div>
                  <p className="text-sm font-bold text-gray-500 mb-1">Total GST collected</p>
                  <h2 className="text-3xl font-black text-indigo-600">₹{gstData.reduce((acc, row) => acc + row.totalTax, 0).toLocaleString()}</h2>
                  <p className="text-xs text-slate-400 mt-2">Combined CGST + SGST + Cess</p>
                </div>
                <div className="w-12 h-12 rounded-full bg-indigo-50 text-indigo-600 flex items-center justify-center">
                  <FileText className="w-6 h-6" />
                </div>
              </div>

              {/* GST Table */}
              <div className="bg-white p-6 rounded-xl shadow-sm border border-slate-100">
                <div className="flex justify-between items-center mb-6">
                  <h3 className="text-lg font-bold text-slate-800">GST Sales Summary (Rate-wise)</h3>
                  <button onClick={() => handleExportCSV('gst')} className="px-4 py-2 bg-indigo-600 text-white font-bold rounded-lg hover:bg-indigo-700 text-xs shadow-sm flex items-center transition">
                    <Download className="w-3.5 h-3.5 mr-2" /> Export Tax CSV
                  </button>
                </div>
                <div className="overflow-x-auto">
                  <table className="w-full text-left text-sm">
                    <thead>
                      <tr className="border-b text-slate-400 uppercase text-xs font-bold">
                        <th className="pb-3">GST Rate</th>
                        <th className="pb-3 text-right">Taxable Sales (Base)</th>
                        <th className="pb-3 text-right">Output CGST</th>
                        <th className="pb-3 text-right">Output SGST</th>
                        <th className="pb-3 text-right">Cess</th>
                        <th className="pb-3 text-right font-black">Total Tax Collected</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y text-slate-700">
                      {gstData.map((row, idx) => (
                        <tr key={idx} className="hover:bg-slate-50 transition-colors">
                          <td className="py-4 font-black text-slate-800 bg-slate-50 px-3 rounded-lg">{row.taxRate}% Rate</td>
                          <td className="py-4 text-right font-medium">₹{row.taxableAmount.toLocaleString()}</td>
                          <td className="py-4 text-right text-slate-500">₹{row.cgstCollected.toLocaleString()}</td>
                          <td className="py-4 text-right text-slate-500">₹{row.sgstCollected.toLocaleString()}</td>
                          <td className="py-4 text-right text-slate-500">₹{row.cessCollected.toLocaleString()}</td>
                          <td className="py-4 text-right font-bold text-indigo-600">₹{row.totalTax.toLocaleString()}</td>
                        </tr>
                      ))}
                      {gstData.length > 0 && (
                        <tr className="bg-slate-50 font-bold border-t-2 border-slate-200">
                          <td className="py-4 px-3 text-slate-800">Total Summary</td>
                          <td className="py-4 text-right text-slate-800">₹{gstData.reduce((acc, r) => acc + r.taxableAmount, 0).toLocaleString()}</td>
                          <td className="py-4 text-right text-slate-600">₹{gstData.reduce((acc, r) => acc + r.cgstCollected, 0).toLocaleString()}</td>
                          <td className="py-4 text-right text-slate-600">₹{gstData.reduce((acc, r) => acc + r.sgstCollected, 0).toLocaleString()}</td>
                          <td className="py-4 text-right text-slate-600">₹{gstData.reduce((acc, r) => acc + r.cessCollected, 0).toLocaleString()}</td>
                          <td className="py-4 text-right text-indigo-600 font-extrabold text-lg">₹{gstData.reduce((acc, r) => acc + r.totalTax, 0).toLocaleString()}</td>
                        </tr>
                      )}
                    </tbody>
                  </table>
                </div>
              </div>
            </div>
          )}

          {/* TAB 3: INVENTORY HEALTH & ALERTS */}
          {activeTab === 'inventory' && inventoryData && (
            <div className="space-y-8">
              {/* Summary KPIs */}
              <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
                <div className="bg-white p-6 rounded-xl shadow-sm border border-slate-100 flex justify-between items-center">
                  <div>
                    <p className="text-sm font-bold text-gray-500 mb-1">Total Stock Assets Valuation</p>
                    <h2 className="text-3xl font-black text-slate-800">₹{inventoryData.totalValuation.toLocaleString()}</h2>
                    <p className="text-xs text-slate-400 mt-2">Valued at active cost prices</p>
                  </div>
                  <div className="w-12 h-12 rounded-full bg-slate-100 text-slate-600 flex items-center justify-center">
                    <Layers className="w-6 h-6" />
                  </div>
                </div>

                <div className="bg-white p-6 rounded-xl shadow-sm border border-slate-100 flex justify-between items-center">
                  <div>
                    <p className="text-sm font-bold text-gray-500 mb-1">Low Stock Alerts</p>
                    <h2 className={`text-3xl font-black ${inventoryData.lowStockCount > 0 ? 'text-amber-500' : 'text-emerald-500'}`}>
                      {inventoryData.lowStockCount} Items
                    </h2>
                    <p className="text-xs text-slate-400 mt-2">Stock is below reorder thresholds</p>
                  </div>
                  <div className={`w-12 h-12 rounded-full flex items-center justify-center ${inventoryData.lowStockCount > 0 ? 'bg-amber-50 text-amber-600' : 'bg-emerald-50 text-emerald-600'}`}>
                    <AlertTriangle className="w-6 h-6" />
                  </div>
                </div>

                <div className="bg-white p-6 rounded-xl shadow-sm border border-slate-100 flex justify-between items-center">
                  <div>
                    <p className="text-sm font-bold text-gray-500 mb-1">Expiring Batches (Within 30 Days)</p>
                    <h2 className={`text-3xl font-black ${inventoryData.nearExpiryCount > 0 ? 'text-rose-500' : 'text-emerald-500'}`}>
                      {inventoryData.nearExpiryCount} Batches
                    </h2>
                    <p className="text-xs text-slate-400 mt-2">Critical attention needed</p>
                  </div>
                  <div className={`w-12 h-12 rounded-full flex items-center justify-center ${inventoryData.nearExpiryCount > 0 ? 'bg-rose-50 text-rose-600' : 'bg-emerald-50 text-emerald-600'}`}>
                    <Clock className="w-6 h-6" />
                  </div>
                </div>
              </div>

              {/* Detail Panels */}
              <div className="grid grid-cols-1 lg:grid-cols-2 gap-8">
                {/* Critical Low Stock Items List */}
                <div className="bg-white p-6 rounded-xl shadow-sm border border-slate-100">
                  <div className="flex justify-between items-center mb-6">
                    <h3 className="text-lg font-bold text-slate-800">Critical Low Stock Items</h3>
                    <button onClick={() => handleExportCSV('low-stock')} className="text-xs text-indigo-600 font-bold hover:underline flex items-center">
                      <Download className="w-3.5 h-3.5 mr-1" /> Export CSV
                    </button>
                  </div>
                  {inventoryData.lowStockItems.length === 0 ? (
                    <div className="h-60 flex items-center justify-center text-gray-400 font-medium">
                      All products are adequately stocked.
                    </div>
                  ) : (
                    <div className="overflow-x-auto">
                      <table className="w-full text-left text-sm">
                        <thead>
                          <tr className="border-b text-slate-400 uppercase text-xs font-bold">
                            <th className="pb-3">Product</th>
                            <th className="pb-3 text-right">In Stock</th>
                            <th className="pb-3 text-right">Reorder Threshold</th>
                          </tr>
                        </thead>
                        <tbody className="divide-y text-slate-700">
                          {inventoryData.lowStockItems.map((item, idx) => (
                            <tr key={idx} className="hover:bg-slate-50 transition-colors">
                              <td className="py-4">
                                <p className="font-bold text-slate-800">{item.productName}</p>
                                <p className="text-xs text-slate-400">{item.productCode}</p>
                              </td>
                              <td className="py-4 text-right font-black text-rose-500 bg-rose-50 px-2 rounded">{item.currentStock}</td>
                              <td className="py-4 text-right font-semibold text-slate-500">{item.reorderPoint}</td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    </div>
                  )}
                </div>

                {/* Near Expiry Batches List */}
                <div className="bg-white p-6 rounded-xl shadow-sm border border-slate-100">
                  <div className="flex justify-between items-center mb-6">
                    <h3 className="text-lg font-bold text-slate-800">Expired & Expiring Batches</h3>
                    <button onClick={() => handleExportCSV('expiring')} className="text-xs text-indigo-600 font-bold hover:underline flex items-center">
                      <Download className="w-3.5 h-3.5 mr-1" /> Export CSV
                    </button>
                  </div>
                  {inventoryData.nearExpiryBatches.length === 0 ? (
                    <div className="h-60 flex items-center justify-center text-gray-400 font-medium">
                      No expired or near-expiry batches detected.
                    </div>
                  ) : (
                    <div className="overflow-x-auto">
                      <table className="w-full text-left text-sm">
                        <thead>
                          <tr className="border-b text-slate-400 uppercase text-xs font-bold">
                            <th className="pb-3">Batch Detail</th>
                            <th className="pb-3 text-right">Expiry Date</th>
                            <th className="pb-3 text-right">Status</th>
                          </tr>
                        </thead>
                        <tbody className="divide-y text-slate-700">
                          {inventoryData.nearExpiryBatches.map((batch, idx) => (
                            <tr key={idx} className="hover:bg-slate-50 transition-colors">
                              <td className="py-4">
                                <p className="font-bold text-slate-800">{batch.productName}</p>
                                <p className="text-xs text-slate-400">Batch: <span className="font-semibold text-slate-600">{batch.batchNumber}</span> • Code: {batch.productCode}</p>
                              </td>
                              <td className="py-4 text-right font-medium">
                                {batch.expiryDate ? new Date(batch.expiryDate).toLocaleDateString('en-IN', { dateStyle: 'medium' }) : 'N/A'}
                              </td>
                              <td className="py-4 text-right">
                                {batch.daysRemaining <= 0 ? (
                                  <span className="px-2.5 py-1 bg-red-100 text-red-800 rounded-full font-black text-xs uppercase tracking-wide">
                                    Expired
                                  </span>
                                ) : (
                                  <span className="px-2.5 py-1 bg-amber-100 text-amber-800 rounded-full font-black text-xs uppercase tracking-wide">
                                    {batch.daysRemaining} days left
                                  </span>
                                )}
                              </td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    </div>
                  )}
                </div>
              </div>
            </div>
          )}
        </div>
      )}
    </div>
  );
};
