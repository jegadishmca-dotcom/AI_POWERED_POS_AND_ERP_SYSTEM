import React, { useState } from 'react';
import { X, ShieldAlert } from 'lucide-react';

export const ManagerPinModal = ({ isOpen, onClose, onSuccess, actionName }: any) => {
  const [pin, setPin] = useState('');

  if (!isOpen) return null;

  const handleVerify = () => {
    // Basic verification - should ideally check hashed pin or auth service
    if (pin === '1234') { // placeholder for manager pin
      setPin('');
      onSuccess();
    } else {
      alert("Invalid Manager PIN");
      setPin('');
    }
  };

  return (
    <div className="fixed inset-0 bg-black/60 z-50 flex items-center justify-center p-4">
      <div className="bg-white rounded-xl shadow-2xl w-full max-w-sm overflow-hidden">
        <div className="bg-red-600 p-4 flex justify-between items-center text-white">
          <h2 className="text-xl font-bold flex items-center"><ShieldAlert className="mr-2" /> Manager Override</h2>
          <button onClick={onClose}><X /></button>
        </div>
        <div className="p-6 text-center">
          <p className="mb-4 text-gray-700">Enter Manager PIN to authorize <strong>{actionName}</strong></p>
          <input 
            type="password" 
            value={pin}
            onChange={(e) => setPin(e.target.value)}
            className="w-full text-center text-3xl p-3 mb-4 border-2 border-gray-300 rounded focus:border-red-600 outline-none"
            placeholder="****"
            maxLength={4}
            autoFocus
          />
          <button 
            onClick={handleVerify}
            className="w-full py-3 bg-red-600 text-white font-bold rounded-lg hover:bg-red-700 transition"
          >
            AUTHORIZE
          </button>
        </div>
      </div>
    </div>
  );
};
