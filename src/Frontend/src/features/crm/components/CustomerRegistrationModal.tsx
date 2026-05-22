import React, { useState } from 'react';
import { UserPlus, Save, ShieldCheck } from 'lucide-react';

export const CustomerRegistrationModal = ({ isOpen, onClose, onRegister, initialPhone }: any) => {
  const [phone, setPhone] = useState('');
  const [name, setName] = useState('');
  const [tamilName, setTamilName] = useState('');
  const [dob, setDob] = useState('');
  const [marketingConsent, setMarketingConsent] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  React.useEffect(() => {
    if (isOpen) {
      setPhone(initialPhone || '');
      setName('');
      setTamilName('');
      setDob('');
      setMarketingConsent(false);
      setError(null);
    }
  }, [isOpen, initialPhone]);

  if (!isOpen) return null;

  const handleSave = async () => {
    if (!phone.trim() || !name.trim()) {
      setError("Mobile Number and Full Name are required.");
      return;
    }
    setError(null);
    setIsSubmitting(true);
    try {
      await onRegister({ phone, name, tamilName, dob: dob || undefined, marketingConsent });
      // Clear fields on success
      setPhone('');
      setName('');
      setTamilName('');
      setDob('');
      setMarketingConsent(false);
      onClose();
    } catch (err: any) {
      console.error('Registration error:', err);
      const msg = err.response?.data?.message || err.response?.data?.Message || err.message || "Failed to register customer.";
      setError(msg);
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="fixed inset-0 bg-black/60 z-[60] flex items-center justify-center p-4">
      <div className="bg-white rounded-xl shadow-2xl w-full max-w-lg overflow-hidden flex flex-col">
        <div className="bg-indigo-600 p-4 flex justify-between items-center text-white">
          <h2 className="text-xl font-bold flex items-center"><UserPlus className="mr-2" /> New Customer Registration</h2>
          <button onClick={onClose} className="font-bold text-xl">&times;</button>
        </div>
        
        <div className="p-6 space-y-4">
          {error && (
            <div className="bg-red-50 border-l-4 border-red-500 p-3 text-red-700 text-sm font-bold rounded">
              {error}
            </div>
          )}
          <div>
            <label className="block text-sm font-bold text-gray-700 mb-1">Mobile Number *</label>
            <input 
              type="tel" 
              className="w-full p-2 border rounded focus:border-indigo-600 outline-none font-bold text-lg" 
              placeholder="10-digit number"
              value={phone}
              onChange={e => setPhone(e.target.value)}
              autoFocus
            />
          </div>
          <div className="grid grid-cols-2 gap-4">
            <div>
              <label className="block text-sm font-bold text-gray-700 mb-1">Full Name *</label>
              <input 
                type="text" 
                className="w-full p-2 border rounded focus:border-indigo-600 outline-none" 
                value={name}
                onChange={e => setName(e.target.value)}
              />
            </div>
            <div>
              <label className="block text-sm font-bold text-gray-700 mb-1">Tamil Name</label>
              <input 
                type="text" 
                className="w-full p-2 border rounded focus:border-indigo-600 outline-none" 
                value={tamilName}
                onChange={e => setTamilName(e.target.value)}
              />
            </div>
          </div>
          <div>
            <label className="block text-sm font-bold text-gray-700 mb-1">Date of Birth (For Offers)</label>
            <input 
              type="date" 
              className="w-full p-2 border rounded outline-none" 
              value={dob}
              onChange={e => setDob(e.target.value)}
            />
          </div>
          
          <div className="bg-slate-50 p-3 rounded border flex items-start gap-3">
            <input 
              type="checkbox" 
              id="dpdp" 
              className="mt-1 w-5 h-5"
              checked={marketingConsent}
              onChange={e => setMarketingConsent(e.target.checked)}
            />
            <label htmlFor="dpdp" className="text-sm text-gray-700">
              <strong className="flex items-center text-slate-800"><ShieldCheck className="w-4 h-4 mr-1 text-emerald-600" /> DPDP Consent</strong>
              I agree to receive promotional offers and understand my data is stored securely.
            </label>
          </div>
        </div>

        <div className="p-4 bg-gray-50 border-t flex justify-end">
          <button 
            onClick={handleSave}
            disabled={isSubmitting}
            className={`px-6 py-2 text-white rounded font-bold shadow flex items-center ${isSubmitting ? 'bg-indigo-400 cursor-not-allowed' : 'bg-indigo-600 hover:bg-indigo-700'}`}
          >
            <Save className="w-5 h-5 mr-2" /> {isSubmitting ? 'Registering...' : 'Register Customer'}
          </button>
        </div>
      </div>
    </div>
  );
};
