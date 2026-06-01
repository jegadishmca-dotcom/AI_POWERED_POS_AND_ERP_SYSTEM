import React, { useState, useEffect } from 'react';
import { ShieldAlert, AlertTriangle, CheckCircle, Search, UserX } from 'lucide-react';
import { getFraudDetection, FraudDetectionResult } from '../../ai/api/ai.api';

export const LossPreventionDashboard: React.FC = () => {
  const [loading, setLoading] = useState(true);
  const [data, setData] = useState<FraudDetectionResult | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const fetchStats = async () => {
      try {
        setLoading(true);
        const result = await getFraudDetection();
        setData(result);
      } catch (err: any) {
        setError(err.message || 'Failed to fetch AI Fraud Detection stats');
      } finally {
        setLoading(false);
      }
    };
    fetchStats();
  }, []);

  if (loading) {
    return (
      <div className="flex items-center justify-center h-96">
        <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-red-600"></div>
        <span className="ml-3 text-gray-500 font-medium">AI Analyzing POS Patterns...</span>
      </div>
    );
  }

  if (error) {
    return (
      <div className="p-4 bg-red-50 text-red-700 rounded-lg flex items-center shadow-sm">
        <AlertTriangle className="h-5 w-5 mr-2" />
        {error}
      </div>
    );
  }

  const flagged = data?.flaggedCashiers || [];

  return (
    <div className="space-y-6 max-w-6xl mx-auto pb-12">
      <div className="flex justify-between items-end">
        <div>
          <h1 className="text-2xl font-bold text-gray-900 flex items-center">
            <ShieldAlert className="h-7 w-7 text-red-600 mr-2" />
            AI Loss Prevention & Fraud Detection
          </h1>
          <p className="text-gray-500 mt-1">Smart analysis of POS transaction patterns to detect anomalies, high void rates, and suspicious price overrides.</p>
        </div>
      </div>

      <div className="bg-white rounded-xl shadow-sm border border-gray-200 overflow-hidden">
        <div className="bg-gray-50 px-6 py-4 border-b border-gray-200 flex justify-between items-center">
          <div className="flex items-center text-sm font-medium text-gray-700">
            <Search className="h-4 w-4 mr-2 text-gray-400" />
            Last 7 Days Scan Results
          </div>
          <span className="text-xs text-gray-500">{data?.message}</span>
        </div>

        {flagged.length === 0 ? (
          <div className="p-12 text-center flex flex-col items-center">
            <CheckCircle className="h-16 w-16 text-green-500 mb-4 opacity-80" />
            <h3 className="text-lg font-medium text-gray-900">No Suspicious Activity Detected</h3>
            <p className="text-gray-500 max-w-md mx-auto mt-2">The AI has analyzed all POS transactions from the last 7 days and found no anomalies in cashier behavior.</p>
          </div>
        ) : (
          <div className="divide-y divide-gray-100">
            {flagged.map((alert, idx) => (
              <div key={idx} className="p-6 hover:bg-gray-50 transition-colors">
                <div className="flex items-start justify-between">
                  <div className="flex items-start space-x-4">
                    <div className={`p-3 rounded-full flex-shrink-0 ${
                      alert.riskLevel === 'High' ? 'bg-red-100 text-red-600' : 'bg-yellow-100 text-yellow-600'
                    }`}>
                      <UserX className="h-6 w-6" />
                    </div>
                    <div>
                      <h4 className="text-lg font-semibold text-gray-900 flex items-center">
                        Cashier ID: {alert.cashierId.substring(0, 8)}...
                        <span className={`ml-3 text-xs font-medium px-2.5 py-0.5 rounded-full ${
                          alert.riskLevel === 'High' ? 'bg-red-100 text-red-800 border border-red-200' : 'bg-yellow-100 text-yellow-800 border border-yellow-200'
                        }`}>
                          {alert.riskLevel} Risk
                        </span>
                      </h4>
                      <div className="mt-2 text-sm text-gray-700 bg-white p-3 rounded-md border border-gray-200 shadow-sm leading-relaxed">
                        <span className="font-medium text-gray-900">AI Observation:</span> {alert.reason}
                      </div>
                      <div className="mt-3 flex items-start text-sm text-blue-800 bg-blue-50 p-3 rounded-md border border-blue-100">
                        <ShieldAlert className="h-4 w-4 mr-2 flex-shrink-0 mt-0.5" />
                        <span><span className="font-semibold">Recommended Action:</span> {alert.recommendedAction}</span>
                      </div>
                    </div>
                  </div>
                </div>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
};
