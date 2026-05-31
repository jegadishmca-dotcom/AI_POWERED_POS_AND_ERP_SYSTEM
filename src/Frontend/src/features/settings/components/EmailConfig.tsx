import React, { useState, useEffect } from 'react';
import { Mail, Server, ShieldCheck, Key, RefreshCw, Save, Send, AlertTriangle, Eye, EyeOff } from 'lucide-react';
import { api } from '../../../utils/api';

export const EmailConfig: React.FC = () => {
  const [smtpServer, setSmtpServer] = useState('smtp.gmail.com');
  const [smtpPort, setSmtpPort] = useState(587);
  const [senderEmail, setSenderEmail] = useState('');
  const [senderPassword, setSenderPassword] = useState('');
  const [recipientEmail, setRecipientEmail] = useState('');
  const [enableSsl, setEnableSsl] = useState(true);

  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [testing, setTesting] = useState(false);
  const [successMsg, setSuccessMsg] = useState<string | null>(null);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);
  const [showPassword, setShowPassword] = useState(false);

  const fetchEmailSettings = async () => {
    try {
      setLoading(true);
      setErrorMsg(null);
      const res = await api.get('/api/settings/email');
      setSmtpServer(res.data.smtpServer || 'smtp.gmail.com');
      setSmtpPort(res.data.smtpPort || 587);
      setSenderEmail(res.data.senderEmail || '');
      setSenderPassword(res.data.senderPassword || '');
      setRecipientEmail(res.data.recipientEmail || 'jegadishmca@gmail.com');
      setEnableSsl(res.data.enableSsl !== false);
    } catch (err: any) {
      console.error('Failed to load email configuration settings', err);
      setErrorMsg('Failed to load email settings configuration from server.');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchEmailSettings();
  }, []);

  const handleSave = async () => {
    try {
      setSaving(true);
      setSuccessMsg(null);
      setErrorMsg(null);

      const payload = {
        smtpServer,
        smtpPort: Number(smtpPort),
        senderEmail,
        senderPassword,
        recipientEmail,
        enableSsl
      };

      await api.post('/api/settings/email', payload);
      setSuccessMsg('Email configuration updated successfully.');
      setTimeout(() => setSuccessMsg(null), 5000);
      // Reload values to get updated masked password
      fetchEmailSettings();
    } catch (err: any) {
      console.error('Failed to update email settings', err);
      setErrorMsg('Failed to save email settings. Please check fields and retry.');
    } finally {
      setSaving(false);
    }
  };

  const handleTestEmail = async () => {
    try {
      setTesting(true);
      setSuccessMsg(null);
      setErrorMsg(null);

      const payload = {
        smtpServer,
        smtpPort: Number(smtpPort),
        senderEmail,
        senderPassword,
        recipientEmail,
        enableSsl
      };

      const res = await api.post('/api/settings/email/test', payload);
      if (res.data.success) {
        setSuccessMsg(res.data.message || 'Test email sent successfully! Please check your inbox.');
      } else {
        setErrorMsg('Failed to send test email. Server responded with error.');
      }
    } catch (err: any) {
      console.error('Failed to send test email', err);
      const msg = err.response?.data?.message || 'SMTP Connection test failed. Check settings and credentials.';
      setErrorMsg(msg);
    } finally {
      setTesting(false);
    }
  };

  return (
    <div className="bg-white p-8 rounded-2xl border border-slate-100 shadow-lg max-w-2xl">
      <div className="flex items-center gap-3 mb-6 pb-4 border-b border-slate-100">
        <Mail className="w-7 h-7 text-indigo-600" />
        <div>
          <h3 className="text-xl font-black text-slate-800">Email Reports Configuration</h3>
          <p className="text-xs text-slate-500 font-medium">Configure SMTP credentials to automatically email EOD reports to the store owner</p>
        </div>
      </div>

      {loading ? (
        <div className="py-12 flex flex-col items-center justify-center space-y-2">
          <RefreshCw className="w-8 h-8 text-indigo-600 animate-spin" />
          <span className="text-xs text-slate-500 font-semibold">Retrieving email setup...</span>
        </div>
      ) : (
        <div className="space-y-5">
          {errorMsg && (
            <div className="p-3 bg-red-50 text-red-700 rounded-xl text-xs font-bold border border-red-100 flex items-start space-x-2">
              <AlertTriangle className="w-4 h-4 text-red-500 shrink-0 mt-0.5" />
              <span>{errorMsg}</span>
            </div>
          )}

          {successMsg && (
            <div className="p-3 bg-green-50 text-green-700 rounded-xl text-xs font-bold border border-green-100 flex items-start space-x-2">
              <ShieldCheck className="w-4 h-4 text-green-500 shrink-0 mt-0.5" />
              <span>{successMsg}</span>
            </div>
          )}

          {/* Form Fields Grid */}
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            {/* SMTP Server */}
            <div className="space-y-1">
              <label className="text-xs font-black text-slate-500 uppercase flex items-center gap-1.5">
                <Server className="w-3.5 h-3.5" /> SMTP Server Host
              </label>
              <input
                type="text"
                placeholder="e.g. smtp.gmail.com"
                value={smtpServer}
                onChange={(e) => setSmtpServer(e.target.value)}
                className="w-full px-4 py-2 border border-slate-200 rounded-xl text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
              />
            </div>

            {/* SMTP Port */}
            <div className="space-y-1">
              <label className="text-xs font-black text-slate-500 uppercase">
                SMTP Port
              </label>
              <input
                type="number"
                placeholder="e.g. 587"
                value={smtpPort}
                onChange={(e) => setSmtpPort(Number(e.target.value))}
                className="w-full px-4 py-2 border border-slate-200 rounded-xl text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
              />
            </div>

            {/* Sender Email */}
            <div className="space-y-1">
              <label className="text-xs font-black text-slate-500 uppercase">
                Sender Email Account
              </label>
              <input
                type="email"
                placeholder="e.g. supermarket@gmail.com"
                value={senderEmail}
                onChange={(e) => setSenderEmail(e.target.value)}
                className="w-full px-4 py-2 border border-slate-200 rounded-xl text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
              />
            </div>

            {/* Sender Password */}
            <div className="space-y-1">
              <label className="text-xs font-black text-slate-500 uppercase flex items-center gap-1.5">
                <Key className="w-3.5 h-3.5" /> Sender Password / App Key
              </label>
              <div className="relative">
                <input
                  type={showPassword ? 'text' : 'password'}
                  placeholder="e.g. xxxx xxxx xxxx xxxx"
                  value={senderPassword}
                  onChange={(e) => setSenderPassword(e.target.value)}
                  className="w-full px-4 py-2 border border-slate-200 rounded-xl text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 pr-10"
                />
                <button
                  type="button"
                  onClick={() => setShowPassword(!showPassword)}
                  className="absolute inset-y-0 right-0 pr-3 flex items-center text-slate-400 hover:text-slate-600"
                >
                  {showPassword ? <EyeOff className="w-4 h-4" /> : <Eye className="w-4 h-4" />}
                </button>
              </div>
            </div>
          </div>

          {/* Recipient Email */}
          <div className="space-y-1">
            <label className="text-xs font-black text-slate-500 uppercase">
              Owner Recipient Email
            </label>
            <input
              type="email"
              placeholder="e.g. owner@gmail.com"
              value={recipientEmail}
              onChange={(e) => setRecipientEmail(e.target.value)}
              className="w-full px-4 py-2 border border-slate-200 rounded-xl text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
            />
            <p className="text-[10px] text-slate-400 font-medium">
              This is the target inbox where daily reports and system alerts are sent.
            </p>
          </div>

          {/* SSL/TLS Toggle */}
          <div className="flex items-start justify-between p-4 rounded-xl hover:bg-slate-50 transition border border-slate-50">
            <div className="space-y-1 pr-6">
              <div className="text-sm font-bold text-slate-800">
                Enable SSL/TLS Encryption
              </div>
              <p className="text-xs text-slate-500 font-medium font-sans">
                Highly recommended for most SMTP providers, including Gmail (port 587 uses STARTTLS).
              </p>
            </div>
            <button
              onClick={() => setEnableSsl(!enableSsl)}
              className={`w-12 h-6 rounded-full shrink-0 relative transition-colors ${enableSsl ? 'bg-indigo-600' : 'bg-slate-350'}`}
            >
              <div className={`w-4 h-4 bg-white rounded-full absolute top-1 transition-all ${enableSsl ? 'right-1' : 'left-1'}`} />
            </button>
          </div>

          {/* Action buttons */}
          <div className="flex justify-between items-center pt-4 border-t border-slate-100">
            {/* Test Email */}
            <button
              onClick={handleTestEmail}
              disabled={testing || saving}
              className="px-4 py-2.5 bg-slate-100 hover:bg-slate-200 disabled:opacity-50 text-slate-700 font-bold rounded-xl text-sm flex items-center transition"
            >
              {testing ? (
                <>
                  <RefreshCw className="w-4 h-4 mr-1.5 animate-spin" /> Sending Test...
                </>
              ) : (
                <>
                  <Send className="w-4 h-4 mr-1.5" /> Test Configuration
                </>
              )}
            </button>

            {/* Save Settings */}
            <button
              onClick={handleSave}
              disabled={saving || testing}
              className="px-5 py-2.5 bg-indigo-600 hover:bg-indigo-700 disabled:opacity-50 text-white font-bold rounded-xl text-sm flex items-center shadow-md transition"
            >
              {saving ? (
                <>
                  <RefreshCw className="w-4 h-4 mr-1.5 animate-spin" /> Saving Setup...
                </>
              ) : (
                <>
                  <Save className="w-4 h-4 mr-1.5" /> Save Configuration
                </>
              )}
            </button>
          </div>
        </div>
      )}
    </div>
  );
};
