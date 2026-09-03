import React, { useEffect, useState } from 'react';
import { recommendationApi, aiInsightsApi } from '../api/executiveAiApi';
import { Target, CheckCircle, XCircle, Clock, ShoppingCart, Package, Users, IndianRupee, Filter } from 'lucide-react';

export const RecommendationCenter: React.FC = () => {
  const [recommendations, setRecommendations] = useState<any[]>([]);
  const [loading, setLoading] = useState(true);
  const [area, setArea] = useState('All');

  useEffect(() => {
    fetchRecs();
  }, [area]);

  const fetchRecs = async () => {
    setLoading(true);
    try {
      const data = await recommendationApi.getRecommendations(area === 'All' ? undefined : area);
      setRecommendations(data.filter((r: any) => r.status !== 'Resolved' && r.status !== 'Ignored'));
    } catch (error) {
      console.error('Error fetching recommendations:', error);
    } finally {
      setLoading(false);
    }
  };

  const handleAction = async (id: string, action: string) => {
    try {
      await aiInsightsApi.updateInsightStatus(id, action);
      fetchRecs();
    } catch (error) {
      console.error('Error updating recommendation:', error);
    }
  };

  const areas = ['All', 'Procurement', 'Inventory', 'CRM', 'Finance'];

  const getAreaIcon = (a: string) => {
    switch (a) {
      case 'Procurement': return <ShoppingCart className="w-5 h-5" />;
      case 'Inventory': return <Package className="w-5 h-5" />;
      case 'CRM': return <Users className="w-5 h-5" />;
      case 'Finance': return <IndianRupee className="w-5 h-5" />;
      default: return <Target className="w-5 h-5" />;
    }
  };

  return (
    <div className="p-6 bg-gray-50 min-h-screen">
      <div className="flex justify-between items-center mb-8">
        <div>
          <h1 className="text-3xl font-bold text-gray-900 tracking-tight flex items-center">
            <Target className="w-8 h-8 mr-3 text-emerald-600" />
            AI Recommendation Center
          </h1>
          <p className="text-gray-500 mt-1">Actionable, prescriptive intelligence</p>
        </div>
        
        <div className="flex space-x-2 bg-white p-1 rounded-lg border border-gray-200 shadow-sm">
          {areas.map(a => (
            <button
              key={a}
              onClick={() => setArea(a)}
              className={`px-4 py-2 rounded-md text-sm font-medium transition-colors ${
                area === a ? 'bg-emerald-50 text-emerald-700' : 'text-gray-600 hover:bg-gray-50'
              }`}
            >
              <div className="flex items-center space-x-2">
                {a !== 'All' && getAreaIcon(a)}
                <span>{a}</span>
              </div>
            </button>
          ))}
        </div>
      </div>

      {loading ? (
        <div className="flex justify-center items-center h-64 text-gray-400">Loading Prescriptive Models...</div>
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-6">
          {recommendations.map((rec) => (
            <div key={rec.id} className="bg-white rounded-xl shadow-sm border border-gray-200 hover:border-emerald-300 transition-colors flex flex-col">
              <div className="p-6 flex-1">
                <div className="flex justify-between items-start mb-4">
                  <span className="px-3 py-1 rounded-full text-xs font-bold bg-emerald-100 text-emerald-800 flex items-center">
                    {getAreaIcon(rec.businessArea)}
                    <span className="ml-1.5">{rec.businessArea}</span>
                  </span>
                  <span className="text-xs font-bold text-gray-500 bg-gray-100 px-2 py-1 rounded">
                    Impact: {rec.impactScore}/100
                  </span>
                </div>
                
                <h3 className="text-lg font-bold text-gray-900 mb-2 leading-snug">{rec.title}</h3>
                <p className="text-gray-600 text-sm mb-4 line-clamp-3">{rec.description}</p>
                
                <div className="bg-gray-50 rounded-lg p-3 border border-gray-100 mt-auto">
                  <div className="flex justify-between items-center text-sm">
                    <span className="text-gray-500">Conf. Level</span>
                    <span className="font-bold text-gray-900">{rec.confidenceScore}%</span>
                  </div>
                  {rec.estimatedFinancialImpact > 0 && (
                    <div className="flex justify-between items-center text-sm mt-2 pt-2 border-t border-gray-200">
                      <span className="text-gray-500">Est. Value</span>
                      <span className="font-bold text-emerald-600">₹{rec.estimatedFinancialImpact.toLocaleString()}</span>
                    </div>
                  )}
                </div>
              </div>
              
              <div className="grid grid-cols-2 border-t border-gray-100 divide-x divide-gray-100 bg-gray-50/50 rounded-b-xl">
                <button 
                  onClick={() => handleAction(rec.id, 'Resolved')}
                  className="py-3.5 flex items-center justify-center text-sm font-medium text-emerald-700 hover:bg-emerald-50 transition-colors"
                >
                  <CheckCircle className="w-4 h-4 mr-2" />
                  Accept & Apply
                </button>
                <button 
                  onClick={() => handleAction(rec.id, 'Ignored')}
                  className="py-3.5 flex items-center justify-center text-sm font-medium text-gray-600 hover:bg-gray-100 transition-colors"
                >
                  <XCircle className="w-4 h-4 mr-2" />
                  Ignore
                </button>
              </div>
            </div>
          ))}

          {recommendations.length === 0 && (
            <div className="col-span-full text-center py-20 bg-white rounded-xl border border-dashed border-gray-300">
              <CheckCircle className="w-12 h-12 text-emerald-300 mx-auto mb-3" />
              <h3 className="text-lg font-medium text-gray-900">All Caught Up</h3>
              <p className="text-gray-500 text-sm mt-1">There are no pending recommendations for this area.</p>
            </div>
          )}
        </div>
      )}
    </div>
  );
};
