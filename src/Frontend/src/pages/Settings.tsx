import React, { useState } from 'react';
import { ServerIpConfig } from '../features/settings/components/ServerIpConfig';
import { PrinterConfig } from '../features/settings/components/PrinterConfig';
import { TerminalConfig } from '../features/settings/components/TerminalConfig';
import { UserManagement } from '../features/settings/components/UserManagement';
import { ChangeOverridePinPanel } from '../features/settings/components/ChangeOverridePinPanel';
import { Settings as SettingsIcon, Network, Printer, Monitor, Users, ShieldCheck, Database } from 'lucide-react';
import { useAuthStore } from '../features/auth/store/auth.store';
import { InventorySafeguards } from '../features/settings/components/InventorySafeguards';

type SettingsTab = 'connection' | 'printers' | 'terminals' | 'users' | 'security' | 'inventoryRules';

export const Settings: React.FC = () => {
  const { user } = useAuthStore();
  const isOwner = user?.role === 'Owner' || user?.role === 'Manager';
  const [activeTab, setActiveTab] = useState<SettingsTab>('connection');

  // Non-owners (cashiers) can only edit their local printer settings
  const tabs: { id: SettingsTab; label: string; icon: React.ReactNode; roles: string[] }[] = [
    { id: 'connection', label: 'Connection Setup', icon: <Network className="w-4 h-4" />, roles: ['Owner', 'Manager'] },
    { id: 'printers', label: 'Printers Configuration', icon: <Printer className="w-4 h-4" />, roles: ['Owner', 'Manager', 'Cashier', 'Supervisor'] },
    { id: 'terminals', label: 'POS Terminals', icon: <Monitor className="w-4 h-4" />, roles: ['Owner', 'Manager'] },
    { id: 'users', label: 'Staff Accounts', icon: <Users className="w-4 h-4" />, roles: ['Owner', 'Manager'] },
    { id: 'inventoryRules', label: 'Inventory Rules', icon: <Database className="w-4 h-4" />, roles: ['Owner', 'Manager'] },
    { id: 'security', label: 'Security PIN', icon: <ShieldCheck className="w-4 h-4" />, roles: ['Owner', 'Manager'] },
  ];

  // Adjust active tab if the logged-in user role is restricted from the default tab
  const allowedTabs = tabs.filter(t => !t.roles || t.roles.includes(user?.role || ''));
  const currentTab = allowedTabs.find(t => t.id === activeTab) ? activeTab : (allowedTabs[0]?.id || 'printers');

  return (
    <div className="p-8 bg-slate-50 min-h-screen">
      {/* Page Header */}
      <div className="flex items-center gap-3 mb-8">
        <div className="bg-indigo-100 p-2.5 rounded-xl text-indigo-600">
          <SettingsIcon className="w-6 h-6" />
        </div>
        <div>
          <h1 className="text-3xl font-black text-slate-800">System Configuration</h1>
          <p className="text-sm text-slate-500">Configure connection targets, devices, terminals, and user permissions</p>
        </div>
      </div>

      <div className="flex flex-col lg:flex-row gap-8">
        {/* Navigation Sidebar Panel */}
        <div className="w-full lg:w-64 flex flex-col gap-1.5 bg-white p-3 rounded-2xl border border-slate-100 shadow-sm h-fit">
          {allowedTabs.map(t => (
            <button
              key={t.id}
              onClick={() => setActiveTab(t.id)}
              className={`w-full px-4 py-3 text-left font-bold text-sm rounded-xl flex items-center gap-3 transition-all ${
                currentTab === t.id
                  ? 'bg-indigo-600 text-white shadow-sm'
                  : 'text-slate-500 hover:text-slate-800 hover:bg-slate-50'
              }`}
            >
              {t.icon}
              {t.label}
            </button>
          ))}
        </div>

        {/* Content Configuration Panels */}
        <div className="flex-1">
          {currentTab === 'connection' && <ServerIpConfig />}
          {currentTab === 'printers' && <PrinterConfig />}
          {currentTab === 'terminals' && <TerminalConfig />}
          {currentTab === 'users' && <UserManagement />}
          {currentTab === 'inventoryRules' && <InventorySafeguards />}
          {currentTab === 'security' && (
            <div className="bg-white p-6 rounded-xl border border-slate-100 shadow-sm max-w-xl">
              <h3 className="font-bold text-slate-800 mb-2">POS Security PIN</h3>
              <p className="text-xs text-slate-400 mb-6">Modify supervisor override approval PINs</p>
              <ChangeOverridePinPanel />
            </div>
          )}
        </div>
      </div>
    </div>
  );
};
