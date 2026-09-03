import React from 'react';
import { MapPin, Plus } from 'lucide-react';

/**
 * TODO / BACKLOG NOTE:
 * Warehouse and Bin management in WarehouseLocationsList.tsx will operate on
 * local state / localStorage persistence (which DOES persist across browser page reloads).
 * However, backend database API endpoints (e.g. POST /api/warehouses and POST /api/warehouses/{id}/bins)
 * do NOT exist in the backend service yet, so data will not persist to PostgreSQL.
 */
import { useState, useEffect } from 'react';
import { Modal } from '../../../components/common/Modal';

interface Warehouse {
  id: number;
  name: string;
  code: string;
  bins: string[];
}

const STORAGE_KEY = 'erp_warehouse_locations';

const INITIAL_WAREHOUSES: Warehouse[] = [
  { id: 1, name: 'Main Store', code: 'WH-MAIN', bins: ['A1-01', 'A1-02', 'B1-01'] },
  { id: 2, name: 'Backroom Storage', code: 'WH-BACK', bins: ['C1-01', 'C1-02'] },
];

export const WarehouseLocationsList = () => {
  const [warehouses, setWarehouses] = useState<Warehouse[]>(() => {
    try {
      const saved = localStorage.getItem(STORAGE_KEY);
      return saved ? JSON.parse(saved) : INITIAL_WAREHOUSES;
    } catch {
      return INITIAL_WAREHOUSES;
    }
  });

  useEffect(() => {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(warehouses));
  }, [warehouses]);

  // Modal states
  const [isWarehouseModalOpen, setIsWarehouseModalOpen] = useState(false);
  const [newWhName, setNewWhName] = useState('');
  const [newWhCode, setNewWhCode] = useState('');
  const [whErrorMessage, setWhErrorMessage] = useState<string | null>(null);

  const [isBinModalOpen, setIsBinModalOpen] = useState(false);
  const [selectedWhId, setSelectedWhId] = useState<number | null>(null);
  const [newBinCode, setNewBinCode] = useState('');
  const [binErrorMessage, setBinErrorMessage] = useState<string | null>(null);

  const handleAddWarehouse = (e: React.FormEvent) => {
    e.preventDefault();
    setWhErrorMessage(null);
    if (!newWhName.trim() || !newWhCode.trim()) {
      setWhErrorMessage('Please enter both warehouse name and location code.');
      return;
    }
    const newWh: Warehouse = {
      id: Date.now(),
      name: newWhName.trim(),
      code: newWhCode.trim().toUpperCase(),
      bins: []
    };
    setWarehouses(prev => [...prev, newWh]);
    setNewWhName('');
    setNewWhCode('');
    setIsWarehouseModalOpen(false);
  };

  const handleOpenAddBin = (whId: number) => {
    setSelectedWhId(whId);
    setNewBinCode('');
    setBinErrorMessage(null);
    setIsBinModalOpen(true);
  };

  const handleAddBin = (e: React.FormEvent) => {
    e.preventDefault();
    setBinErrorMessage(null);
    if (!newBinCode.trim() || selectedWhId === null) {
      setBinErrorMessage('Please enter a valid bin designation code.');
      return;
    }
    const formattedBin = newBinCode.trim().toUpperCase();
    setWarehouses(prev => prev.map(wh => {
      if (wh.id === selectedWhId) {
        if (wh.bins.includes(formattedBin)) {
          setBinErrorMessage('This bin code already exists in the selected warehouse.');
          return wh;
        }
        return { ...wh, bins: [...wh.bins, formattedBin] };
      }
      return wh;
    }));
    setIsBinModalOpen(false);
    setNewBinCode('');
    setSelectedWhId(null);
  };

  return (
    <div className="bg-white dark:bg-slate-900 shadow rounded-xl p-6 max-w-4xl mx-auto border border-slate-200 dark:border-slate-800">
      <div className="flex justify-between items-center mb-6 border-b border-slate-200 dark:border-slate-800 pb-4">
        <div>
          <h2 className="text-2xl font-bold text-slate-800 dark:text-white flex items-center">
            <MapPin className="mr-3 text-red-600" /> Warehouse & Bins
          </h2>
          <p className="text-xs text-slate-500 mt-1">Configured storage zones and bin locations</p>
        </div>
        <button 
          onClick={() => { setWhErrorMessage(null); setIsWarehouseModalOpen(true); }}
          className="px-4 py-2 bg-red-600 hover:bg-red-700 text-white rounded-lg flex items-center font-bold text-sm shadow-md transition-all cursor-pointer"
        >
          <Plus className="w-5 h-5 mr-1" /> Add Warehouse
        </button>
      </div>

      <div className="grid gap-6">
        {warehouses.map(wh => (
          <div key={wh.id} className="border border-slate-200 dark:border-slate-800 rounded-xl p-4 bg-slate-50 dark:bg-slate-800/60">
            <div className="flex justify-between items-center mb-4 border-b border-slate-200 dark:border-slate-700/60 pb-2">
              <h3 className="font-bold text-lg text-slate-800 dark:text-white">
                {wh.name} <span className="text-sm text-slate-500 ml-2 font-mono">({wh.code})</span>
              </h3>
              <button 
                onClick={() => handleOpenAddBin(wh.id)}
                className="text-red-600 dark:text-red-400 hover:text-red-700 text-sm font-bold flex items-center gap-1 cursor-pointer"
              >
                <Plus className="w-4 h-4" /> Add Bin
              </button>
            </div>
            <div className="flex flex-wrap gap-2">
              {wh.bins.length > 0 ? (
                wh.bins.map(bin => (
                  <span key={bin} className="bg-white dark:bg-slate-900 border border-slate-300 dark:border-slate-700 px-3 py-1 rounded-md text-sm font-medium text-slate-700 dark:text-slate-300 shadow-sm">
                    {bin}
                  </span>
                ))
              ) : (
                <span className="text-xs text-slate-400 italic">No bins assigned yet</span>
              )}
            </div>
          </div>
        ))}
      </div>

      {/* Add Warehouse Modal */}
      <Modal isOpen={isWarehouseModalOpen} onClose={() => setIsWarehouseModalOpen(false)} title="Add New Warehouse">
        <form onSubmit={handleAddWarehouse} className="space-y-4">
          {whErrorMessage && <div className="text-sm text-rose-600 bg-rose-50 p-3 rounded-lg">{whErrorMessage}</div>}
          <div>
            <label className="block text-xs font-bold text-slate-500 uppercase mb-1">Warehouse Name</label>
            <input
              type="text"
              required
              value={newWhName}
              onChange={(e) => setNewWhName(e.target.value)}
              placeholder="e.g. Distribution Hub North"
              className="w-full px-4 py-2.5 bg-slate-50 dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-lg outline-none focus:ring-2 focus:ring-red-500 text-slate-800 dark:text-white text-sm"
            />
          </div>
          <div>
            <label className="block text-xs font-bold text-slate-500 uppercase mb-1">Location Code</label>
            <input
              type="text"
              required
              value={newWhCode}
              onChange={(e) => setNewWhCode(e.target.value)}
              placeholder="e.g. WH-NORTH"
              className="w-full px-4 py-2.5 bg-slate-50 dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-lg outline-none focus:ring-2 focus:ring-red-500 text-slate-800 dark:text-white text-sm uppercase"
            />
          </div>
          <button type="submit" className="w-full bg-red-600 hover:bg-red-700 text-white font-bold py-2.5 rounded-lg transition-all text-sm cursor-pointer">
            Save Warehouse
          </button>
        </form>
      </Modal>

      {/* Add Bin Modal */}
      <Modal isOpen={isBinModalOpen} onClose={() => setIsBinModalOpen(false)} title="Add Storage Bin">
        <form onSubmit={handleAddBin} className="space-y-4">
          {binErrorMessage && <div className="text-sm text-rose-600 bg-rose-50 p-3 rounded-lg">{binErrorMessage}</div>}
          <div>
            <label className="block text-xs font-bold text-slate-500 uppercase mb-1">Bin Code / Label</label>
            <input
              type="text"
              required
              value={newBinCode}
              onChange={(e) => setNewBinCode(e.target.value)}
              placeholder="e.g. D1-01 or RACK-B2"
              className="w-full px-4 py-2.5 bg-slate-50 dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-lg outline-none focus:ring-2 focus:ring-red-500 text-slate-800 dark:text-white text-sm uppercase"
            />
          </div>
          <button type="submit" className="w-full bg-red-600 hover:bg-red-700 text-white font-bold py-2.5 rounded-lg transition-all text-sm cursor-pointer">
            Save Bin
          </button>
        </form>
      </Modal>
    </div>
  );
};
