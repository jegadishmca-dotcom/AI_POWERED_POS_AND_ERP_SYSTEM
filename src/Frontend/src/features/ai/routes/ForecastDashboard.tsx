import React, { useEffect, useState } from 'react';
import { forecastApi } from '../api/executiveAiApi';
import { LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip as RechartsTooltip, ResponsiveContainer, Legend } from 'recharts';
import { TrendingUp, BarChart2, Package, Activity, Download } from 'lucide-react';

export const ForecastDashboard: React.FC = () => {
  const [forecasts, setForecasts] = useState<any[]>([]);
  const [accuracy, setAccuracy] = useState<any[]>([]);
  const [loading, setLoading] = useState(true);
  const [viewType, setViewType] = useState('PRODUCT');

  useEffect(() => {
    fetchData();
  }, [viewType]);

  const fetchData = async () => {
    setLoading(true);
    try {
      const [fData, aData] = await Promise.all([
        forecastApi.getForecasts(viewType),
        forecastApi.getAccuracy()
      ]);
      setForecasts(fData);
      setAccuracy(aData);
    } catch (error) {
      console.error('Error fetching forecasts:', error);
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="p-6 bg-gray-50 min-h-screen">
      <div className="flex justify-between items-center mb-8">
        <div>
          <h1 className="text-3xl font-bold text-gray-900 tracking-tight flex items-center">
            <TrendingUp className="w-8 h-8 mr-3 text-indigo-600" />
            AI Demand Forecasting
          </h1>
          <p className="text-gray-500 mt-1">Predictive analytics for Product, Category, and Store demand</p>
        </div>
        
        <div className="flex space-x-3">
           <div className="flex bg-white rounded-lg p-1 border border-gray-200 shadow-sm">
             {['PRODUCT', 'CATEGORY', 'STORE'].map(type => (
               <button
                 key={type}
                 onClick={() => setViewType(type)}
                 className={`px-4 py-2 rounded-md text-sm font-semibold transition-all ${
                   viewType === type 
                     ? 'bg-indigo-600 text-white shadow-sm' 
                     : 'text-gray-600 hover:bg-gray-50'
                 }`}
               >
                 {type.charAt(0) + type.slice(1).toLowerCase()}
               </button>
             ))}
           </div>
           <button className="flex items-center px-4 py-2 bg-white border border-gray-300 rounded-lg text-sm font-medium text-gray-700 hover:bg-gray-50 shadow-sm">
             <Download className="w-4 h-4 mr-2" />
             Export Data
           </button>
        </div>
      </div>

      {loading ? (
        <div className="flex justify-center items-center h-64 text-gray-400">Processing Models...</div>
      ) : (
        <>
          {/* Top KPI row for model accuracy */}
          {accuracy.length > 0 && (
            <div className="grid grid-cols-1 md:grid-cols-3 gap-6 mb-8">
              <AccuracyCard title="Mean Absolute Percentage Error" value={`${accuracy[0].overallMape.toFixed(2)}%`} desc="Overall MAPE (lower is better)" />
              <AccuracyCard title="Root Mean Square Error" value={accuracy[0].overallRmse.toFixed(2)} desc="Overall RMSE (lower is better)" />
              <AccuracyCard title="Model Health" value="OPTIMAL" desc="Retraining automatically daily" highlight />
            </div>
          )}

          <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
            <div className="col-span-2 bg-white p-6 rounded-xl shadow-sm border border-gray-100">
              <h2 className="text-lg font-bold text-gray-900 mb-6 flex items-center">
                <BarChart2 className="w-5 h-5 mr-2 text-indigo-500" />
                Forecast vs Actual Trends
              </h2>
              <div className="h-[400px]">
                <ResponsiveContainer width="100%" height="100%">
                  <LineChart data={forecasts} margin={{ top: 5, right: 30, left: 20, bottom: 5 }}>
                    <CartesianGrid strokeDasharray="3 3" vertical={false} stroke="#E5E7EB" />
                    <XAxis dataKey="forecastDate" tickFormatter={(val) => new Date(val).toLocaleDateString()} stroke="#9CA3AF" fontSize={12} />
                    <YAxis stroke="#9CA3AF" fontSize={12} />
                    <RechartsTooltip 
                      labelFormatter={(val) => new Date(val).toLocaleDateString()}
                      contentStyle={{ borderRadius: '8px', border: '1px solid #E5E7EB', boxShadow: '0 4px 6px -1px rgba(0, 0, 0, 0.1)' }}
                    />
                    <Legend wrapperStyle={{ paddingTop: '20px' }} />
                    <Line type="monotone" name="Predicted Quantity" dataKey="predictedQuantity" stroke="#4F46E5" strokeWidth={3} dot={false} activeDot={{ r: 8 }} />
                    <Line type="monotone" name="Upper Bound (95%)" dataKey="upperBoundQuantity" stroke="#818CF8" strokeWidth={1} strokeDasharray="5 5" dot={false} />
                    <Line type="monotone" name="Lower Bound (95%)" dataKey="lowerBoundQuantity" stroke="#818CF8" strokeWidth={1} strokeDasharray="5 5" dot={false} />
                  </LineChart>
                </ResponsiveContainer>
              </div>
            </div>

            <div className="col-span-1 bg-white rounded-xl shadow-sm border border-gray-100 overflow-hidden flex flex-col">
              <div className="p-6 border-b border-gray-100 bg-gray-50/50">
                <h2 className="text-lg font-bold text-gray-900 flex items-center">
                  <Package className="w-5 h-5 mr-2 text-indigo-500" />
                  Upcoming Projections
                </h2>
              </div>
              <div className="flex-1 overflow-y-auto p-2">
                {forecasts.slice(0, 10).map((f, i) => (
                  <div key={i} className="flex justify-between items-center p-4 border-b border-gray-50 last:border-0 hover:bg-gray-50 transition-colors rounded-lg">
                    <div>
                      <p className="text-sm font-semibold text-gray-900">{f.targetName || f.targetId}</p>
                      <p className="text-xs text-gray-500 mt-1">{new Date(f.forecastDate).toLocaleDateString()}</p>
                    </div>
                    <div className="text-right">
                      <p className="text-lg font-bold text-indigo-600">{f.predictedQuantity.toLocaleString()}</p>
                      <div className="flex items-center justify-end mt-1 space-x-1">
                        <Activity className="w-3 h-3 text-emerald-500" />
                        <span className="text-xs font-medium text-emerald-600">Conf: {f.confidenceLevel}%</span>
                      </div>
                    </div>
                  </div>
                ))}
              </div>
            </div>
          </div>
        </>
      )}
    </div>
  );
};

const AccuracyCard = ({ title, value, desc, highlight }: any) => (
  <div className={`p-6 rounded-xl border ${highlight ? 'bg-indigo-600 border-indigo-700 text-white shadow-lg shadow-indigo-200' : 'bg-white border-gray-100 shadow-sm'}`}>
    <p className={`text-sm font-medium mb-1 ${highlight ? 'text-indigo-100' : 'text-gray-500'}`}>{title}</p>
    <h3 className={`text-3xl font-bold tracking-tight mb-1 ${highlight ? 'text-white' : 'text-gray-900'}`}>{value}</h3>
    <p className={`text-xs ${highlight ? 'text-indigo-200' : 'text-gray-400'}`}>{desc}</p>
  </div>
);
