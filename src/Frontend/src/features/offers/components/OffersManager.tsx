import React, { useState, useEffect } from 'react';
import { Tag, Plus, Edit2, Play, Square, Trash2, TrendingUp, AlertCircle, Calendar } from 'lucide-react';
import { getOffers, updateOffer, deleteOffer, getOfferUsageMetrics } from '../services/offers.api';
import { OfferFormModal } from './OfferFormModal';
import { useAuthStore } from '../../auth/store/auth.store';
import { Download, Upload, Copy } from 'lucide-react';

export const OffersManager = () => {
  const [offers, setOffers] = useState<any[]>([]);
  const [metrics, setMetrics] = useState<any[]>([]);
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [editingOffer, setEditingOffer] = useState<any>(null);
  const [filterStatus, setFilterStatus] = useState('ALL'); // ALL, ACTIVE, INACTIVE
  
  const { user } = useAuthStore();
  const isManager = user?.role === 'Manager' || user?.role === 'Owner';

  useEffect(() => {
    fetchData();
  }, []);

  const fetchData = async () => {
    try {
      const [offersData, metricsData] = await Promise.all([
        getOffers(),
        getOfferUsageMetrics()
      ]);
      setOffers(offersData);
      setMetrics(metricsData);
    } catch (err) {
      console.error('Failed to load offers', err);
    }
  };

  const handleToggleStatus = async (offer: any) => {
    try {
      await updateOffer(offer.id, { ...offer, isActive: !offer.isActive });
      fetchData();
    } catch (err) {
      console.error('Failed to toggle status', err);
    }
  };

  const handleDelete = async (id: string) => {
    if (!window.confirm('Are you sure you want to delete this offer?')) return;
    try {
      await deleteOffer(id);
      fetchData();
    } catch (err) {
      console.error('Failed to delete', err);
    }
  };

  const handleDuplicate = async (offer: any) => {
    const duplicate = {
      ...offer,
      id: undefined,
      name: `${offer.name} (Copy)`,
      isActive: false
    };
    setEditingOffer(duplicate);
    setIsModalOpen(true);
  };

  const handleExport = () => {
    window.location.href = '/api/offers/export?format=json';
  };

  const handleImport = () => {
    const input = document.createElement('input');
    input.type = 'file';
    input.accept = 'application/json';
    input.onchange = async (e: any) => {
      const file = e.target.files[0];
      if (!file) return;
      const reader = new FileReader();
      reader.onload = async (event: any) => {
        try {
          const payload = JSON.parse(event.target.result);
          // Assuming an import API endpoint exists (implemented in backend)
          await fetch('/api/offers/import', {
            method: 'POST',
            headers: {
              'Content-Type': 'application/json',
              'Authorization': `Bearer ${localStorage.getItem('token')}`
            },
            body: JSON.stringify(payload)
          });
          alert('Import successful!');
          fetchData();
        } catch (err) {
          alert('Failed to import: Invalid JSON or server error.');
        }
      };
      reader.readAsText(file);
    };
    input.click();
  };

  if (!isManager) {
    return (
      <div className="flex-1 bg-slate-50 flex items-center justify-center p-8">
        <div className="text-center">
          <AlertCircle className="w-16 h-16 text-red-500 mx-auto mb-4" />
          <h2 className="text-2xl font-bold text-slate-800">Access Denied</h2>
          <p className="text-slate-500 mt-2">Only Managers and Owners can access the Offers module.</p>
        </div>
      </div>
    );
  }

  const now = new Date();
  const activeOffersCount = offers.filter(o => o.isActive && new Date(o.startDate) <= now && new Date(o.endDate) >= now).length;
  const scheduledCount = offers.filter(o => o.isActive && new Date(o.startDate) > now).length;
  const expiringThisWeek = offers.filter(o => o.isActive && new Date(o.endDate) > now && new Date(o.endDate) < new Date(now.getTime() + 7 * 24 * 60 * 60 * 1000)).length;
  const inactiveCount = offers.filter(o => !o.isActive).length;

  const filteredOffers = offers.filter(o => {
    if (filterStatus === 'ACTIVE') return o.isActive;
    if (filterStatus === 'INACTIVE') return !o.isActive;
    return true;
  });

  return (
    <div className="flex-1 bg-slate-50 overflow-auto p-8">
      <div className="max-w-7xl mx-auto">
        <div className="flex justify-between items-center mb-8">
          <div>
            <h1 className="text-3xl font-black text-slate-800 tracking-tight flex items-center gap-3">
              <Tag className="w-8 h-8 text-indigo-600" />
              Offers & Promotions
            </h1>
            <p className="text-slate-500 mt-1">Manage discounts, combos, and time-based promotions</p>
          </div>
          <div className="flex items-center gap-3">
            <button 
              onClick={handleImport}
              className="bg-white text-slate-700 border border-slate-300 px-4 py-2 rounded-lg font-bold hover:bg-slate-50 transition-colors flex items-center gap-2 shadow-sm"
            >
              <Upload className="w-4 h-4" /> Import
            </button>
            <button 
              onClick={handleExport}
              className="bg-white text-slate-700 border border-slate-300 px-4 py-2 rounded-lg font-bold hover:bg-slate-50 transition-colors flex items-center gap-2 shadow-sm"
            >
              <Download className="w-4 h-4" /> Export
            </button>
            <button 
              onClick={() => { setEditingOffer(null); setIsModalOpen(true); }}
              className="bg-indigo-600 text-white px-6 py-3 rounded-xl font-bold hover:bg-indigo-700 transition-colors flex items-center gap-2 shadow-lg"
            >
              <Plus className="w-5 h-5" />
              Create Offer
            </button>
          </div>
        </div>

        {/* Dashboard Stats */}
        <div className="grid grid-cols-1 md:grid-cols-4 gap-6 mb-8">
          <div className="bg-white p-6 rounded-2xl shadow-sm border border-slate-200">
            <h3 className="text-sm font-bold text-slate-400 uppercase tracking-wider mb-2">Total Active Offers</h3>
            <div className="text-4xl font-black text-emerald-600">{activeOffersCount}</div>
          </div>
          <div className="bg-white p-6 rounded-2xl shadow-sm border border-slate-200">
            <h3 className="text-sm font-bold text-slate-400 uppercase tracking-wider mb-2">Scheduled</h3>
            <div className="text-4xl font-black text-blue-600">{scheduledCount}</div>
          </div>
          <div className="bg-white p-6 rounded-2xl shadow-sm border border-slate-200">
            <h3 className="text-sm font-bold text-slate-400 uppercase tracking-wider mb-2">Expiring This Week</h3>
            <div className="text-4xl font-black text-orange-600">{expiringThisWeek}</div>
          </div>
          <div className="bg-white p-6 rounded-2xl shadow-sm border border-slate-200">
            <h3 className="text-sm font-bold text-slate-400 uppercase tracking-wider mb-2">Inactive Offers</h3>
            <div className="text-4xl font-black text-slate-400">{inactiveCount}</div>
          </div>
        </div>

        {/* Filters */}
        <div className="flex gap-4 mb-6">
          <button onClick={() => setFilterStatus('ALL')} className={`px-4 py-2 rounded-lg font-bold ${filterStatus === 'ALL' ? 'bg-indigo-100 text-indigo-700' : 'bg-white text-slate-600 border border-slate-200'}`}>All</button>
          <button onClick={() => setFilterStatus('ACTIVE')} className={`px-4 py-2 rounded-lg font-bold ${filterStatus === 'ACTIVE' ? 'bg-emerald-100 text-emerald-700' : 'bg-white text-slate-600 border border-slate-200'}`}>Active</button>
          <button onClick={() => setFilterStatus('INACTIVE')} className={`px-4 py-2 rounded-lg font-bold ${filterStatus === 'INACTIVE' ? 'bg-slate-200 text-slate-700' : 'bg-white text-slate-600 border border-slate-200'}`}>Inactive</button>
        </div>

        {/* Data Grid */}
        <div className="bg-white rounded-2xl shadow-sm border border-slate-200 overflow-hidden">
          <table className="w-full text-left text-sm text-slate-600">
            <thead className="bg-slate-50 border-b border-slate-200 text-slate-500 font-bold uppercase tracking-wider">
              <tr>
                <th className="p-4">Offer Name</th>
                <th className="p-4">Type</th>
                <th className="p-4">Priority</th>
                <th className="p-4">Start Date</th>
                <th className="p-4">End Date</th>
                <th className="p-4 text-right">Usage</th>
                <th className="p-4">Status</th>
                <th className="p-4 text-center">Actions</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {filteredOffers.map((offer) => {
                const isCurrentlyActive = offer.isActive && new Date(offer.startDate) <= now && new Date(offer.endDate) >= now;
                const metric = metrics.find(m => m.offerId === offer.id);
                
                return (
                  <tr key={offer.id} className="hover:bg-slate-50">
                    <td className="p-4 font-bold text-slate-800">
                      {offer.name}
                      {offer.promoCode && <span className="ml-2 px-2 py-0.5 bg-slate-100 text-slate-600 rounded text-xs font-mono">{offer.promoCode}</span>}
                    </td>
                    <td className="p-4">
                      <span className="px-2 py-1 bg-indigo-50 text-indigo-700 rounded-md font-medium text-xs">{offer.offerType}</span>
                    </td>
                    <td className="p-4 font-mono font-medium">{offer.priority}</td>
                    <td className="p-4 text-slate-500">{new Date(offer.startDate).toLocaleDateString()}</td>
                    <td className="p-4 text-slate-500">{new Date(offer.endDate).toLocaleDateString()}</td>
                    <td className="p-4 text-right font-medium text-slate-800">
                      {metric?.timesApplied || 0}
                    </td>
                    <td className="p-4">
                      {offer.isActive ? (
                        isCurrentlyActive 
                          ? <span className="px-3 py-1 bg-emerald-100 text-emerald-800 rounded-full font-bold text-xs flex items-center gap-1 w-max"><span className="w-2 h-2 rounded-full bg-emerald-500"></span> Active</span>
                          : <span className="px-3 py-1 bg-blue-100 text-blue-800 rounded-full font-bold text-xs flex items-center gap-1 w-max"><Calendar className="w-3 h-3" /> Scheduled</span>
                      ) : (
                         new Date(offer.endDate) < now
                           ? <span className="px-3 py-1 bg-rose-100 text-rose-800 rounded-full font-bold text-xs flex items-center gap-1 w-max"><AlertCircle className="w-3 h-3" /> Expired</span>
                           : <span className="px-3 py-1 bg-slate-200 text-slate-600 rounded-full font-bold text-xs flex items-center gap-1 w-max"><Square className="w-3 h-3" /> Inactive</span>
                      )}
                    </td>
                    <td className="p-4">
                      <div className="flex justify-center gap-2">
                        <button onClick={() => handleToggleStatus(offer)} className={`p-2 rounded-lg transition-colors ${offer.isActive ? 'hover:bg-rose-100 text-rose-600' : 'hover:bg-emerald-100 text-emerald-600'}`} title={offer.isActive ? 'Deactivate' : 'Activate'}>
                          {offer.isActive ? <Square className="w-4 h-4" /> : <Play className="w-4 h-4" />}
                        </button>
                        <button onClick={() => handleDuplicate(offer)} className="p-2 hover:bg-indigo-100 rounded-lg text-indigo-600 transition-colors" title="Duplicate">
                          <Copy className="w-4 h-4" />
                        </button>
                        <button onClick={() => { setEditingOffer(offer); setIsModalOpen(true); }} className="p-2 hover:bg-slate-100 rounded-lg text-slate-600 transition-colors" title="Edit">
                          <Edit2 className="w-4 h-4" />
                        </button>
                        <button onClick={() => handleDelete(offer.id)} className="p-2 hover:bg-rose-100 rounded-lg text-rose-600 transition-colors" title="Delete">
                          <Trash2 className="w-4 h-4" />
                        </button>
                      </div>
                    </td>
                  </tr>
                );
              })}
              {filteredOffers.length === 0 && (
                <tr>
                  <td colSpan={8} className="p-8 text-center text-slate-500 font-medium">No offers found.</td>
                </tr>
              )}
            </tbody>
          </table>
        </div>

        {/* Analytics Section (Simplified for now, will expand later) */}
        {metrics.length > 0 && (
          <div className="mt-12">
            <h2 className="text-2xl font-bold text-slate-800 mb-6 flex items-center gap-2">
              <TrendingUp className="w-6 h-6 text-indigo-600" />
              Offer Usage Analytics (Top Performers)
            </h2>
            <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
              {metrics.sort((a,b) => b.timesApplied - a.timesApplied).slice(0,3).map(m => (
                <div key={m.offerId} className="bg-white p-6 rounded-2xl shadow-sm border border-slate-200">
                  <h3 className="font-bold text-slate-800 mb-4 truncate" title={m.offerName}>{m.offerName}</h3>
                  <div className="space-y-3">
                    <div className="flex justify-between text-sm">
                      <span className="text-slate-500">Times Applied</span>
                      <span className="font-bold text-slate-800">{m.timesApplied}</span>
                    </div>
                    <div className="flex justify-between text-sm">
                      <span className="text-slate-500">Discount Given</span>
                      <span className="font-bold text-rose-600">₹{m.totalDiscountGiven.toFixed(2)}</span>
                    </div>
                    <div className="flex justify-between text-sm">
                      <span className="text-slate-500">Revenue Influenced</span>
                      <span className="font-bold text-emerald-600">₹{m.revenueInfluenced.toFixed(2)}</span>
                    </div>
                  </div>
                </div>
              ))}
            </div>
          </div>
        )}

      </div>

      {isModalOpen && (
        <OfferFormModal 
          offer={editingOffer} 
          onClose={() => setIsModalOpen(false)} 
          onSave={() => { setIsModalOpen(false); fetchData(); }} 
        />
      )}
    </div>
  );
};
