import React, { useState } from 'react';
import { Network, CheckCircle2, XCircle, Loader2 } from 'lucide-react';
import axios from 'axios';

export const ServerIpConfig: React.FC = () => {
  const [serverIp, setServerIp] = useState<string>(
    localStorage.getItem('pos_server_ip') || ''
  );
  const [testing, setTesting] = useState(false);
  const [testResult, setTestResult] = useState<'success' | 'failed' | null>(null);

  const handleSave = () => {
    const trimmed = serverIp.trim();
    if (trimmed === '') {
      localStorage.removeItem('pos_server_ip');
      alert('Configured Server IP cleared. System will use default server URL.');
    } else {
      localStorage.setItem('pos_server_ip', trimmed);
      alert(`Server IP saved successfully as: ${trimmed}`);
    }
    window.location.reload();
  };

  const handleTestConnection = async () => {
    setTesting(true);
    setTestResult(null);
    try {
      const target = serverIp.trim();
      const baseUrl = target
        ? (target.startsWith('http') ? target : `http://${target}`)
        : window.location.origin;
      
      const res = await axios.get(`${baseUrl}/health`, { timeout: 4000 });
      if (res.status === 200 && res.data === 'Healthy') {
        setTestResult('success');
      } else {
        setTestResult('failed');
      }
    } catch (err) {
      console.error('Connection test failed:', err);
      setTestResult('failed');
    } finally {
      setTesting(false);
    }
  };

  return (
    <div className="bg-white p-6 rounded-xl border border-slate-100 shadow-sm max-w-xl">
      <div className="flex items-center gap-3 mb-4">
        <div className="p-2 bg-indigo-50 text-indigo-600 rounded-lg">
          <Network className="w-5 h-5" />
        </div>
        <div>
          <h3 className="font-bold text-slate-800">Server Connection Setup</h3>
          <p className="text-xs text-slate-400">Configure central database &amp; API server endpoint IP</p>
        </div>
      </div>

      <div className="space-y-4">
        <div>
          <label className="block text-xs font-bold text-slate-500 uppercase mb-2">
            Server IP Address / Domain
          </label>
          <div className="flex gap-2">
            <input
              type="text"
              placeholder="e.g. 192.168.29.64:8000"
              value={serverIp}
              onChange={(e) => setServerIp(e.target.value)}
              className="flex-1 px-4 py-2.5 border rounded-xl text-sm text-slate-700 bg-slate-50 focus:outline-none focus:ring-2 focus:ring-indigo-500"
            />
            <button
              onClick={handleTestConnection}
              disabled={testing}
              className="px-4 py-2.5 bg-slate-100 hover:bg-slate-200 text-slate-700 font-bold text-sm rounded-xl transition flex items-center gap-1.5"
            >
              {testing ? (
                <>
                  <Loader2 className="w-4 h-4 animate-spin text-slate-500" />
                  Testing...
                </>
              ) : (
                'Test Ping'
              )}
            </button>
          </div>
          <p className="text-xs text-slate-400 mt-2">
            Leave blank to connect directly to the current host origin.
          </p>
        </div>

        {testResult === 'success' && (
          <div className="p-3 bg-emerald-50 border border-emerald-200 text-emerald-800 rounded-xl text-xs font-bold flex items-center gap-2">
            <CheckCircle2 className="w-4 h-4 text-emerald-600" />
            Connection test successful! Backend server is online and healthy.
          </div>
        )}

        {testResult === 'failed' && (
          <div className="p-3 bg-rose-50 border border-rose-200 text-rose-800 rounded-xl text-xs font-bold flex items-center gap-2">
            <XCircle className="w-4 h-4 text-rose-600" />
            Connection test failed. Please verify the IP address and check server power/network.
          </div>
        )}

        <div className="pt-2">
          <button
            onClick={handleSave}
            className="px-6 py-2.5 bg-indigo-600 hover:bg-indigo-700 text-white font-bold text-sm rounded-xl transition shadow-sm"
          >
            Save &amp; Reload App
          </button>
        </div>
      </div>
    </div>
  );
};
