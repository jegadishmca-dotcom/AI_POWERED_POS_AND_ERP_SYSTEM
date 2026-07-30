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
  FileText,
  Eye,
  CreditCard,
  Wallet,
  Receipt
} from 'lucide-react';
import { 
  getGstReport, 
  getMarginReport, 
  getInventoryInsights, 
  getInvoiceSalesReport,
  GstReportRow, 
  MarginReport, 
  InventoryInsights,
  InvoiceSalesRow 
} from '../api/reports.api';
import { getInvoiceByNumber } from '../../pos/api/pos.api';
import { printReceipt } from '../../pos/utils/printReceipt';

export const ReportsHub: React.FC = () => {
  const [activeTab, setActiveTab] = useState<'margins' | 'gst' | 'invoices' | 'inventory'>('invoices');
  
  // Set default date range to today's date so cashier/owner sees selected business date sales instantly
  const todayStr = new Date().toISOString().split('T')[0];
  const [fromDate, setFromDate] = useState<string>(todayStr);
  const [toDate, setToDate] = useState<string>(todayStr);
  
  const [marginData, setMarginData] = useState<MarginReport | null>(null);
  const [gstData, setGstData] = useState<GstReportRow[]>([]);
  const [inventoryData, setInventoryData] = useState<InventoryInsights | null>(null);
  const [invoiceSalesData, setInvoiceSalesData] = useState<InvoiceSalesRow[]>([]);

  // Filters for Invoice-Wise Sales
  const [invoiceSearchQuery, setInvoiceSearchQuery] = useState('');
  const [paymentModeFilter, setPaymentModeFilter] = useState<string>('ALL');
  
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
      } else if (activeTab === 'invoices') {
        const data = await getInvoiceSalesReport(fromDate, toDate);
        setInvoiceSalesData(data);
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

  const handlePrintInvoice = async (invoiceNumber: string) => {
    try {
      const fullInvoice = await getInvoiceByNumber(invoiceNumber);
      if (fullInvoice) {
        printReceipt(fullInvoice);
      } else {
        alert('Invoice details not found');
      }
    } catch (err) {
      console.error('Error opening receipt:', err);
      alert('Could not load invoice receipt details.');
    }
  };

  // Filtered Invoices calculation
  const filteredInvoices = invoiceSalesData.filter((inv) => {
    const query = invoiceSearchQuery.toLowerCase().trim();
    const matchesQuery = !query || (
      inv.invoiceNumber.toLowerCase().includes(query) ||
      inv.cashierName.toLowerCase().includes(query) ||
      (inv.customerName && inv.customerName.toLowerCase().includes(query)) ||
      (inv.customerPhone && inv.customerPhone.includes(query))
    );

    const matchesPayment = paymentModeFilter === 'ALL' || (
      inv.paymentMode && inv.paymentMode.toUpperCase().includes(paymentModeFilter.toUpperCase())
    );

    return matchesQuery && matchesPayment;
  });

  // Invoice Summary Totals
  const totalInvoicesCount = filteredInvoices.length;
  const totalNetSales = filteredInvoices.reduce((sum, item) => sum + (item.netPayable || 0), 0);
  const totalDiscounts = filteredInvoices.reduce((sum, item) => sum + (item.discountAmount || 0), 0);
  const totalTaxes = filteredInvoices.reduce((sum, item) => sum + (item.taxAmount || 0), 0);

  const handleExportCSV = (type: string) => {
    let headers: string[] = [];
    let rows: string[][] = [];
    let filename = '';

    if (type === 'invoices') {
      filename = `Invoice_Wise_Sales_Report_${fromDate}_to_${toDate}.csv`;
      headers = [
        'S.No',
        'Invoice Number',
        'Date & Time',
        'Cashier',
        'Customer Name',
        'Customer Phone',
        'Items Count',
        'Total Qty Sold',
        'Subtotal (₹)',
        'Discount (₹)',
        'Tax (GST ₹)',
        'Net Value (₹)',
        'Payment Mode',
        'Status'
      ];
      rows = filteredInvoices.map((inv, idx) => [
        (idx + 1).toString(),
        inv.invoiceNumber,
        inv.createdAt ? new Date(inv.createdAt).toLocaleString('en-IN') : 'N/A',
        inv.cashierName || 'Cashier',
        inv.customerName || 'WALK-IN',
        inv.customerPhone || '',
        inv.itemCount.toString(),
        inv.totalQty.toString(),
        inv.subTotal.toFixed(2),
        inv.discountAmount.toFixed(2),
        inv.taxAmount.toFixed(2),
        inv.netPayable.toFixed(2),
        inv.paymentMode || 'Cash',
        inv.status || 'Completed'
      ]);
    } else if (type === 'gst') {
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
          <p className="text-gray-500 font-medium">Invoice sales breakdown, gross margins, tax filings, and stock metrics</p>
        </div>

        {/* Date Filters */}
        {activeTab !== 'inventory' && (
          <div className="flex items-center gap-4 bg-white p-3 rounded-xl shadow-sm border border-slate-200">
            <div className="flex items-center text-gray-500">
              <Calendar className="w-4 h-4 mr-2 text-indigo-600" />
              <span className="text-xs font-black uppercase text-slate-700">Business Date</span>
            </div>
            <div className="flex items-center gap-2">
              <input 
                type="date" 
                value={fromDate}
                onChange={(e) => setFromDate(e.target.value)}
                className="px-3 py-1.5 border border-slate-300 rounded-lg text-sm font-bold text-slate-800 bg-slate-50 focus:outline-none focus:ring-2 focus:ring-indigo-500"
              />
              <span className="text-gray-400 font-bold">to</span>
              <input 
                type="date" 
                value={toDate}
                onChange={(e) => setToDate(e.target.value)}
                className="px-3 py-1.5 border border-slate-300 rounded-lg text-sm font-bold text-slate-800 bg-slate-50 focus:outline-none focus:ring-2 focus:ring-indigo-500"
              />
            </div>
          </div>
        )}
      </div>

      {/* Tabs Menu */}
      <div className="flex border-b border-slate-200 mb-8 gap-6 overflow-x-auto">
        <button 
          onClick={() => setActiveTab('invoices')}
          className={`pb-4 text-sm font-bold border-b-2 transition-all flex items-center gap-2 ${
            activeTab === 'invoices' 
              ? 'border-indigo-600 text-indigo-600 font-black' 
              : 'border-transparent text-slate-500 hover:text-slate-700'
          }`}
        >
          <Receipt className="w-4 h-4" /> Invoice-Wise Sales
        </button>
        <button 
          onClick={() => setActiveTab('margins')}
          className={`pb-4 text-sm font-bold border-b-2 transition-all flex items-center gap-2 ${
            activeTab === 'margins' 
              ? 'border-indigo-600 text-indigo-600 font-black' 
              : 'border-transparent text-slate-500 hover:text-slate-700'
          }`}
        >
          <TrendingUp className="w-4 h-4" /> Sales & Profit Margins
        </button>
        <button 
          onClick={() => setActiveTab('gst')}
          className={`pb-4 text-sm font-bold border-b-2 transition-all flex items-center gap-2 ${
            activeTab === 'gst' 
              ? 'border-indigo-600 text-indigo-600 font-black' 
              : 'border-transparent text-slate-500 hover:text-slate-700'
          }`}
        >
          <FileText className="w-4 h-4" /> GST Tax Filings (Sales)
        </button>
        <button 
          onClick={() => setActiveTab('inventory')}
          className={`pb-4 text-sm font-bold border-b-2 transition-all flex items-center gap-2 ${
            activeTab === 'inventory' 
              ? 'border-indigo-600 text-indigo-600 font-black' 
              : 'border-transparent text-slate-500 hover:text-slate-700'
          }`}
        >
          <AlertTriangle className="w-4 h-4" /> Inventory Health & Alerts
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
          {/* TAB: INVOICE-WISE SALES REPORT */}
          {activeTab === 'invoices' && (
            <div className="space-y-6">
              {/* Summary KPIs */}
              <div className="grid grid-cols-1 md:grid-cols-4 gap-6">
                <div className="bg-white p-6 rounded-xl shadow-sm border border-slate-200">
                  <p className="text-xs font-bold text-gray-500 uppercase tracking-wider mb-1">Total Invoices</p>
                  <h2 className="text-3xl font-black text-slate-800">{totalInvoicesCount}</h2>
                  <p className="text-xs text-slate-400 mt-1 font-semibold">Selected business date range</p>
                </div>
                <div className="bg-white p-6 rounded-xl shadow-sm border border-slate-200">
                  <p className="text-xs font-bold text-gray-500 uppercase tracking-wider mb-1">Total Net Revenue</p>
                  <h2 className="text-3xl font-black text-emerald-600">₹{totalNetSales.toLocaleString('en-IN', { minimumFractionDigits: 2 })}</h2>
                  <p className="text-xs text-emerald-600/80 mt-1 font-bold">Sum of invoice net payable</p>
                </div>
                <div className="bg-white p-6 rounded-xl shadow-sm border border-slate-200">
                  <p className="text-xs font-bold text-gray-500 uppercase tracking-wider mb-1">Total Discounts</p>
                  <h2 className="text-3xl font-black text-amber-600">₹{totalDiscounts.toLocaleString('en-IN', { minimumFractionDigits: 2 })}</h2>
                  <p className="text-xs text-amber-600/80 mt-1 font-bold">Promotions & offers given</p>
                </div>
                <div className="bg-white p-6 rounded-xl shadow-sm border border-slate-200">
                  <p className="text-xs font-bold text-gray-500 uppercase tracking-wider mb-1">Total Tax (GST)</p>
                  <h2 className="text-3xl font-black text-indigo-600">₹{totalTaxes.toLocaleString('en-IN', { minimumFractionDigits: 2 })}</h2>
                  <p className="text-xs text-indigo-600/80 mt-1 font-bold">CGST + SGST + Cess</p>
                </div>
              </div>

              {/* Table Controls Bar */}
              <div className="bg-white p-4 rounded-xl shadow-sm border border-slate-200 flex flex-wrap gap-4 justify-between items-center">
                <div className="flex flex-wrap gap-3 items-center flex-1">
                  <div className="relative min-w-[280px]">
                    <Search className="w-4 h-4 absolute left-3 top-3 text-slate-400" />
                    <input 
                      type="text"
                      placeholder="Search Invoice #, Cashier, Customer..."
                      value={invoiceSearchQuery}
                      onChange={(e) => setInvoiceSearchQuery(e.target.value)}
                      className="w-full pl-9 pr-4 py-2 border border-slate-300 rounded-lg text-sm font-semibold text-slate-800 bg-slate-50 focus:outline-none focus:ring-2 focus:ring-indigo-500"
                    />
                  </div>

                  {/* Payment Mode Pills */}
                  <div className="flex items-center gap-1.5 bg-slate-100 p-1 rounded-lg">
                    {['ALL', 'CASH', 'UPI', 'CARD'].map((mode) => (
                      <button
                        key={mode}
                        onClick={() => setPaymentModeFilter(mode)}
                        className={`px-3 py-1.5 rounded-md text-xs font-black transition-all ${
                          paymentModeFilter === mode
                            ? 'bg-indigo-600 text-white shadow-xs'
                            : 'text-slate-600 hover:text-slate-900 hover:bg-slate-200/60'
                        }`}
                      >
                        {mode}
                      </button>
                    ))}
                  </div>
                </div>

                <button 
                  onClick={() => handleExportCSV('invoices')}
                  className="bg-slate-800 hover:bg-slate-900 text-white px-4 py-2 rounded-lg font-bold text-xs flex items-center gap-2 transition shadow-sm"
                >
                  <Download className="w-4 h-4" /> Export CSV
                </button>
              </div>

              {/* Invoice List Table */}
              <div className="bg-white rounded-xl shadow-sm border border-slate-200 overflow-hidden">
                {filteredInvoices.length === 0 ? (
                  <div className="p-12 text-center text-slate-400 font-semibold">
                    No invoices found for the selected business date and filter criteria.
                  </div>
                ) : (
                  <div className="overflow-x-auto">
                    <table className="w-full text-left text-sm border-collapse">
                      <thead>
                        <tr className="bg-slate-100 border-b border-slate-200 text-slate-700 font-black text-xs uppercase tracking-wider">
                          <th className="p-3.5 text-center w-12">S.No</th>
                          <th className="p-3.5">Invoice #</th>
                          <th className="p-3.5">Date & Time</th>
                          <th className="p-3.5">Cashier</th>
                          <th className="p-3.5">Customer</th>
                          <th className="p-3.5 text-center">Items / Units</th>
                          <th className="p-3.5 text-right">Subtotal (₹)</th>
                          <th className="p-3.5 text-right">Discount (₹)</th>
                          <th className="p-3.5 text-right">GST Tax (₹)</th>
                          <th className="p-3.5 text-right">Net Value (₹)</th>
                          <th className="p-3.5 text-center">Payment</th>
                          <th className="p-3.5 text-center">Action</th>
                        </tr>
                      </thead>
                      <tbody className="divide-y divide-slate-200 text-slate-800">
                        {filteredInvoices.map((inv, idx) => {
                          const isEven = idx % 2 === 0;
                          return (
                            <tr key={inv.id || idx} className={`transition-colors hover:bg-indigo-50/50 ${isEven ? 'bg-white' : 'bg-slate-50/60'}`}>
                              <td className="p-3.5 text-center font-bold text-slate-500 text-xs">{idx + 1}</td>
                              <td className="p-3.5 font-black text-slate-900">{inv.invoiceNumber}</td>
                              <td className="p-3.5 text-xs font-bold text-slate-600 whitespace-nowrap">
                                {inv.createdAt ? new Date(inv.createdAt).toLocaleString('en-IN', { dateStyle: 'short', timeStyle: 'short' }) : 'N/A'}
                              </td>
                              <td className="p-3.5 font-bold text-slate-700 text-xs">{inv.cashierName || 'Cashier'}</td>
                              <td className="p-3.5">
                                <p className="font-bold text-slate-800 text-xs">{inv.customerName || 'WALK-IN'}</p>
                                {inv.customerPhone && <p className="text-[11px] text-slate-400 font-semibold">{inv.customerPhone}</p>}
                              </td>
                              <td className="p-3.5 text-center font-bold text-xs text-slate-700">
                                {inv.itemCount} items ({inv.totalQty} units)
                              </td>
                              <td className="p-3.5 text-right font-semibold text-slate-600">₹{inv.subTotal.toFixed(2)}</td>
                              <td className="p-3.5 text-right font-bold text-amber-600">{inv.discountAmount > 0 ? `-₹${inv.discountAmount.toFixed(2)}` : '₹0.00'}</td>
                              <td className="p-3.5 text-right font-semibold text-slate-600">₹{inv.taxAmount.toFixed(2)}</td>
                              <td className="p-3.5 text-right font-black text-emerald-700 text-base">₹{inv.netPayable.toFixed(2)}</td>
                              <td className="p-3.5 text-center">
                                <span className={`inline-block px-2.5 py-1 rounded-md text-[11px] font-black uppercase tracking-wider ${
                                  inv.paymentMode?.toUpperCase().includes('CASH') ? 'bg-emerald-100 text-emerald-800 border border-emerald-300' :
                                  inv.paymentMode?.toUpperCase().includes('UPI') ? 'bg-blue-100 text-blue-800 border border-blue-300' :
                                  inv.paymentMode?.toUpperCase().includes('CARD') ? 'bg-purple-100 text-purple-800 border border-purple-300' :
                                  'bg-slate-200 text-slate-800 border border-slate-300'
                                }`}>
                                  {inv.paymentMode || 'CASH'}
                                </span>
                              </td>
                              <td className="p-3.5 text-center">
                                <button 
                                  onClick={() => handlePrintInvoice(inv.invoiceNumber)}
                                  className="text-xs bg-indigo-50 hover:bg-indigo-100 text-indigo-700 border border-indigo-200 px-2.5 py-1.5 rounded-lg font-bold transition flex items-center justify-center gap-1 mx-auto"
                                  title="View & Print Invoice Receipt"
                                >
                                  <Eye className="w-3.5 h-3.5" /> Receipt
                                </button>
                              </td>
                            </tr>
                          );
                        })}
                      </tbody>
                    </table>
                  </div>
                )}
              </div>
            </div>
          )}

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
                  <h2 className="text-3xl font-black text-indigo-600">₹{(marginData.summary?.totalProfit || 0).toLocaleString()}</h2>
                  <p className="text-xs text-indigo-500 mt-2 font-medium">Revenue minus product cost</p>
                </div>
                <div className="bg-white p-6 rounded-xl shadow-sm border border-slate-100">
                  <p className="text-sm font-bold text-gray-500 mb-1">Gross Profit Margin</p>
                  <h2 className="text-3xl font-black text-emerald-600">{(marginData.summary?.marginPercentage || 0).toFixed(2)}%</h2>
                  <p className="text-xs text-slate-400 mt-2">Average markup percentage</p>
                </div>
              </div>

              {/* Category Profitability Breakdown */}
              <div className="grid grid-cols-1 md:grid-cols-2 gap-8">
                <div className="bg-white p-6 rounded-xl shadow-sm border border-slate-100">
                  <div className="flex justify-between items-center mb-6">
                    <h3 className="text-lg font-bold text-slate-800">Category Profitability</h3>
                    <button onClick={() => handleExportCSV('category-margins')} className="text-xs text-indigo-600 font-bold hover:underline flex items-center">
                      <Download className="w-3.5 h-3.5 mr-1" /> Export CSV
                    </button>
                  </div>
                  {marginData.categoryMargins.length === 0 ? (
                    <div className="h-60 flex items-center justify-center text-gray-400 font-medium">
                      No sales data available for this range.
                    </div>
                  ) : (
                    <div className="overflow-x-auto">
                      <table className="w-full text-left text-sm">
                        <thead>
                          <tr className="border-b text-slate-400 uppercase text-xs font-bold">
                            <th className="pb-3">Category</th>
                            <th className="pb-3 text-center">Qty</th>
                            <th className="pb-3 text-right">Revenue</th>
                            <th className="pb-3 text-right">Margin %</th>
                          </tr>
                        </thead>
                        <tbody className="divide-y text-slate-700">
                          {marginData.categoryMargins.map((cat, idx) => (
                            <tr key={idx} className="hover:bg-slate-50 transition-colors">
                              <td className="py-4 font-bold text-slate-800">{cat.categoryName}</td>
                              <td className="py-4 text-center font-medium">{cat.quantitySold}</td>
                              <td className="py-4 text-right font-bold">₹{cat.revenue.toLocaleString()}</td>
                              <td className="py-4 text-right">
                                <span className={`font-extrabold ${cat.marginPercentage >= 15 ? 'text-emerald-600' : 'text-amber-600'}`}>
                                  {cat.marginPercentage.toFixed(1)}%
                                </span>
                              </td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    </div>
                  )}
                </div>

                {/* Top Profitable Products */}
                <div className="bg-white p-6 rounded-xl shadow-sm border border-slate-100">
                  <div className="flex justify-between items-center mb-6">
                    <h3 className="text-lg font-bold text-slate-800">Top Profitable Products</h3>
                    <button onClick={() => handleExportCSV('product-margins')} className="text-xs text-indigo-600 font-bold hover:underline flex items-center">
                      <Download className="w-3.5 h-3.5 mr-1" /> Export CSV
                    </button>
                  </div>
                  {marginData.productMargins.length === 0 ? (
                    <div className="h-60 flex items-center justify-center text-gray-400 font-medium">
                      No sales data available for this range.
                    </div>
                  ) : (
                    <div className="space-y-4">
                      {marginData.productMargins.slice(0, 5).map((item, idx) => (
                        <div key={idx} className="flex justify-between items-center border-b pb-3">
                          <div>
                            <p className="font-bold text-slate-800">{item.productName}</p>
                            <p className="text-xs text-slate-400">{item.quantitySold} Units Sold • Sales ₹{item.revenue.toLocaleString()}</p>
                          </div>
                          <div className="text-right">
                            <p className="font-extrabold text-indigo-600">₹{item.profit.toLocaleString()}</p>
                            <p className="text-xs font-bold text-emerald-600">{item.marginPercentage.toFixed(1)}% margin</p>
                          </div>
                        </div>
                      ))}
                    </div>
                  )}
                </div>
              </div>
            </div>
          )}

          {/* TAB 2: GST TAX FILINGS */}
          {activeTab === 'gst' && (
            <div className="bg-white p-6 rounded-xl shadow-sm border border-slate-100">
              <div className="flex justify-between items-center mb-6">
                <div>
                  <h3 className="text-lg font-bold text-slate-800">GST Sales Tax Summary</h3>
                  <p className="text-xs text-gray-400">CGST + SGST tax collected grouped by GST Slab %</p>
                </div>
                <button onClick={() => handleExportCSV('gst')} className="bg-indigo-50 text-indigo-600 hover:bg-indigo-100 px-4 py-2 rounded-lg font-bold text-sm transition flex items-center">
                  <Download className="w-4 h-4 mr-2" /> Export CSV
                </button>
              </div>

              {gstData.length === 0 ? (
                <div className="h-60 flex items-center justify-center text-gray-400 font-medium">
                  No tax data recorded for the selected period.
                </div>
              ) : (
                <div className="overflow-x-auto">
                  <table className="w-full text-left text-sm">
                    <thead>
                      <tr className="border-b text-slate-400 uppercase text-xs font-bold">
                        <th className="pb-3">GST Slab</th>
                        <th className="pb-3 text-right">Taxable Amount</th>
                        <th className="pb-3 text-right">CGST</th>
                        <th className="pb-3 text-right">SGST</th>
                        <th className="pb-3 text-right">Cess</th>
                        <th className="pb-3 text-right">Total Tax Collected</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y text-slate-700">
                      {gstData.map((row, idx) => (
                        <tr key={idx} className="hover:bg-slate-50 transition-colors">
                          <td className="py-4 font-extrabold text-slate-800">{row.taxRate}% GST</td>
                          <td className="py-4 text-right font-medium">₹{row.taxableAmount.toLocaleString()}</td>
                          <td className="py-4 text-right font-medium text-slate-600">₹{row.cgstCollected.toLocaleString()}</td>
                          <td className="py-4 text-right font-medium text-slate-600">₹{row.sgstCollected.toLocaleString()}</td>
                          <td className="py-4 text-right font-medium text-slate-600">₹{row.cessCollected.toLocaleString()}</td>
                          <td className="py-4 text-right font-bold text-indigo-600">₹{row.totalTax.toLocaleString()}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </div>
          )}

          {/* TAB 3: INVENTORY HEALTH & ALERTS */}
          {activeTab === 'inventory' && inventoryData && (
            <div className="space-y-8">
              {/* Overview Metrics */}
              <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
                <div className="bg-white p-6 rounded-xl shadow-sm border border-slate-100">
                  <p className="text-sm font-bold text-gray-500 mb-1">Total Stock Valuation</p>
                  <h2 className="text-3xl font-black text-slate-800">₹{inventoryData.totalValuation.toLocaleString()}</h2>
                  <p className="text-xs text-slate-400 mt-2">Based on active batch purchase costs</p>
                </div>
                <div className="bg-white p-6 rounded-xl shadow-sm border border-slate-100">
                  <p className="text-sm font-bold text-gray-500 mb-1">Low Stock Alerts</p>
                  <h2 className="text-3xl font-black text-rose-500">{inventoryData.lowStockCount}</h2>
                  <p className="text-xs text-rose-500 mt-2 font-medium">Products below reorder threshold</p>
                </div>
                <div className="bg-white p-6 rounded-xl shadow-sm border border-slate-100">
                  <p className="text-sm font-bold text-gray-500 mb-1">Near Expiry Batches</p>
                  <h2 className="text-3xl font-black text-amber-500">{inventoryData.nearExpiryCount}</h2>
                  <p className="text-xs text-amber-500 mt-2 font-medium">Batches expiring within 30 days</p>
                </div>
              </div>

              <div className="grid grid-cols-1 md:grid-cols-2 gap-8">
                {/* Low Stock Items List */}
                <div className="bg-white p-6 rounded-xl shadow-sm border border-slate-100">
                  <div className="flex justify-between items-center mb-6">
                    <h3 className="text-lg font-bold text-slate-800">Low Stock Reorder Alerts</h3>
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
