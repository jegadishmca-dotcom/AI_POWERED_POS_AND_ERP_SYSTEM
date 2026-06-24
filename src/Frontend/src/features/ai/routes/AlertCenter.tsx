import React, { useEffect, useState } from 'react';
import { alertCenterApi } from '../api/executiveAiApi';
import { AlertTriangle, Bell, Clock, CheckCircle2, ShieldAlert } from 'lucide-react';

export const AlertCenter: React.FC = () => {
  const [alerts, setAlerts] = useState<any[]>([]);
  const [loading, setLoading] = useState(true);
  const [filter, setFilter] = useState('All');

  useEffect(() => {
    fetchAlerts();
  }, [filter]);

  const fetchAlerts = async () => {
    setLoading(true);
    try {
      const severity = filter !== 'All' ? filter : undefined;
      const data = await alertCenterApi.getAlerts(severity, false);
      setAlerts(data);
    } catch (error) {
      console.error('Error fetching alerts:', error);
    } finally {
      setLoading(false);
    }
  };

  const handleAction = async (id: string, action: 'acknowledge' | 'resolve') => {
    try {
      if (action === 'acknowledge') {
        await alertCenterApi.acknowledgeAlert(id);
      } else {
        await alertCenterApi.resolveAlert(id);
      }
      fetchAlerts();
    } catch (error) {
      console.error('Error updating alert:', error);
    }
  };

  const getSeverityStyle = (severity: string) => {
    switch (severity) {
      case 'Critical': return 'bg-red-100 text-red-800 border-red-200';
      case 'High': return 'bg-orange-100 text-orange-800 border-orange-200';
      case 'Medium': return 'bg-amber-100 text-amber-800 border-amber-200';
      default: return 'bg-blue-100 text-blue-800 border-blue-200';
    }
  };

  return (
    <div className="p-6 bg-gray-50 min-h-screen">
      <div className="flex justify-between items-center mb-8">
        <div>
          <h1 className="text-3xl font-bold text-gray-900 tracking-tight flex items-center">
            <Bell className="w-8 h-8 mr-3 text-red-600" />
            Active Alerts Center
          </h1>
          <p className="text-gray-500 mt-1">Real-time AI anomaly detection and critical alerts</p>
        </div>
        
        <div className="flex space-x-2 bg-white p-1 rounded-lg border border-gray-200 shadow-sm">
          {['All', 'Critical', 'High', 'Medium', 'Low'].map(s => (
            <button
              key={s}
              onClick={() => setFilter(s)}
              className={`px-4 py-2 rounded-md text-sm font-medium transition-colors ${
                filter === s ? 'bg-red-50 text-red-700' : 'text-gray-600 hover:bg-gray-50'
              }`}
            >
              {s}
            </button>
          ))}
        </div>
      </div>

      {loading ? (
        <div className="flex justify-center items-center h-64 text-gray-400">Scanning for Anomalies...</div>
      ) : (
        <div className="space-y-4">
          {alerts.map((alert) => (
            <div key={alert.id} className="bg-white rounded-xl p-5 border-l-4 shadow-sm flex flex-col md:flex-row justify-between items-start md:items-center hover:shadow-md transition-shadow" style={{ borderLeftColor: alert.severity === 'Critical' ? '#DC2626' : alert.severity === 'High' ? '#F97316' : '#F59E0B' }}>
              <div className="flex items-start md:items-center flex-1">
                <div className={`p-3 rounded-full mr-4 ${getSeverityStyle(alert.severity).replace('border-', '')}`}>
                  <AlertTriangle className="w-6 h-6" />
                </div>
                <div>
                  <div className="flex items-center space-x-3 mb-1">
                    <span className={`px-2 py-0.5 rounded text-xs font-bold border ${getSeverityStyle(alert.severity)}`}>
                      {alert.severity}
                    </span>
                    <h3 className="text-lg font-bold text-gray-900">{alert.title}</h3>
                  </div>
                  <p className="text-gray-600 text-sm">{alert.message}</p>
                  <div className="flex items-center space-x-4 mt-2 text-xs text-gray-500 font-medium">
                    <span className="flex items-center">
                      <Clock className="w-3.5 h-3.5 mr-1" />
                      {new Date(alert.createdAt).toLocaleString()}
                    </span>
                    <span className="px-2 py-0.5 bg-gray-100 rounded text-gray-700">
                      {alert.alertType}
                    </span>
                  </div>
                </div>
              </div>
              
              <div className="mt-4 md:mt-0 ml-0 md:ml-6 flex space-x-3 shrink-0">
                {alert.isAcknowledged ? (
                  <button 
                    onClick={() => handleAction(alert.id, 'resolve')}
                    className="px-4 py-2 flex items-center text-sm font-medium text-white bg-indigo-600 rounded-lg hover:bg-indigo-700 transition-colors shadow-sm"
                  >
                    <CheckCircle2 className="w-4 h-4 mr-2" />
                    Resolve Issue
                  </button>
                ) : (
                  <button 
                    onClick={() => handleAction(alert.id, 'acknowledge')}
                    className="px-4 py-2 flex items-center text-sm font-medium text-indigo-700 bg-indigo-50 border border-indigo-200 rounded-lg hover:bg-indigo-100 transition-colors"
                  >
                    <ShieldAlert className="w-4 h-4 mr-2" />
                    Acknowledge
                  </button>
                )}
              </div>
            </div>
          ))}

          {alerts.length === 0 && (
            <div className="text-center py-20 bg-white rounded-xl border border-dashed border-gray-300">
              <CheckCircle2 className="w-12 h-12 text-emerald-400 mx-auto mb-3" />
              <h3 className="text-lg font-medium text-gray-900">System Healthy</h3>
              <p className="text-gray-500 text-sm mt-1">There are no active alerts requiring your attention.</p>
            </div>
          )}
        </div>
      )}
    </div>
  );
};
