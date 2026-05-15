import React from 'react';
import { MapPin, Plus } from 'lucide-react';

export const WarehouseLocationsList = () => {
  const warehouses = [
    { id: 1, name: 'Main Store', code: 'WH-MAIN', bins: ['A1-01', 'A1-02', 'B1-01'] },
    { id: 2, name: 'Backroom Storage', code: 'WH-BACK', bins: ['C1-01', 'C1-02'] },
  ];

  return (
    <div className="bg-white shadow rounded-lg p-6 max-w-4xl mx-auto">
      <div className="flex justify-between items-center mb-6 border-b pb-4">
        <h2 className="text-2xl font-bold text-slate-800 flex items-center">
          <MapPin className="mr-3 text-red-600" /> Warehouse & Bins
        </h2>
        <button className="px-4 py-2 bg-red-600 text-white rounded flex items-center font-bold hover:bg-red-700">
          <Plus className="w-5 h-5 mr-1" /> Add Warehouse
        </button>
      </div>

      <div className="grid gap-6">
        {warehouses.map(wh => (
          <div key={wh.id} className="border rounded-lg p-4 bg-slate-50">
            <div className="flex justify-between items-center mb-4 border-b pb-2">
              <h3 className="font-bold text-lg text-slate-800">{wh.name} <span className="text-sm text-gray-500 ml-2">({wh.code})</span></h3>
              <button className="text-red-600 text-sm font-bold">+ Add Bin</button>
            </div>
            <div className="flex flex-wrap gap-2">
              {wh.bins.map(bin => (
                <span key={bin} className="bg-white border border-gray-300 px-3 py-1 rounded text-sm shadow-sm">{bin}</span>
              ))}
            </div>
          </div>
        ))}
      </div>
    </div>
  );
};
