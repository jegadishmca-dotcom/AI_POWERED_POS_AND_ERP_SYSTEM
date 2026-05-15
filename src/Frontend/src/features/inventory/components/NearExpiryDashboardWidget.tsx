import React from 'react';
import { AlertTriangle, Clock } from 'lucide-react';

export const NearExpiryDashboardWidget = () => {
  const alerts = [
    { id: 1, product: 'Amul Butter 500g', batch: 'B-098', days: 5, stock: 15 },
    { id: 2, product: 'Britannia Bread', batch: 'B-102', days: 2, stock: 8 },
  ];

  return (
    <div className="bg-white shadow rounded-lg p-4 border-l-4 border-orange-500">
      <div className="flex items-center justify-between mb-4">
        <h3 className="font-bold text-slate-800 flex items-center">
          <AlertTriangle className="w-5 h-5 text-orange-500 mr-2" /> Near Expiry Alerts
        </h3>
        <span className="bg-orange-100 text-orange-800 text-xs px-2 py-1 rounded font-bold">30 Days</span>
      </div>

      <div className="space-y-3">
        {alerts.map(alert => (
          <div key={alert.id} className="flex justify-between items-center p-3 bg-orange-50 rounded border border-orange-100">
            <div>
              <p className="font-bold text-sm text-slate-800">{alert.product}</p>
              <p className="text-xs text-gray-500">Batch: {alert.batch} | Stock: {alert.stock}</p>
            </div>
            <div className="text-right">
              <p className={ont-bold text-sm flex items-center justify-end \}>
                <Clock className="w-3 h-3 mr-1" /> {alert.days} Days
              </p>
            </div>
          </div>
        ))}
      </div>
    </div>
  );
};
