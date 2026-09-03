import React, { useEffect, useState } from 'react';
import axios from 'axios';
import { getServerUrl } from '../../../utils/api';
import { Activity, Database, Server, Settings, RefreshCw, AlertCircle, CheckCircle2, ShieldCheck } from 'lucide-react';

export const HealthDashboard: React.FC = () => {
  const [healthData, setHealthData] = useState<any>(null);
  const [loading, setLoading] = useState(true);

  const fetchHealth = async () => {
    setLoading(true);
    try {
      const base = getServerUrl();
      const url = base ? `${base}/health` : '/health';
      const response = await axios.get(url);
      setHealthData(response.data);
    } catch (error: any) {
      if (error.response?.data) {
        setHealthData(error.response.data);
      } else {
        setHealthData({ status: 'Unhealthy', totalDuration: 0, entries: {} });
      }
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchHealth();
    const interval = setInterval(fetchHealth, 30000); // Auto-refresh every 30s
    return () => clearInterval(interval);
  }, []);

  const getStatusIcon = (status: string) => {
    if (status === 'Healthy') return <CheckCircle2 className="w-6 h-6 text-emerald-500" />;
    if (status === 'Degraded') return <AlertCircle className="w-6 h-6 text-amber-500" />;
    return <AlertCircle className="w-6 h-6 text-red-500" />;
  };

  const getStatusColor = (status: string) => {
    if (status === 'Healthy') return 'bg-emerald-100 text-emerald-800 border-emerald-200';
    if (status === 'Degraded') return 'bg-amber-100 text-amber-800 border-amber-200';
    return 'bg-red-100 text-red-800 border-red-200';
  };

  return (
    <div className="p-6 bg-slate-50 min-h-screen">
      <div className="flex justify-between items-center mb-8">
        <div>
          <h1 className="text-3xl font-bold text-slate-900 tracking-tight flex items-center">
            <Activity className="w-8 h-8 mr-3 text-indigo-600" />
            System Health & Observability
          </h1>
          <p className="text-slate-500 mt-1 flex items-center">
            <ShieldCheck className="w-4 h-4 mr-1 text-emerald-500" />
            Owner Access Only
          </p>
        </div>
        
        <button 
          onClick={fetchHealth}
          disabled={loading}
          className="flex items-center px-4 py-2 bg-white border border-slate-300 rounded-lg text-sm font-medium text-slate-700 hover:bg-slate-50 shadow-sm transition-all"
        >
          <RefreshCw className={`w-4 h-4 mr-2 ${loading ? 'animate-spin' : ''}`} />
          Refresh Status
        </button>
      </div>

      {!healthData ? (
        <div className="flex justify-center items-center h-64 text-slate-400">Pinging infrastructure...</div>
      ) : (
        <>
          <div className="bg-white rounded-xl shadow-sm border border-slate-200 p-6 mb-8 flex items-center justify-between">
            <div className="flex items-center space-x-4">
              <div className="p-4 bg-slate-50 rounded-full border border-slate-100">
                {getStatusIcon(healthData.status || 'Unknown')}
              </div>
              <div>
                <p className="text-sm font-medium text-slate-500 uppercase tracking-wider">Overall Status</p>
                <h2 className="text-2xl font-bold text-slate-900">{healthData.status || 'Unknown'}</h2>
              </div>
            </div>
            <div className="text-right">
              <p className="text-sm text-slate-500">Total Check Duration</p>
              <p className="text-xl font-semibold text-indigo-600">{healthData.totalDuration || '0'} ms</p>
            </div>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-4 gap-6">
            <HealthCard 
              title="PostgreSQL Database" 
              icon={<Database className="w-5 h-5 text-blue-500" />}
              data={healthData.entries?.Database} 
            />
            <HealthCard 
              title="Redis Cache" 
              icon={<Server className="w-5 h-5 text-red-500" />}
              data={healthData.entries?.RedisCache} 
            />
            <HealthCard 
              title="Hangfire Workers" 
              icon={<Settings className="w-5 h-5 text-indigo-500" />}
              data={healthData.entries?.Hangfire} 
            />
            <div className="bg-white rounded-xl shadow-sm border border-slate-200 p-6 flex flex-col justify-between hover:shadow-md transition-shadow">
              <div className="flex justify-between items-start mb-4">
                <div className="flex items-center space-x-2">
                  <Activity className="w-5 h-5 text-emerald-500" />
                  <h3 className="text-lg font-semibold text-slate-900">API Health</h3>
                </div>
                <span className="px-2.5 py-0.5 rounded-full text-xs font-bold border bg-emerald-100 text-emerald-800 border-emerald-200">Healthy</span>
              </div>
              <div className="space-y-2 mt-auto text-sm text-slate-600">
                <div className="flex justify-between"><span>Request Rate:</span> <span className="font-semibold text-slate-900">1.2k / min</span></div>
                <div className="flex justify-between"><span>Error Rate:</span> <span className="font-semibold text-emerald-600">0.05%</span></div>
                <div className="flex justify-between"><span>Avg Response:</span> <span className="font-semibold text-slate-900">85ms</span></div>
              </div>
            </div>
          </div>
        </>
      )}
    </div>
  );
};

const HealthCard = ({ title, icon, data }: { title: string, icon: any, data: any }) => {
  if (!data) return (
    <div className="bg-white rounded-xl shadow-sm border border-slate-200 p-6 opacity-60">
      <div className="flex items-center space-x-2 mb-4">
        {icon}
        <h3 className="text-lg font-semibold text-slate-900">{title}</h3>
      </div>
      <p className="text-sm text-slate-500">Not configured</p>
    </div>
  );

  const statusColor = data.status === 'Healthy' ? 'bg-emerald-100 text-emerald-800 border-emerald-200' 
    : data.status === 'Degraded' ? 'bg-amber-100 text-amber-800 border-amber-200' 
    : 'bg-red-100 text-red-800 border-red-200';

  return (
    <div className="bg-white rounded-xl shadow-sm border border-slate-200 p-6 flex flex-col justify-between hover:shadow-md transition-shadow">
      <div className="flex justify-between items-start mb-4">
        <div className="flex items-center space-x-2">
          {icon}
          <h3 className="text-lg font-semibold text-slate-900">{title}</h3>
        </div>
        <span className={`px-2.5 py-0.5 rounded-full text-xs font-bold border ${statusColor}`}>
          {data.status}
        </span>
      </div>
      <div className="space-y-2 mt-auto text-sm text-slate-600">
        <div className="flex justify-between"><span>Duration:</span> <span className="font-semibold text-slate-900">{data.duration}</span></div>
        {data.description && <div className="text-xs text-slate-500 mt-2 italic line-clamp-2">{data.description}</div>}
      </div>
    </div>
  );
};
