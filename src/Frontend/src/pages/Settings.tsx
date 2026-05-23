import React from 'react';
import { ChangeOverridePinPanel } from '../features/settings/components/ChangeOverridePinPanel';
import { Settings as SettingsIcon, ShieldCheck } from 'lucide-react';
import { useAuthStore } from '../features/auth/store/auth.store';

export const Settings: React.FC = () => {
  const { user } = useAuthStore();
  const isOwner = user?.role === 'Owner' || user?.role === 'Manager';

  return (
    <div className="p-6 max-w-4xl">
      {/* Page Header */}
      <div className="flex items-center gap-3 mb-8">
        <div className="bg-indigo-100 p-2.5 rounded-xl">
          <SettingsIcon className="w-6 h-6 text-indigo-600" />
        </div>
        <div>
          <h1 className="text-2xl font-extrabold text-slate-800">Settings</h1>
          <p className="text-sm text-slate-500">System configuration &amp; security options</p>
        </div>
      </div>

      <div className="grid grid-cols-1 gap-6">
        {/* Override PIN Section — Owner/Manager only */}
        {isOwner && (
          <section>
            <h2 className="text-lg font-bold text-slate-700 mb-3 flex items-center gap-2">
              <ShieldCheck className="w-5 h-5 text-red-500" />
              POS Security
            </h2>
            <ChangeOverridePinPanel />
          </section>
        )}

        {!isOwner && (
          <div className="text-center py-16 text-slate-400">
            <SettingsIcon className="w-12 h-12 mx-auto mb-3 opacity-30" />
            <p>No settings available for your role.</p>
          </div>
        )}
      </div>
    </div>
  );
};
