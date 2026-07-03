import React, { useState, useEffect } from 'react';
import { Mail, Server, ShieldCheck, Key, RefreshCw, Save, Send, AlertTriangle, Eye, EyeOff, Clock, Globe } from 'lucide-react';
import { api } from '../../../utils/api';
import { useAuthStore } from '../../auth/store/auth.store';

export const EmailConfig: React.FC = () => {
  const [deliveryMethod, setDeliveryMethod] = useState('POSTMARK');
  
  // SMTP Fields
  const [smtpServer, setSmtpServer] = useState('smtp.gmail.com');
  const [smtpPort, setSmtpPort] = useState(587);
  const [senderPassword, setSenderPassword] = useState('');
  const [enableSsl, setEnableSsl] = useState(true);

  // Mailgun Fields
  const [mailgunDomain, setMailgunDomain] = useState('');
  const [mailgunApiKey, setMailgunApiKey] = useState('');

  // Postmark Fields
  const [postmarkToken, setPostmarkToken] = useState('');

  // Resend Fields
  const [resendApiKey, setResendApiKey] = useState('');

  // Shared Fields
  const [senderEmail, setSenderEmail] = useState('');
  const [recipientEmail, setRecipientEmail] = useState('');
  const [developerAlertEmail, setDeveloperAlertEmail] = useState('');
  const [triggerIntervalMinutes, setTriggerIntervalMinutes] = useState(0);

  // UI States
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [testing, setTesting] = useState(false);
  const [triggeringSales, setTriggeringSales] = useState(false);
  const [successMsg, setSuccessMsg] = useState<string | null>(null);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);
  
  const [showPassword, setShowPassword] = useState(false);
  const [showMgKey, setShowMgKey] = useState(false);
  const [showPmToken, setShowPmToken] = useState(false);
  const [showRsKey, setShowRsKey] = useState(false);

  const { user } = useAuthStore();
  const isSuperAdmin = user?.username?.toLowerCase() === 'admin@supermarket.local';

  const fetchEmailSettings = async () => {
    try {
      setLoading(true);
      setErrorMsg(null);
      const res = await api.get('/api/settings/email');
      
      setDeliveryMethod(res.data.deliveryMethod || 'POSTMARK');
      
      setSmtpServer(res.data.smtpServer || 'smtp.gmail.com');
      setSmtpPort(res.data.smtpPort || 587);
      setSenderEmail(res.data.senderEmail || '');
      setSenderPassword(res.data.senderPassword || '');
      
      setRecipientEmail(res.data.recipientEmail || 'jegadishmca@gmail.com');
      setDeveloperAlertEmail(res.data.developerAlertEmail || '');
      setEnableSsl(res.data.enableSsl !== false);
      setTriggerIntervalMinutes(res.data.triggerIntervalMinutes || 0);

      setMailgunDomain(res.data.mailgunDomain || '');
      setMailgunApiKey(res.data.mailgunApiKey || '');
      setPostmarkToken(res.data.postmarkToken || '');
      setResendApiKey(res.data.resendApiKey || '');
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
        deliveryMethod,
        smtpServer,
        smtpPort: Number(smtpPort),
        senderEmail,
        senderPassword,
        recipientEmail,
        developerAlertEmail,
        enableSsl,
        triggerIntervalMinutes: Number(triggerIntervalMinutes),
        mailgunDomain,
        mailgunApiKey,
        postmarkToken,
        resendApiKey
      };

      await api.post('/api/settings/email', payload);
      setSuccessMsg('Email configuration updated successfully.');
      setTimeout(() => setSuccessMsg(null), 5000);
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
        deliveryMethod,
        smtpServer,
        smtpPort: Number(smtpPort),
        senderEmail,
        senderPassword,
        recipientEmail,
        developerAlertEmail,
        enableSsl,
        triggerIntervalMinutes: Number(triggerIntervalMinutes),
        mailgunDomain,
        mailgunApiKey,
        postmarkToken,
        resendApiKey
      };

      const res = await api.post('/api/settings/email/test', payload);
      if (res.data.success) {
        setSuccessMsg(res.data.message || 'Test email sent successfully! Please check your inbox.');
      } else {
        setErrorMsg('Failed to send test email. Server responded with error.');
      }
    } catch (err: any) {
      console.error('Failed to send test email', err);
      const msg = err.response?.data?.message || 'Email Connection test failed. Check settings and API keys.';
      setErrorMsg(msg);
    } finally {
      setTesting(false);
    }
  };

  const handleTriggerSales = async () => {
    try {
      setTriggeringSales(true);
      setSuccessMsg(null);
      setErrorMsg(null);

      const res = await api.post('/api/aiautomation/trigger-daily-email');
      if (res.data.success) {
        setSuccessMsg(res.data.message || "Today's sales report email triggered successfully.");
      } else {
        setErrorMsg("Failed to trigger sales report. Server responded with error.");
      }
    } catch (err: any) {
      console.error("Failed to trigger sales email", err);
      const msg = err.response?.data?.message || "Failed to trigger sales report email.";
      setErrorMsg(msg);
    } finally {
      setTriggeringSales(false);
    }
  };

  return (
    <div className="bg-white p-8 rounded-2xl border border-slate-100 shadow-lg max-w-2xl">
      <div className="flex items-center gap-3 mb-6 pb-4 border-b border-slate-100">
        <Mail className="w-7 h-7 text-indigo-600" />
        <div>
          <h3 className="text-xl font-black text-slate-800">Email Reports Configuration</h3>
          <p className="text-xs text-slate-500 font-medium font-sans">Configure SMTP credentials or transactional email APIs to automatically send daily reports</p>
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

          {/* Delivery Method Selector */}
          <div className="space-y-1">
            <label className="text-xs font-black text-slate-500 uppercase">
              Email Delivery Method
            </label>
            <select
              value={deliveryMethod}
              onChange={(e) => setDeliveryMethod(e.target.value)}
              className="w-full px-4 py-2 border border-slate-200 rounded-xl text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 bg-white"
            >
              <option value="POSTMARK">Postmark HTTP API (Requires Work/Custom Domain Email)</option>
              <option value="RESEND">Resend HTTP API (Recommended: Free & supports Gmail sign-up)</option>
              <option value="MAILGUN">Mailgun HTTP API (Alternative HTTP API)</option>
              <option value="SMTP">SMTP (Standard SMTP Server - Timed out on Free Render Tier)</option>
            </select>
            <p className="text-[10px] text-slate-400 font-medium font-sans">
              Note: SMTP port 587 is blocked by default on Render's free tier. Use Resend or Postmark HTTP APIs to bypass.
            </p>
          </div>

          <div className="border-t border-slate-100 pt-4 space-y-4">
            {/* Conditional Input Rendering */}
            {deliveryMethod === 'SMTP' && (
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
                    disabled={!isSuperAdmin}
                    className="w-full px-4 py-2 border border-slate-200 rounded-xl text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 disabled:opacity-50 disabled:bg-slate-50"
                  />
                </div>

                {/* Sender Password */}
                {isSuperAdmin && (
                  <div className="space-y-1">
                    <label className="text-xs font-black text-slate-500 uppercase flex items-center gap-1.5">
                      <Key className="w-3.5 h-3.5" /> SMTP Password / App Key
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
                )}
              </div>
            )}

            {deliveryMethod === 'MAILGUN' && (
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                {/* Mailgun Domain */}
                <div className="space-y-1">
                  <label className="text-xs font-black text-slate-500 uppercase flex items-center gap-1.5">
                    <Globe className="w-3.5 h-3.5" /> Mailgun Domain
                  </label>
                  <input
                    type="text"
                    placeholder="e.g. mg.yourdomain.com"
                    value={mailgunDomain}
                    onChange={(e) => setMailgunDomain(e.target.value)}
                    className="w-full px-4 py-2 border border-slate-200 rounded-xl text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
                  />
                </div>

                {/* Mailgun Api Key */}
                {isSuperAdmin && (
                  <div className="space-y-1">
                    <label className="text-xs font-black text-slate-500 uppercase flex items-center gap-1.5">
                      <Key className="w-3.5 h-3.5" /> Mailgun API Key
                    </label>
                    <div className="relative">
                      <input
                        type={showMgKey ? 'text' : 'password'}
                        placeholder="e.g. key-xxxxxxxxxxxx"
                        value={mailgunApiKey}
                        onChange={(e) => setMailgunApiKey(e.target.value)}
                        className="w-full px-4 py-2 border border-slate-200 rounded-xl text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 pr-10"
                      />
                      <button
                        type="button"
                        onClick={() => setShowMgKey(!showMgKey)}
                        className="absolute inset-y-0 right-0 pr-3 flex items-center text-slate-400 hover:text-slate-600"
                      >
                        {showMgKey ? <EyeOff className="w-4 h-4" /> : <Eye className="w-4 h-4" />}
                      </button>
                    </div>
                  </div>
                )}

                {/* Sender Email */}
                <div className="space-y-1 md:col-span-2">
                  <label className="text-xs font-black text-slate-500 uppercase">
                    Sender Email Signature
                  </label>
                  <input
                    type="email"
                    placeholder="e.g. postmaster@mg.yourdomain.com"
                    value={senderEmail}
                    onChange={(e) => setSenderEmail(e.target.value)}
                    disabled={!isSuperAdmin}
                    className="w-full px-4 py-2 border border-slate-200 rounded-xl text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 disabled:opacity-50 disabled:bg-slate-50"
                  />
                </div>
              </div>
            )}

            {deliveryMethod === 'POSTMARK' && (
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                {/* Postmark Token */}
                {isSuperAdmin && (
                  <div className="space-y-1 md:col-span-2">
                    <label className="text-xs font-black text-slate-500 uppercase flex items-center gap-1.5">
                      <Key className="w-3.5 h-3.5" /> Postmark Server Token
                    </label>
                    <div className="relative">
                      <input
                        type={showPmToken ? 'text' : 'password'}
                        placeholder="e.g. xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
                        value={postmarkToken}
                        onChange={(e) => setPostmarkToken(e.target.value)}
                        className="w-full px-4 py-2 border border-slate-200 rounded-xl text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 pr-10"
                      />
                      <button
                        type="button"
                        onClick={() => setShowPmToken(!showPmToken)}
                        className="absolute inset-y-0 right-0 pr-3 flex items-center text-slate-400 hover:text-slate-600"
                      >
                        {showPmToken ? <EyeOff className="w-4 h-4" /> : <Eye className="w-4 h-4" />}
                      </button>
                    </div>
                  </div>
                )}

                {/* Sender Email */}
                <div className="space-y-1 md:col-span-2">
                  <label className="text-xs font-black text-slate-500 uppercase">
                    Sender Email Signature (Must be verified in Postmark)
                  </label>
                  <input
                    type="email"
                    placeholder="e.g. postmaster@yourdomain.com"
                    value={senderEmail}
                    onChange={(e) => setSenderEmail(e.target.value)}
                    disabled={!isSuperAdmin}
                    className="w-full px-4 py-2 border border-slate-200 rounded-xl text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 disabled:opacity-50 disabled:bg-slate-50"
                  />
                  <p className="text-[10px] text-slate-400">
                    Postmark requires the Sender Email to match a verified single sender signature or domain signature in your Postmark account.
                  </p>
                </div>
              </div>
            )}

            {deliveryMethod === 'RESEND' && (
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                {/* Resend API Key */}
                {isSuperAdmin && (
                  <div className="space-y-1 md:col-span-2">
                    <label className="text-xs font-black text-slate-500 uppercase flex items-center gap-1.5">
                      <Key className="w-3.5 h-3.5" /> Resend API Key
                    </label>
                    <div className="relative">
                      <input
                        type={showRsKey ? 'text' : 'password'}
                        placeholder="e.g. re_xxxxxxxxxxxx"
                        value={resendApiKey}
                        onChange={(e) => setResendApiKey(e.target.value)}
                        className="w-full px-4 py-2 border border-slate-200 rounded-xl text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 pr-10"
                      />
                      <button
                        type="button"
                        onClick={() => setShowRsKey(!showRsKey)}
                        className="absolute inset-y-0 right-0 pr-3 flex items-center text-slate-400 hover:text-slate-600"
                      >
                        {showRsKey ? <EyeOff className="w-4 h-4" /> : <Eye className="w-4 h-4" />}
                      </button>
                    </div>
                  </div>
                )}

                {/* Sender Email */}
                <div className="space-y-1 md:col-span-2">
                  <label className="text-xs font-black text-slate-500 uppercase">
                    Sender Email (Use "onboarding@resend.dev" for free tier testing)
                  </label>
                  <input
                    type="text"
                    placeholder="onboarding@resend.dev"
                    value={senderEmail}
                    onChange={(e) => setSenderEmail(e.target.value)}
                    disabled={!isSuperAdmin}
                    className="w-full px-4 py-2 border border-slate-200 rounded-xl text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 disabled:opacity-50 disabled:bg-slate-50"
                  />
                  <p className="text-[10px] text-slate-400 font-sans">
                    Resend allows sending from <strong>onboarding@resend.dev</strong> to your own verified login email account. Once you add your custom domain to Resend, you can send from any address.
                  </p>
                </div>
              </div>
            )}

            {/* Recipient Email */}
            <div className="space-y-1">
              <label className="text-xs font-black text-slate-500 uppercase">
                Owner Recipient Email
              </label>
              <input
                type="text"
                placeholder="e.g. owner1@gmail.com, owner2@gmail.com"
                value={recipientEmail}
                onChange={(e) => setRecipientEmail(e.target.value)}
                className="w-full px-4 py-2 border border-slate-200 rounded-xl text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
              />
              <p className="text-[10px] text-slate-400 font-medium">
                This is the target inbox where daily reports and system alerts are sent. Separate multiple emails with a comma.
              </p>
            </div>
 
            {/* Developer Alert Email */}
            <div className="space-y-1">
              <label className="text-xs font-black text-slate-500 uppercase">
                Developer Alert Email
              </label>
              <input
                type="text"
                placeholder="e.g. dev-alerts@supermarket.local"
                value={developerAlertEmail}
                onChange={(e) => setDeveloperAlertEmail(e.target.value)}
                className="w-full px-4 py-2 border border-slate-200 rounded-xl text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
              />
              <p className="text-[10px] text-slate-400 font-medium">
                This is the target inbox where critical developer system notifications, environment switch requests, and audit alarms are sent.
              </p>
            </div>

            {/* Trigger Interval (Minutes) */}
            <div className="space-y-1">
              <label className="text-xs font-black text-slate-500 uppercase flex items-center gap-1.5">
                <Clock className="w-3.5 h-3.5" /> Trigger Interval (Minutes)
              </label>
              <input
                type="number"
                placeholder="e.g. 0 for EOD, 120 for every 2 hours"
                value={triggerIntervalMinutes}
                onChange={(e) => setTriggerIntervalMinutes(Math.max(0, Number(e.target.value)))}
                className="w-full px-4 py-2 border border-slate-200 rounded-xl text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
              />
              <p className="text-[10px] text-slate-400 font-medium">
                Set to 0 to only trigger once at End-Of-Day (11:59 PM IST). Set to a value greater than 0 (e.g., 120) to automatically trigger report emails at that interval in minutes.
              </p>
            </div>

            {/* SSL/TLS Toggle (Only for SMTP) */}
            {deliveryMethod === 'SMTP' && (
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
                  className={`w-12 h-6 rounded-full shrink-0 relative transition-colors ${enableSsl ? 'bg-indigo-600' : 'bg-slate-200'}`}
                >
                  <div className={`w-4 h-4 bg-white rounded-full absolute top-1 transition-all ${enableSsl ? 'right-1' : 'left-1'}`} />
                </button>
              </div>
            )}

            {/* Action buttons */}
            <div className="flex flex-wrap gap-3 justify-between items-center pt-4 border-t border-slate-100">
              <div className="flex flex-wrap gap-3">
                {/* Test Email */}
                <button
                  onClick={handleTestEmail}
                  disabled={testing || saving || triggeringSales}
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

                {/* Trigger Today's Sales */}
                <button
                  onClick={handleTriggerSales}
                  disabled={testing || saving || triggeringSales}
                  className="px-4 py-2.5 bg-emerald-50 hover:bg-emerald-100 border border-emerald-250 disabled:opacity-50 text-emerald-700 font-bold rounded-xl text-sm flex items-center transition"
                >
                  {triggeringSales ? (
                    <>
                      <RefreshCw className="w-4 h-4 mr-1.5 animate-spin" /> Triggering...
                    </>
                  ) : (
                    <>
                      <Send className="w-4 h-4 mr-1.5 text-emerald-600" /> Trigger Today's Sales
                    </>
                  )}
                </button>
              </div>

              {/* Save Settings */}
              <button
                onClick={handleSave}
                disabled={saving || testing || triggeringSales}
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
        </div>
      )}
    </div>
  );
};
