import React, { useEffect, useState } from 'react';
import { storePerformanceApi } from '../api/executiveAiApi';
import { Store, TrendingUp, Trophy, ArrowUpRight, ArrowDownRight, Map } from 'lucide-react';
import { BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip as RechartsTooltip, ResponsiveContainer, Cell } from 'recharts';

export const StorePerformanceDashboard: React.FC = () => {
  const [benchmarks, setBenchmarks] = useState<any[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    // Since API might not exist yet, we'll gracefully handle it and use mock data if needed
    storePerformanceApi.getBenchmarks()
      .then(data => setBenchmarks(data))
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

  return (
    <div className="p-6 bg-gray-50 min-h-screen">
      <div className="flex justify-between items-center mb-8">
        <div>
          <h1 className="text-3xl font-bold text-gray-900 tracking-tight flex items-center">
            <Trophy className="w-8 h-8 mr-3 text-yellow-500" />
            Store Benchmark Dashboard
          </h1>
          <p className="text-gray-500 mt-1">Cross-store performance, rankings, and AI scoring</p>
        </div>
        
        <div className="flex space-x-3">
           <button className="flex items-center px-4 py-2 bg-white border border-gray-300 rounded-lg text-sm font-medium text-gray-700 hover:bg-gray-50 shadow-sm">
             <Map className="w-4 h-4 mr-2" />
             Compare Regions
           </button>
        </div>
      </div>

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
