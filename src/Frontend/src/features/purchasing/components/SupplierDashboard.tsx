import React, { useState, useEffect } from 'react';
import { BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer, RadarChart, PolarGrid, PolarAngleAxis, PolarRadiusAxis, Radar, Legend } from 'recharts';
import { exportToCsv } from '../../../utils/exportToCsv';

export function SupplierDashboard() {
  const [suppliers, setSuppliers] = useState<any[]>([]);

  useEffect(() => {
    // Mocking supplier analytics data based on SupplierAnalyticsController
    setSuppliers([
      { SupplierName: 'Fresh Farms Ltd', DeliveryAccuracy: 98, LeadTimeCompliance: 95, FillRate: 99, QualityScore: 100, SupplierRating: 98, PurchaseValue: 450000 },
      { SupplierName: 'Dairy Co', DeliveryAccuracy: 90, LeadTimeCompliance: 85, FillRate: 92, QualityScore: 95, SupplierRating: 90.5, PurchaseValue: 280000 },
      { SupplierName: 'Daily Bakes', DeliveryAccuracy: 100, LeadTimeCompliance: 100, FillRate: 100, QualityScore: 98, SupplierRating: 99.5, PurchaseValue: 150000 },
      { SupplierName: 'Agro Suppliers', DeliveryAccuracy: 75, LeadTimeCompliance: 70, FillRate: 80, QualityScore: 85, SupplierRating: 77.5, PurchaseValue: 620000 },
    ]);
  }, []);

  const handleExport = () => {
    exportToCsv(suppliers, 'Supplier_Scorecards_Report', [
      { key: 'SupplierName', label: 'Supplier Name' },
      { key: 'DeliveryAccuracy', label: 'Delivery Accuracy (%)' },
      { key: 'FillRate', label: 'Fill Rate (%)' },
      { key: 'QualityScore', label: 'Quality Score (%)' },
      { key: 'LeadTimeCompliance', label: 'Lead Time Compliance (%)' },
      { key: 'SupplierRating', label: 'Overall Rating' },
      { key: 'PurchaseValue', label: 'Purchase Value (₹)' },
    ]);
  };

  // Format data for Radar Chart
  const radarData = suppliers.map(s => ({
    subject: s.SupplierName,
    Delivery: s.DeliveryAccuracy,
    Quality: s.QualityScore,
    FillRate: s.FillRate,
    LeadTime: s.LeadTimeCompliance
  }));

  const getRatingBadge = (rating: number) => {
    if (rating >= 95) return <span className="px-2 py-1 bg-green-900 text-green-300 text-xs font-medium rounded-full border border-green-700">Excellent (A+)</span>;
    if (rating >= 85) return <span className="px-2 py-1 bg-blue-900 text-blue-300 text-xs font-medium rounded-full border border-blue-700">Good (B)</span>;
    if (rating >= 70) return <span className="px-2 py-1 bg-yellow-900 text-yellow-300 text-xs font-medium rounded-full border border-yellow-700">Warning (C)</span>;
    return <span className="px-2 py-1 bg-red-900 text-red-300 text-xs font-medium rounded-full border border-red-700">Critical (D)</span>;
  };

  return (
    <div className="p-6 space-y-6 bg-slate-900 min-h-screen text-slate-200">
      <div className="flex justify-between items-center">
        <h1 className="text-2xl font-bold text-white">Supplier Analytics</h1>
        <div className="flex space-x-2">
          <button 
            onClick={handleExport}
            className="bg-slate-800 hover:bg-slate-700 text-slate-200 border border-slate-600 px-4 py-2 rounded-lg text-sm font-medium transition-colors cursor-pointer"
          >
            Export Report
          </button>
        </div>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
        <div className="bg-slate-800 p-4 rounded-xl border border-slate-700 shadow-sm relative overflow-hidden">
          <div className="absolute top-0 left-0 w-full h-1 bg-indigo-500"></div>
          <p className="text-sm text-slate-400 mb-1">Total Active Suppliers</p>
          <p className="text-3xl font-bold text-white">{suppliers.length}</p>
        </div>
        <div className="bg-slate-800 p-4 rounded-xl border border-slate-700 shadow-sm relative overflow-hidden">
          <div className="absolute top-0 left-0 w-full h-1 bg-green-500"></div>
          <p className="text-sm text-slate-400 mb-1">Average Delivery Accuracy</p>
          <p className="text-3xl font-bold text-green-500">90.7%</p>
        </div>
        <div className="bg-slate-800 p-4 rounded-xl border border-slate-700 shadow-sm relative overflow-hidden">
          <div className="absolute top-0 left-0 w-full h-1 bg-blue-500"></div>
          <p className="text-sm text-slate-400 mb-1">Total Purchase Value (30d)</p>
          <p className="text-3xl font-bold text-blue-500">₹{(suppliers.reduce((acc, curr) => acc + curr.PurchaseValue, 0) / 100000).toFixed(2)}L</p>
        </div>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        {/* Radar Chart for Performance Comparison */}
        <div className="bg-slate-800 p-6 rounded-xl border border-slate-700">
          <h3 className="text-lg font-semibold text-white mb-4">Supplier Performance Index</h3>
          <div className="h-80">
            <ResponsiveContainer width="100%" height="100%">
              <RadarChart cx="50%" cy="50%" outerRadius="80%" data={radarData}>
                <PolarGrid stroke="#475569" />
                <PolarAngleAxis dataKey="subject" tick={{ fill: '#94a3b8', fontSize: 12 }} />
                <PolarRadiusAxis angle={30} domain={[0, 100]} tick={{ fill: '#64748b' }} />
                <Radar name="Delivery Accuracy" dataKey="Delivery" stroke="#3b82f6" fill="#3b82f6" fillOpacity={0.3} />
                <Radar name="Quality Score" dataKey="Quality" stroke="#10b981" fill="#10b981" fillOpacity={0.3} />
                <Legend wrapperStyle={{ paddingTop: '20px' }} />
                <Tooltip contentStyle={{ backgroundColor: '#1e293b', borderColor: '#334155', color: '#f8fafc' }} />
              </RadarChart>
            </ResponsiveContainer>
          </div>
        </div>

        {/* Purchase Value Bar Chart */}
        <div className="bg-slate-800 p-6 rounded-xl border border-slate-700">
          <h3 className="text-lg font-semibold text-white mb-4">Purchase Value Distribution</h3>
          <div className="h-80">
            <ResponsiveContainer width="100%" height="100%">
              <BarChart data={suppliers} margin={{ top: 20, right: 30, left: 20, bottom: 5 }}>
                <CartesianGrid strokeDasharray="3 3" stroke="#334155" vertical={false} />
                <XAxis dataKey="SupplierName" stroke="#94a3b8" tick={{fontSize: 12}} />
                <YAxis stroke="#94a3b8" />
                <Tooltip contentStyle={{ backgroundColor: '#1e293b', borderColor: '#334155', color: '#f8fafc' }} />
                <Bar dataKey="PurchaseValue" fill="#8b5cf6" radius={[4, 4, 0, 0]} />
              </BarChart>
            </ResponsiveContainer>
          </div>
        </div>
      </div>

      <div className="bg-slate-800 rounded-xl border border-slate-700 overflow-hidden">
        <div className="px-6 py-4 border-b border-slate-700 bg-slate-800">
          <h3 className="text-lg font-semibold text-white">Supplier Scorecards</h3>
        </div>
        <div className="overflow-x-auto">
          <table className="w-full text-sm text-left text-slate-300">
            <thead className="text-xs text-slate-400 uppercase bg-slate-900/50">
              <tr>
                <th className="px-6 py-3 font-medium">Supplier</th>
                <th className="px-6 py-3 font-medium text-center">Delivery Acc.</th>
                <th className="px-6 py-3 font-medium text-center">Fill Rate</th>
                <th className="px-6 py-3 font-medium text-center">Quality</th>
                <th className="px-6 py-3 font-medium text-right">Overall Rating</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-700/50">
              {suppliers.map((supplier, idx) => (
                <tr key={idx} className="hover:bg-slate-700/30 transition-colors">
                  <td className="px-6 py-4 font-medium text-white">{supplier.SupplierName}</td>
                  <td className="px-6 py-4 text-center">{supplier.DeliveryAccuracy}%</td>
                  <td className="px-6 py-4 text-center">{supplier.FillRate}%</td>
                  <td className="px-6 py-4 text-center">{supplier.QualityScore}%</td>
                  <td className="px-6 py-4 text-right">{getRatingBadge(supplier.SupplierRating)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}
