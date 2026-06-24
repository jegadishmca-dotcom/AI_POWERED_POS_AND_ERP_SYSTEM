import React, { useEffect, useState } from 'react';
import { aiInsightsApi } from '../api/executiveAiApi';
import { ShieldAlert, TrendingUp, Search, CheckCircle, AlertOctagon, Lightbulb } from 'lucide-react';

export const InsightsCenter: React.FC = () => {
  const [insights, setInsights] = useState<any[]>([]);
  const [loading, setLoading] = useState(true);
  const [filter, setFilter] = useState('All');

  useEffect(() => {
    fetchInsights();
  }, [filter]);

  const fetchInsights = async () => {
    setLoading(true);
    try {
      const data = await aiInsightsApi.getInsights(filter === 'All' ? undefined : filter);
      setInsights(data);
    } catch (error) {
      console.error('Error fetching insights:', error);
    } finally {
      setLoading(false);
    }
  };

  const handleStatusUpdate = async (id: string, newStatus: string) => {
    try {
      await aiInsightsApi.updateInsightStatus(id, newStatus, `Updated to ${newStatus}`);
      fetchInsights();
    } catch (error) {
      console.error('Error updating status:', error);
    }
  };

  const getCategoryIcon = (category: string) => {
    switch (category) {
      case 'Risk': return <ShieldAlert className="w-5 h-5 text-red-500" />;
      case 'Opportunity': return <TrendingUp className="w-5 h-5 text-emerald-500" />;
      case 'Observation': return <Search className="w-5 h-5 text-blue-500" />;
      default: return <Lightbulb className="w-5 h-5 text-amber-500" />;
    }
  };

  const getImpactColor = (score: number) => {
    if (score > 80) return 'text-red-700 bg-red-100 border-red-200';
    if (score > 50) return 'text-amber-700 bg-amber-100 border-amber-200';
    return 'text-blue-700 bg-blue-100 border-blue-200';
  };

  return (
    <div className="p-6 bg-gray-50 min-h-screen">
      <div className="flex justify-between items-center mb-8">
        <div>
          <h1 className="text-3xl font-bold text-gray-900 tracking-tight flex items-center">
            <Lightbulb className="w-8 h-8 mr-3 text-amber-500" />
            AI Insights Center
          </h1>
          <p className="text-gray-500 mt-1">Categorized business risks, opportunities, and observations</p>
        </div>
        
        {/* Filters */}
        <div className="flex space-x-2 bg-white p-1 rounded-lg border border-gray-200 shadow-sm">
          {['All', 'New', 'In Progress', 'Resolved'].map(f => (
            <button
              key={f}
              onClick={() => setFilter(f)}
              className={`px-4 py-2 rounded-md text-sm font-medium transition-colors ${
                filter === f ? 'bg-indigo-50 text-indigo-700' : 'text-gray-600 hover:bg-gray-50'
              }`}
            >
              {f}
            </button>
          ))}
        </div>
      </div>

      {loading ? (
        <div className="flex justify-center items-center h-64 text-gray-400">Loading Intelligence...</div>
      ) : (
        <div className="grid grid-cols-1 gap-4">
          {insights.map((insight) => (
            <div key={insight.id} className="bg-white rounded-xl p-6 border border-gray-100 shadow-sm hover:shadow-md transition-shadow">
              <div className="flex justify-between items-start">
                <div className="flex items-start space-x-4">
                  <div className="p-3 bg-gray-50 rounded-lg border border-gray-100">
                    {getCategoryIcon(insight.insightCategory)}
                  </div>
                  <div>
                    <div className="flex items-center space-x-3 mb-1">
                      <h3 className="text-lg font-semibold text-gray-900">{insight.title}</h3>
                      <span className="px-2.5 py-0.5 rounded-full text-xs font-semibold bg-gray-100 text-gray-700">
                        {insight.businessArea}
                      </span>
                    </div>
                    <p className="text-gray-600 text-sm mb-4 leading-relaxed">{insight.description}</p>
                    
                    {/* Metrics Row */}
                    <div className="flex items-center space-x-6 text-sm">
                      <div className="flex flex-col">
                        <span className="text-gray-400 text-xs mb-0.5">Impact Score</span>
                        <span className={`px-2 py-1 rounded border text-xs font-bold w-fit ${getImpactColor(insight.impactScore)}`}>
                          {insight.impactScore}/100
                        </span>
                      </div>
                      <div className="flex flex-col">
                        <span className="text-gray-400 text-xs mb-0.5">Confidence</span>
                        <span className="font-semibold text-gray-700">{insight.confidenceScore}%</span>
                      </div>
                      {insight.estimatedFinancialImpact && (
                        <div className="flex flex-col">
                          <span className="text-gray-400 text-xs mb-0.5">Financial Impact</span>
                          <span className="font-bold text-emerald-600">₹{insight.estimatedFinancialImpact.toLocaleString()}</span>
                        </div>
                      )}
                    </div>
                    
                    {/* AI Explanation / Action */}
                    <div className="mt-4 p-4 bg-indigo-50/50 rounded-lg border border-indigo-100/50">
                      <p className="text-xs font-semibold text-indigo-900 mb-1 flex items-center">
                        <CheckCircle className="w-3.5 h-3.5 mr-1" /> Recommended Action
                      </p>
                      <p className="text-sm text-indigo-800">{insight.recommendedAction || 'Monitor closely and investigate root cause.'}</p>
                    </div>
                  </div>
                </div>

                {/* Actions */}
                <div className="flex flex-col space-y-2 min-w-[140px]">
                  {insight.status !== 'Resolved' && (
                    <button 
                      onClick={() => handleStatusUpdate(insight.id, 'Resolved')}
                      className="px-4 py-2 text-sm font-medium text-emerald-700 bg-emerald-50 border border-emerald-200 rounded-lg hover:bg-emerald-100 transition-colors"
                    >
                      Mark Resolved
                    </button>
                  )}
                  {insight.status === 'New' && (
                    <button 
                      onClick={() => handleStatusUpdate(insight.id, 'In Progress')}
                      className="px-4 py-2 text-sm font-medium text-indigo-700 bg-indigo-50 border border-indigo-200 rounded-lg hover:bg-indigo-100 transition-colors"
                    >
                      Acknowledge
                    </button>
                  )}
                  {insight.status !== 'Ignored' && (
                    <button 
                      onClick={() => handleStatusUpdate(insight.id, 'Ignored')}
                      className="px-4 py-2 text-sm font-medium text-gray-600 bg-white border border-gray-300 rounded-lg hover:bg-gray-50 transition-colors"
                    >
                      Dismiss
                    </button>
                  )}
                  
                  <div className="mt-4 text-right">
                    <span className={`text-xs font-bold uppercase tracking-wider ${insight.status === 'Resolved' ? 'text-emerald-500' : insight.status === 'New' ? 'text-blue-500' : 'text-gray-500'}`}>
                      {insight.status}
                    </span>
                  </div>
                </div>
              </div>
            </div>
          ))}

          {insights.length === 0 && (
            <div className="text-center py-20 bg-white rounded-xl border border-dashed border-gray-300">
              <AlertOctagon className="w-12 h-12 text-gray-300 mx-auto mb-3" />
              <h3 className="text-lg font-medium text-gray-900">No Insights Found</h3>
              <p className="text-gray-500 text-sm mt-1">There are currently no AI insights matching the selected filter.</p>
            </div>
          )}
        </div>
      )}
    </div>
  );
};
