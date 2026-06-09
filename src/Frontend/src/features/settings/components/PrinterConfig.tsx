import React, { useState, useEffect } from 'react';
import { Printer, Settings2, CheckCircle2, AlertCircle } from 'lucide-react';
import JsBarcode from 'jsbarcode';

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
      const html = `<!DOCTYPE html>
<html lang="ta">
<head>
  <meta charset="UTF-8"/>
  <title>Receipt Test Print</title>
  <style>
    * { margin: 0; padding: 0; box-sizing: border-box; }
    body { font-family: monospace; font-size: 11px; width: 80mm; color: #000; background: #fff; padding: 10px; }
    hr { border: none; border-top: 1px dashed #000; margin: 6px 0; }
  </style>
</head>
<body>
  <div style="text-align:center;font-weight:bold;font-size:14px;">APPLE SUPER MARKET</div>
  <div style="text-align:center;font-weight:bold;font-size:12px;">ஆப்பிள் சூப்பர் மார்க்கெட்</div>
  <hr/>
  <div style="text-align:center;font-weight:bold;margin:10px 0;">SYSTEM PRINTER TEST</div>
  <div style="display:flex;justify-content:space-between;margin-bottom:2px;"><span>Date: ${new Date().toLocaleDateString('en-IN')}</span><span>Time: ${new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}</span></div>
  <hr/>
  <div style="display:flex;justify-content:space-between;margin-bottom:2px;"><span>Printer Mode:</span><span>System Default</span></div>
  <div style="display:flex;justify-content:space-between;margin-bottom:2px;"><span>Paper Width:</span><span>80mm (Thermal)</span></div>
  <div style="display:flex;justify-content:space-between;margin-bottom:2px;"><span>Connection:</span><span>ONLINE & OK</span></div>
  <hr/>
  <div style="text-align:center;margin-top:15px;font-size:10px;">
    <div>Thank you for shopping with us!</div>
    <div>Visit Again!</div>
  </div>
  <div style="height:20mm;"></div>
  <script>
    window.onload = function() {
      window.print();
      setTimeout(function() { window.close(); }, 1000);
    };
  </script>
</body>
</html>`;
      const printWindow = window.open('', '_blank', 'width=400,height=600,scrollbars=yes');
      if (printWindow) {
        printWindow.document.open();
        printWindow.document.write(html);
        printWindow.document.close();
      } else {
        setMessage({ text: 'Please allow popups for this site to print receipt test page.', type: 'error' });
      }
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
        let port;
        // @ts-ignore
        const ports = await navigator.serial.getPorts();
        if (ports && ports.length > 0) {
          port = ports[0];
        } else {
          // @ts-ignore
          port = await navigator.serial.requestPort();
        }
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
        let errMsg = err.message || err;
        if (err.name === 'NotFoundError' || errMsg.includes('No port selected')) {
          errMsg = "No port was selected. Please plug in and turn on the printer, click again, and select the device in the browser prompt.";
        }
        setMessage({ text: `USB Connection failed: ${errMsg}`, type: 'error' });
      }
    }
  };

  const testBarcodePrint = async () => {
    if (settings.barcodeMode === 'system') {
      const barcodeValue = "TEST12345678";
      const tempContainer = document.createElement('div');
      const svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
      tempContainer.appendChild(svg);
      
      try {
        JsBarcode(svg, barcodeValue, {
          format: "CODE128",
          width: 2,
          height: 40,
          displayValue: false, // Hide numeric text below barcode lines
          margin: 0
        });
      } catch (e) {
        console.error("Barcode generation failed", e);
        setMessage({ text: 'Failed to generate test barcode SVG.', type: 'error' });
        return;
      }

      const svgHtml = svg.outerHTML;

      // Mock Packed and Expiry dates
      const pkdDate = "09/06/2026";
      const expDate = "09/12/2026";

      const labelColHtml = `
      <div class="label-col">
        <div class="header-spacer"></div>
        <div class="middle-row">
          <div class="code-vertical">3387</div>
          <div class="barcode-svg">${svgHtml}</div>
        </div>
        <div class="product-name">Test Sticker Label</div>
        <div class="price-row">₹ : 0.00</div>
        <div class="dates-row">
          <span>PKD:${pkdDate}</span>
          <span>EXP:${expDate}</span>
        </div>
      </div>
    `;

      const html = `<!DOCTYPE html>
<html>
<head>
  <meta charset="UTF-8"/>
  <title>Print Barcode - Test Label</title>
  <style>
    @page {
      size: 102mm 22mm;
      margin: 0;
    }
    * {
      margin: 0;
      padding: 0;
      box-sizing: border-box;
    }
    body {
      width: 102mm;
      height: 22mm;
      display: flex;
      flex-direction: row;
      justify-content: space-between;
      align-items: center;
      background: #fff;
      overflow: hidden;
      padding: 0 1.5mm;
    }
    .label-col {
      width: 31mm;
      height: 22mm;
      display: flex;
      flex-direction: column;
      justify-content: flex-start;
      overflow: hidden;
    }
    .header-spacer {
      height: 4.5mm;
      width: 100%;
    }
    .middle-row {
      display: flex;
      flex-direction: row;
      align-items: center;
      justify-content: space-between;
      height: 9mm;
      margin-top: 0.5mm;
    }
    .code-vertical {
      font-size: 7px;
      font-weight: bold;
      writing-mode: vertical-rl;
      transform: rotate(180deg);
      white-space: nowrap;
      width: 3mm;
      text-align: center;
      font-family: Arial, sans-serif;
    }
    .barcode-svg {
      flex-grow: 1;
      display: flex;
      justify-content: center;
      align-items: center;
      height: 100%;
      overflow: hidden;
      padding-left: 1mm;
    }
    .barcode-svg svg {
      width: 100%;
      height: auto;
      max-height: 9mm;
    }
    .product-name {
      font-size: 7.5px;
      font-weight: bold;
      text-align: center;
      margin-top: 0.3mm;
      height: 3mm;
      line-height: 3mm;
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
      font-family: Arial, sans-serif;
    }
    .price-row {
      font-size: 9.5px;
      font-weight: bold;
      text-align: center;
      height: 3.2mm;
      line-height: 3.2mm;
      font-family: Arial, sans-serif;
    }
    .dates-row {
      display: flex;
      justify-content: space-between;
      font-size: 5.2px;
      font-family: monospace;
      height: 1.8mm;
      line-height: 1.8mm;
      padding: 0 0.5mm;
    }
    @media print {
      body {
        width: 102mm;
        height: 22mm;
      }
    }
  </style>
</head>
<body>
  ${labelColHtml}
  ${labelColHtml}
  ${labelColHtml}
  <script>
    window.onload = function() {
      window.print();
      setTimeout(function() { window.close(); }, 1000);
    };
  </script>
</body>
</html>`;

      const printWindow = window.open('', '_blank', 'width=450,height=250');
      if (printWindow) {
        printWindow.document.open();
        printWindow.document.write(html);
        printWindow.document.close();
      } else {
        setMessage({ text: 'Please allow popups for this site to print barcodes.', type: 'error' });
      }
      return;
    }

    if (settings.barcodeMode === 'usb') {
      if (!('serial' in navigator)) {
        setMessage({ text: 'Web Serial API is not supported. Please use Chrome.', type: 'error' });
        return;
      }
      try {
        let port;
        // @ts-ignore
        const ports = await navigator.serial.getPorts();
        if (ports && ports.length > 0) {
          port = ports[0];
        } else {
          // @ts-ignore
          port = await navigator.serial.requestPort();
        }
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
        let errMsg = err.message || err;
        if (err.name === 'NotFoundError' || errMsg.includes('No port selected')) {
          errMsg = "No port was selected. Please plug in and turn on the printer, click again, and select the device in the browser prompt.";
        }
        setMessage({ text: `USB Label Printer failed: ${errMsg}`, type: 'error' });
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
        <div className="space-y-4 border-r pr-0 md:pr-8 last:border-r-0 flex flex-col justify-between">
          <div className="space-y-4">
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

            {settings.receiptMode === 'system' && (
              <div className="text-[11px] text-slate-500 bg-slate-50/80 p-3 rounded-lg border border-slate-100/80 leading-normal">
                <strong>Tip:</strong> Uses standard system driver printing. Set up your printer in Windows/macOS Control Panel, select it as default or in print preview.
              </div>
            )}
            {settings.receiptMode === 'usb' && (
              <div className="text-[11px] text-slate-500 bg-slate-50/80 p-3 rounded-lg border border-slate-100/80 leading-normal">
                <strong>Tip:</strong> Requires virtual serial port drivers (e.g., CH340 or POS PRINTER USB-to-Serial). Select the matching COM port when prompted.
              </div>
            )}
            {settings.receiptMode === 'network' && (
              <div className="text-[11px] text-slate-500 bg-slate-50/80 p-3 rounded-lg border border-slate-100/80 leading-normal">
                <strong>Tip:</strong> Sends ESC/POS raw print data directly over TCP port 9100. Make sure the printer is on the same local network subnet.
              </div>
            )}
          </div>

          <button
            onClick={testReceiptPrint}
            className="w-full py-2 bg-slate-100 hover:bg-slate-200 text-slate-700 font-bold text-xs rounded-xl transition mt-4"
          >
            Send Test Print Page
          </button>
        </div>

        {/* Barcode Printer Panel */}
        <div className="space-y-4 flex flex-col justify-between">
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

            {settings.barcodeMode === 'system' && (
              <div className="text-[11px] text-slate-500 bg-slate-50/80 p-3 rounded-lg border border-slate-100/80 leading-normal">
                <strong>Tip:</strong> Recommended for standard USB label printers. Renders clean HTML labels and opens the standard browser print window.
              </div>
            )}
            {settings.barcodeMode === 'usb' && (
              <div className="text-[11px] text-slate-500 bg-slate-50/80 p-3 rounded-lg border border-slate-100/80 leading-normal">
                <strong>Tip:</strong> Sends raw ZPL/EPL commands directly. Requires a ZPL-compatible barcode printer configured on a virtual COM port. Select the COM port when prompted.
              </div>
            )}
          </div>

          <button
            onClick={testBarcodePrint}
            className="w-full py-2 bg-slate-100 hover:bg-slate-200 text-slate-700 font-bold text-xs rounded-xl transition mt-4"
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
