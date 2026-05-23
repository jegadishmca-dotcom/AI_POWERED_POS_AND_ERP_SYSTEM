import React, { useState } from 'react';
import { X, ShieldAlert, Loader2 } from 'lucide-react';
import { api } from '../../../../services/api';

export const ManagerPinModal = ({ isOpen, onClose, onSuccess, actionName }: any) => {
  const [pin, setPin]         = useState('');
  const [error, setError]     = useState('');
  const [loading, setLoading] = useState(false);

  if (!isOpen) return null;

  const handleVerify = async () => {
    if (!pin || pin.length < 4) {
      setError('Please enter a valid PIN.');
      return;
    }
    setError('');
    setLoading(true);
    try {
      const res = await api.post('/auth/verify-override-pin', { pin });
      if (res.data?.authorized) {
        setPin('');
        onSuccess();
      } else {
        setError('Invalid Manager PIN. Access denied.');
        setPin('');
      }
    } catch {
      setError('Unable to verify PIN. Please try again.');
      setPin('');
    } finally {
      setLoading(false);
    }
  };

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter') handleVerify();
  };

  return (
    <div className="fixed inset-0 bg-black/60 z-50 flex items-center justify-center p-4">
      <div className="bg-white rounded-xl shadow-2xl w-full max-w-sm overflow-hidden">
        <div className="bg-red-600 p-4 flex justify-between items-center text-white">
          <h2 className="text-xl font-bold flex items-center">
            <ShieldAlert className="mr-2" /> Manager Override
          </h2>
          <button onClick={onClose}><X /></button>
        </div>
        <div className="p-6 text-center">
          <p className="mb-4 text-gray-700">
            Enter Manager PIN to authorize <strong>{actionName}</strong>
          </p>
          <input
            type="password"
            value={pin}
            onChange={(e) => setPin(e.target.value.replace(/\D/g, '').slice(0, 8))}
            onKeyDown={handleKeyDown}
            className="w-full text-center text-3xl p-3 mb-2 border-2 border-gray-300 rounded focus:border-red-600 outline-none tracking-widest"
            placeholder="••••"
            autoFocus
            inputMode="numeric"
            maxLength={8}
          />
          {error && (
            <p className="text-red-600 text-sm mb-3 font-medium">{error}</p>
          )}
          <button
            onClick={handleVerify}
            disabled={loading || pin.length < 4}
            className="w-full py-3 bg-red-600 text-white font-bold rounded-lg hover:bg-red-700 transition disabled:opacity-50 disabled:cursor-not-allowed flex items-center justify-center gap-2"
          >
            {loading ? <><Loader2 className="w-5 h-5 animate-spin" /> VERIFYING...</> : 'AUTHORIZE'}
          </button>
        </div>
      </div>
    </div>
  );
};
