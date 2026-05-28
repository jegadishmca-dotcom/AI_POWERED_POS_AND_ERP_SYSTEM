import React, { useState, useEffect } from 'react';
import { Printer, Settings2, CheckCircle2, AlertCircle } from 'lucide-react';

interface PrinterSettings {
  receiptMode: 'usb' | 'network' | 'system';
  receiptIp: string;
  receiptBaudRate: number;
  barcodeMode: 'usb' | 'system';
  barcodeBaudRate: number;
}

export const PrinterConfig: React.FC = () => {
  const [settings, setSettings] = useState<PrinterSettings>({
    receiptMode: 'system',
    receiptIp: '',
    receiptBaudRate: 9600,
    barcodeMode: 'system',
    barcodeBaudRate: 9600,
  });

  const [message, setMessage] = useState<{ text: string; type: 'success' | 'error' } | null>(null);

  useEffect(() => {
    const saved = localStorage.getItem('pos_printer_config');
    if (saved) {
      try {
        setSettings(JSON.parse(saved));
      } catch (e) {
        console.error('Failed to parse printer config:', e);
      }
    }
  }, []);

  const handleSave = () => {
    localStorage.setItem('pos_printer_config', JSON.stringify(settings));
    setMessage({ text: 'Printer configurations saved successfully.', type: 'success' });
    setTimeout(() => setMessage(null), 3000);
  };

  const testReceiptPrint = async () => {
    if (settings.receiptMode === 'system') {
      window.print();
      return;
    }

    if (settings.receiptMode === 'network') {
      setMessage({ text: `Sending test payload to network printer at ${settings.receiptIp}:9100...`, type: 'success' });
      // Simulate/trigger network mock print test
      setTimeout(() => setMessage(null), 3000);
      return;
    }

    if (settings.receiptMode === 'usb') {
      if (!('serial' in navigator)) {
        setMessage({ text: 'Web Serial API is not supported in this browser. Please use Chrome.', type: 'error' });
        return;
      }
      try {
        // @ts-ignore
        const port = await navigator.serial.requestPort();
        await port.open({ baudRate: settings.receiptBaudRate });
        const writer = port.writable.getWriter();
        const encoder = new TextEncoder();
        
        // ESC/POS test commands
        await writer.write(new Uint8Array([0x1B, 0x40])); // Init
        await writer.write(encoder.encode("   USB THERMAL PRINTER TEST\n"));
        await writer.write(encoder.encode("   =======================\n"));
        await writer.write(encoder.encode("   Status: ONLINE & OK\n\n\n\n"));
        await writer.write(new Uint8Array([0x1D, 0x56, 0x00])); // Cut
        
        writer.releaseLock();
        await port.close();
        setMessage({ text: 'Test receipt sent successfully.', type: 'success' });
      } catch (err: any) {
        setMessage({ text: `USB Connection failed: ${err.message || err}`, type: 'error' });
      }
    }
  };

  const testBarcodePrint = async () => {
    if (settings.barcodeMode === 'system') {
      alert("Barcode printing system uses your browser default print layout.");
      return;
    }

    if (settings.barcodeMode === 'usb') {
      if (!('serial' in navigator)) {
        setMessage({ text: 'Web Serial API is not supported. Please use Chrome.', type: 'error' });
        return;
      }
      try {
        // @ts-ignore
        const port = await navigator.serial.requestPort();
        await port.open({ baudRate: settings.barcodeBaudRate });
        const writer = port.writable.getWriter();
        const encoder = new TextEncoder();
        
        // TSC/ZPL label printer sample
        const labelCmd = "^XA\n^FO50,50^A0N,40,40^FDTEST LABEL^FS\n^FO50,100^BY3^BCN,100,Y,N,N^FD12345678^FS\n^XZ\n";
        await writer.write(encoder.encode(labelCmd));
        
        writer.releaseLock();
        await port.close();
        setMessage({ text: 'Test barcode command sent to label printer.', type: 'success' });
      } catch (err: any) {
        setMessage({ text: `USB Label Printer failed: ${err.message || err}`, type: 'error' });
      }
    }
  };

  return (
    <div className="bg-white p-6 rounded-xl border border-slate-100 shadow-sm max-w-4xl">
      <div className="flex items-center gap-3 mb-6">
        <div className="p-2 bg-indigo-50 text-indigo-600 rounded-lg">
          <Printer className="w-5 h-5" />
        </div>
        <div>
          <h3 className="font-bold text-slate-800">Printer &amp; Hardware Configuration</h3>
          <p className="text-xs text-slate-400">Configure thermal receipts (80mm) and barcode sticker labels</p>
        </div>
      </div>

      {message && (
        <div className={`mb-6 p-4 rounded-xl text-xs font-bold flex items-center gap-2 border ${
          message.type === 'success' ? 'bg-emerald-50 border-emerald-200 text-emerald-800' : 'bg-rose-50 border-rose-200 text-rose-800'
        }`}>
          {message.type === 'success' ? <CheckCircle2 className="w-4 h-4 text-emerald-600" /> : <AlertCircle className="w-4 h-4 text-rose-600" />}
          {message.text}
        </div>
      )}

      <div className="grid grid-cols-1 md:grid-cols-2 gap-8 mb-6">
        {/* Receipt Printer Panel */}
        <div className="space-y-4 border-r pr-0 md:pr-8 last:border-r-0">
          <h4 className="font-extrabold text-slate-700 text-sm flex items-center gap-1.5 pb-2 border-b">
            <Settings2 className="w-4 h-4 text-indigo-500" />
            Thermal Receipt Printer (Sales)
          </h4>

          <div>
            <label className="block text-xs font-bold text-slate-500 uppercase mb-2">Connection Interface</label>
            <select
              value={settings.receiptMode}
              onChange={(e: any) => setSettings({ ...settings, receiptMode: e.target.value })}
              className="w-full px-4 py-2.5 border rounded-xl text-sm bg-white"
            >
              <option value="system">System Default (Browser Print)</option>
              <option value="usb">USB Port (Web Serial - Direct)</option>
              <option value="network">LAN Network Printer (TCP/IP Proxy)</option>
            </select>
          </div>

          {settings.receiptMode === 'network' && (
            <div>
              <label className="block text-xs font-bold text-slate-500 uppercase mb-2">Printer IP Address</label>
              <input
                type="text"
                placeholder="e.g. 192.168.1.100"
                value={settings.receiptIp}
                onChange={(e) => setSettings({ ...settings, receiptIp: e.target.value })}
                className="w-full px-4 py-2.5 border rounded-xl text-sm"
              />
            </div>
          )}

          {settings.receiptMode === 'usb' && (
            <div>
              <label className="block text-xs font-bold text-slate-500 uppercase mb-2">Serial Baud Rate</label>
              <select
                value={settings.receiptBaudRate}
                onChange={(e: any) => setSettings({ ...settings, receiptBaudRate: parseInt(e.target.value) })}
                className="w-full px-4 py-2.5 border rounded-xl text-sm bg-white"
              >
                <option value="9600">9600 bps (Standard)</option>
                <option value="19200">19200 bps</option>
                <option value="38400">38400 bps</option>
                <option value="115200">115200 bps</option>
              </select>
            </div>
          )}

          <button
            onClick={testReceiptPrint}
            className="w-full py-2 bg-slate-100 hover:bg-slate-200 text-slate-700 font-bold text-xs rounded-xl transition"
          >
            Send Test Print Page
          </button>
        </div>

        {/* Barcode Printer Panel */}
        <div className="space-y-4">
          <h4 className="font-extrabold text-slate-700 text-sm flex items-center gap-1.5 pb-2 border-b">
            <Settings2 className="w-4 h-4 text-emerald-500" />
            Barcode Label Printer (USB)
          </h4>

          <div>
            <label className="block text-xs font-bold text-slate-500 uppercase mb-2">Connection Interface</label>
            <select
              value={settings.barcodeMode}
              onChange={(e: any) => setSettings({ ...settings, barcodeMode: e.target.value })}
              className="w-full px-4 py-2.5 border rounded-xl text-sm bg-white"
            >
              <option value="system">System Default Print Layout</option>
              <option value="usb">USB Label Port (Web Serial - ZPL/EPL)</option>
            </select>
          </div>

          {settings.barcodeMode === 'usb' && (
            <div>
              <label className="block text-xs font-bold text-slate-500 uppercase mb-2">Serial Baud Rate</label>
              <select
                value={settings.barcodeBaudRate}
                onChange={(e: any) => setSettings({ ...settings, barcodeBaudRate: parseInt(e.target.value) })}
                className="w-full px-4 py-2.5 border rounded-xl text-sm bg-white"
              >
                <option value="9600">9600 bps (Standard)</option>
                <option value="19200">19200 bps</option>
                <option value="38400">38400 bps</option>
              </select>
            </div>
          )}

          <button
            onClick={testBarcodePrint}
            className="w-full py-2 bg-slate-100 hover:bg-slate-200 text-slate-700 font-bold text-xs rounded-xl transition"
          >
            Print Test Label Sticker
          </button>
        </div>
      </div>

      <div className="flex justify-end pt-4 border-t">
        <button
          onClick={handleSave}
          className="px-6 py-2.5 bg-indigo-600 hover:bg-indigo-700 text-white font-bold text-sm rounded-xl shadow-sm transition"
        >
          Save Configuration
        </button>
      </div>
    </div>
  );
};
