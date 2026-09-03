import React, { useEffect, useState } from 'react';
import { storePerformanceApi } from '../api/executiveAiApi';
import { Store, TrendingUp, Trophy, ArrowUpRight, ArrowDownRight, Map } from 'lucide-react';
import { BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip as RechartsTooltip, ResponsiveContainer, Cell } from 'recharts';

export const StorePerformanceDashboard: React.FC = () => {
  const [benchmarks, setBenchmarks] = useState<any[]>([]);
  const [loading, setLoading] = useState(true);
  const [showRegionModal, setShowRegionModal] = useState(false);

  useEffect(() => {
    // Since API might not exist yet, we'll gracefully handle it and use mock data if needed
    storePerformanceApi.getBenchmarks()
      .then((data: any) => setBenchmarks(data))
      .catch(() => {
        setBenchmarks([
          { storeName: 'Store 1', region: 'North', rank: 1, revenueVariance: 12.5, percentile: 98, aiScore: 92 },
          { storeName: 'Store 2', region: 'South', rank: 2, revenueVariance: 8.2, percentile: 91, aiScore: 88 },
          { storeName: 'Store 3', region: 'East', rank: 3, revenueVariance: -2.1, percentile: 65, aiScore: 74 },
          { storeName: 'Store 4', region: 'West', rank: 4, revenueVariance: -5.4, percentile: 40, aiScore: 65 },
        ]);
      })
      .finally(() => setLoading(false));
  }, []);

  // Compute regional aggregates
  const regionalData = React.useMemo(() => {
    const map: Record<string, { count: number; totalScore: number; totalVariance: number; topStore: string; maxScore: number }> = {};
    benchmarks.forEach(b => {
      const reg = b.region || 'Other';
      if (!map[reg]) {
        map[reg] = { count: 0, totalScore: 0, totalVariance: 0, topStore: b.storeName, maxScore: b.aiScore };
      }
      map[reg].count += 1;
      map[reg].totalScore += b.aiScore || 0;
      map[reg].totalVariance += b.revenueVariance || 0;
      if ((b.aiScore || 0) > map[reg].maxScore) {
        map[reg].maxScore = b.aiScore;
        map[reg].topStore = b.storeName;
      }
    });

    return Object.entries(map).map(([region, data]) => ({
      region,
      storeCount: data.count,
      avgScore: Math.round(data.totalScore / data.count),
      avgVariance: +(data.totalVariance / data.count).toFixed(1),
      topStore: data.topStore
    }));
  }, [benchmarks]);

  return (
    <div className="p-6 bg-gray-50 min-h-screen">
      {/* Transparency Alert Banner */}
      <div className="mb-6 p-4 rounded-xl bg-amber-50 border border-amber-200 flex items-start gap-3 text-amber-900 shadow-sm">
        <div className="p-1.5 bg-amber-100 rounded-lg text-amber-700 mt-0.5">
          <Trophy className="w-5 h-5" />
        </div>
        <div className="text-xs leading-relaxed">
          <span className="font-bold text-sm block mb-0.5 text-amber-950">DEMO PREVIEW: Multi-Store Franchise Simulation</span>
          Apple Supermarket currently operates as a <strong>single flagship store (Branch 1 – Kumbakonam)</strong> with 394,867 historical transactions. Because no physical multi-branch territories exist in the customer's production database, the cross-store rankings, regional comparison (North/South/East/West), and territory AI scores shown below are <em>simulated prototype benchmarks</em> demonstrating multi-unit franchise reporting capabilities.
        </div>
      </div>

      <div className="flex justify-between items-center mb-8">
        <div>
          <h1 className="text-3xl font-bold text-gray-900 tracking-tight flex items-center gap-2">
            <Trophy className="w-8 h-8 text-yellow-500" />
            Store Benchmark Dashboard
            <span className="text-xs font-semibold px-2.5 py-1 rounded-full bg-amber-100 text-amber-800 border border-amber-300">
              Demo Simulation
            </span>
          </h1>
          <p className="text-gray-500 mt-1">Cross-store performance, rankings, and AI scoring (Prototype View)</p>
        </div>
        
        <div className="flex space-x-3">
           <button 
             onClick={() => setShowRegionModal(true)}
             className="flex items-center px-4 py-2 bg-indigo-600 text-white rounded-lg text-sm font-medium hover:bg-indigo-700 shadow-sm shadow-indigo-200 transition-all"
           >
             <Map className="w-4 h-4 mr-2" />
             Compare Regions (Demo)
           </button>
        </div>
      </div>

      {/* Regional Comparison Modal */}
      {showRegionModal && (
        <div className="fixed inset-0 bg-slate-900/60 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div className="bg-white rounded-2xl max-w-2xl w-full p-6 shadow-2xl border border-gray-100 animate-in fade-in zoom-in-95 duration-200">
            <div className="flex justify-between items-center pb-4 border-b border-gray-100 mb-6">
              <div className="flex items-center gap-3">
                <div className="p-2.5 bg-indigo-50 text-indigo-600 rounded-xl">
                  <Map className="w-6 h-6" />
                </div>
                <div>
                  <h3 className="text-xl font-bold text-gray-900 flex items-center gap-2">
                    Regional Performance Benchmarks
                    <span className="text-[10px] font-bold px-2 py-0.5 rounded-md bg-amber-100 text-amber-800 border border-amber-300">
                      Simulated Demo
                    </span>
                  </h3>
                  <p className="text-xs text-gray-500">Cross-territory prototype data (Simulated multi-branch expansion)</p>
                </div>
              </div>
              <button 
                onClick={() => setShowRegionModal(false)}
                className="text-gray-400 hover:text-gray-600 p-2 rounded-lg hover:bg-gray-100 transition-colors"
              >
                ✕
              </button>
            </div>

            <div className="grid grid-cols-1 md:grid-cols-2 gap-4 mb-6">
              {regionalData.map((reg) => (
                <div key={reg.region} className="p-4 rounded-xl border border-gray-100 bg-gray-50/50 hover:bg-white hover:shadow-md transition-all">
                  <div className="flex justify-between items-start mb-2">
                    <span className="font-bold text-gray-900 text-base">{reg.region} Region</span>
                    <span className={`text-xs font-bold px-2 py-0.5 rounded-full ${
                      reg.avgScore >= 85 ? 'bg-emerald-100 text-emerald-700' :
                      reg.avgScore >= 70 ? 'bg-amber-100 text-amber-700' : 'bg-rose-100 text-rose-700'
                    }`}>
                      Score: {reg.avgScore}/100
                    </span>
                  </div>
                  <div className="space-y-1.5 text-xs text-gray-600">
                    <div className="flex justify-between">
                      <span>Active Stores:</span>
                      <span className="font-semibold text-gray-900">{reg.storeCount}</span>
                    </div>
                    <div className="flex justify-between">
                      <span>Avg Revenue Variance:</span>
                      <span className={`font-semibold ${reg.avgVariance >= 0 ? 'text-emerald-600' : 'text-rose-600'}`}>
                        {reg.avgVariance >= 0 ? `+${reg.avgVariance}%` : `${reg.avgVariance}%`}
                      </span>
                    </div>
                    <div className="flex justify-between">
                      <span>Top Benchmark Store:</span>
                      <span className="font-semibold text-indigo-600">{reg.topStore}</span>
                    </div>
                  </div>
                </div>
              ))}
            </div>

            <div className="bg-indigo-50/60 p-4 rounded-xl border border-indigo-100/80 mb-6 text-xs text-indigo-900 leading-relaxed">
              <strong>AI Recommendation:</strong> North and South regional stores are exceeding operational benchmarks with strong basket sizes. Recommend reallocating seasonal promotional budget toward East and West regions to stabilize inventory velocity.
            </div>

            <div className="flex justify-end">
              <button
                onClick={() => setShowRegionModal(false)}
                className="px-5 py-2.5 bg-gray-900 text-white rounded-xl text-sm font-semibold hover:bg-gray-800 transition-colors shadow-sm"
              >
                Close Comparison
              </button>
            </div>
          </div>
        </div>
      )}

      {loading ? (
        <div className="flex justify-center items-center h-64 text-gray-400">Loading Benchmarks...</div>
      ) : (
        <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
          <div className="col-span-2 bg-white p-6 rounded-xl shadow-sm border border-gray-100">
            <h2 className="text-lg font-bold text-gray-900 mb-6 flex items-center">
              <Store className="w-5 h-5 mr-2 text-indigo-500" />
              Store Rankings & Variance
            </h2>
            <div className="h-[400px]">
              <ResponsiveContainer width="100%" height="100%">
                <BarChart data={benchmarks} layout="vertical" margin={{ top: 5, right: 30, left: 40, bottom: 5 }}>
                  <CartesianGrid strokeDasharray="3 3" horizontal={true} vertical={false} stroke="#E5E7EB" />
                  <XAxis type="number" hide />
                  <YAxis dataKey="storeName" type="category" axisLine={false} tickLine={false} stroke="#4B5563" fontWeight="500" />
                  <RechartsTooltip 
                    cursor={{ fill: '#F3F4F6' }}
                    contentStyle={{ borderRadius: '8px', border: 'none', boxShadow: '0 4px 6px -1px rgba(0, 0, 0, 0.1)' }}
                  />
                  <Bar dataKey="aiScore" radius={[0, 4, 4, 0]} barSize={32}>
                    {benchmarks.map((entry, index) => (
                      <Cell key={`cell-${index}`} fill={entry.aiScore > 80 ? '#10B981' : entry.aiScore > 70 ? '#F59E0B' : '#EF4444'} />
                    ))}
                  </Bar>
                </BarChart>
              </ResponsiveContainer>
            </div>
          </div>

          <div className="col-span-1 space-y-4">
            {benchmarks.map((store) => (
              <div key={store.storeName} className="bg-white rounded-xl p-5 border border-gray-100 shadow-sm hover:shadow-md transition-shadow relative overflow-hidden">
                <div className={`absolute top-0 left-0 w-1.5 h-full ${store.rank === 1 ? 'bg-yellow-400' : store.rank === 2 ? 'bg-gray-300' : store.rank === 3 ? 'bg-amber-600' : 'bg-indigo-500'}`} />
                <div className="ml-2">
                  <div className="flex justify-between items-start mb-2">
                    <div>
                      <h3 className="text-lg font-bold text-gray-900">{store.storeName}</h3>
                      <p className="text-xs text-gray-500">{store.region} Region</p>
                    </div>
                    <div className="text-right">
                      <p className="text-2xl font-black text-gray-900">#{store.rank}</p>
                    </div>
                  </div>
                  
                  <div className="grid grid-cols-2 gap-4 mt-4 pt-4 border-t border-gray-50">
                    <div>
                      <p className="text-xs text-gray-500 mb-1">Revenue Var.</p>
                      <p className={`text-sm font-bold flex items-center ${store.revenueVariance > 0 ? 'text-emerald-600' : 'text-red-600'}`}>
                        {store.revenueVariance > 0 ? <ArrowUpRight className="w-4 h-4 mr-1" /> : <ArrowDownRight className="w-4 h-4 mr-1" />}
                        {Math.abs(store.revenueVariance)}%
                      </p>
                    </div>
                    <div>
                      <p className="text-xs text-gray-500 mb-1">Percentile</p>
                      <p className="text-sm font-bold text-indigo-600">{store.percentile}th</p>
                    </div>
                  </div>
                </div>
              </div>
            ))}
          </div>
        </div>
      )}
    </div>
  );
};
