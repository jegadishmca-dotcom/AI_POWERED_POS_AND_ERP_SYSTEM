import { api } from '@/utils/api';
import { Invoice } from '../types';

/**
 * Converts the invoice object into a raw text string suitable for thermal printers.
 */
const generateEscPosText = (invoice: Invoice): string => {
  let text = "     ENTERPRISE SUPERMARKET\n";
  text += "          Tax Invoice\n";
  text += "--------------------------------\n";
  text += `Inv: ${invoice.invoiceNumber}\n`;
  text += `Date: ${invoice.createdAt}\n`;
  text += "--------------------------------\n";
  invoice.items.forEach(item => {
    text += `${item.name}\n`;
    text += `${item.qty} x ${item.unitPrice}    ${item.finalLineTotal}\n`;
  });
  text += "--------------------------------\n";
  text += `TOTAL:           ${invoice.totalAmount}\n`;
  text += `NET PAYABLE:     ${invoice.netPayable}\n`;
  text += "--------------------------------\n";
  text += "   Thank you for shopping!   \n\n\n\n";
  return text;
};

/**
 * Attempts to print using Web Serial API (USB ESC/POS).
 * Falls back to Network Printer Proxy via Backend.
 */
export const printReceipt = async (invoice: Invoice, useWebSerial: boolean = true) => {
  const textContent = generateEscPosText(invoice);

  // 1. Web Serial API (Hardware USB)
  if (useWebSerial && 'serial' in navigator) {
    try {
      // @ts-ignore
      const port = await navigator.serial.requestPort();
      await port.open({ baudRate: 9600 });
      const writer = port.writable.getWriter();
      
      const encoder = new TextEncoder();
      
      // Init ESC/POS
      await writer.write(new Uint8Array([0x1B, 0x40]));
      // Write text
      await writer.write(encoder.encode(textContent));
      // Cut paper
      await writer.write(new Uint8Array([0x0A, 0x0A, 0x0A, 0x0A, 0x1D, 0x56, 0x00]));
      
      writer.releaseLock();
      await port.close();
      return true;
    } catch (err) {
      console.warn("Web Serial failed or user cancelled. Falling back to Network Proxy.", err);
    }
  }

  // 2. Fallback: Network Proxy (LAN Printer via Backend)
  try {
    await api.post('/api/pos/print', { invoice });
    return true;
  } catch (err) {
    console.error("Network Print Proxy failed.", err);
    
    // 3. Last Resort: CSS window.print() 
    window.print();
    return false;
  }
};
