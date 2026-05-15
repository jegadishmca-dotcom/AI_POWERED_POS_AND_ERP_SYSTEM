$frontendDir = "d:\JEGADISH\AI_POWERED_POS_AND_ERP_SYSTEM\src\Frontend"

New-Item -ItemType Directory -Force -Path "$frontendDir\src\features\pos\db"
New-Item -ItemType Directory -Force -Path "$frontendDir\src\features\pos\hooks"
New-Item -ItemType Directory -Force -Path "$frontendDir\src\features\pos\components"
New-Item -ItemType Directory -Force -Path "$frontendDir\src\features\pos\api"
New-Item -ItemType Directory -Force -Path "$frontendDir\src\features\pos\routes"
New-Item -ItemType Directory -Force -Path "$frontendDir\src\features\pos\types"

# Types
@"
export interface CartItem {
  id: string; // uuid
  productId: string;
  barcode?: string;
  name: string;
  quantity: number;
  unitPrice: number;
  discountAmount: number;
  totalAmount: number;
}

export interface Invoice {
  id: string;
  businessDate: string;
  invoiceNumber: string;
  terminalId: string;
  terminalSequence: number;
  cashierId: string;
  subTotal: number;
  discountAmount: number;
  taxAmount: number;
  totalAmount: number;
  roundOff: number;
  netPayable: number;
  paymentMode: string;
  status: 'PENDING' | 'COMPLETED' | 'HOLD';
  items: CartItem[];
}
"@ | Out-File -FilePath "$frontendDir\src\features\pos\types\index.ts" -Encoding utf8

# Dexie Database Schema
@"
import Dexie, { Table } from 'dexie';
import { Invoice } from '../types';

export interface LocalProduct {
  id: string;
  code: string;
  name: string;
  barcode: string;
  price: number;
  isWeighable: boolean;
}

export class PosDatabase extends Dexie {
  catalog!: Table<LocalProduct, string>;
  invoices!: Table<Invoice, string>;
  sync_queue!: Table<Invoice, string>;

  constructor() {
    super('PosDatabase');
    this.version(1).stores({
      catalog: 'id, code, barcode, name', // Primary key and indexed props
      invoices: 'id, status',
      sync_queue: 'id',
    });
  }
}

export const posDb = new PosDatabase();
"@ | Out-File -FilePath "$frontendDir\src\features\pos\db\pos.db.ts" -Encoding utf8

# API Sync
@"
import { api } from '@/utils/api';
import { Invoice } from '../types';
import { posDb } from '../db/pos.db';

export const syncInvoices = async () => {
  const pending = await posDb.sync_queue.toArray();
  if (pending.length === 0) return;

  try {
    const res = await api.post('/api/pos/sync', { invoices: pending });
    
    // If successful, clear the sync queue
    if (res.data.failed === 0) {
      await posDb.sync_queue.clear();
    } else {
      console.warn('Partial sync success', res.data.errors);
      // Logic to remove only successfully synced invoices goes here
    }
  } catch (error) {
    console.error('Offline mode: Sync failed, will retry later.');
  }
};
"@ | Out-File -FilePath "$frontendDir\src\features\pos\api\pos.sync.ts" -Encoding utf8

# Hooks
@"
import { useEffect, useCallback } from 'react';

// Weighted barcode format: 21 + 5 digit item code + 5 digit weight/price + 1 checksum
// Example: 210012300500C -> Item 00123, 500g

export const useBarcodeScanner = (onScan: (barcode: string, weight?: number) => void) => {
  let barcodeBuffer = '';
  let lastKeyTime = 0;

  const handleKeyDown = useCallback((e: KeyboardEvent) => {
    // Ignore if typing in an input field (unless we want scanner to override)
    if ((e.target as HTMLElement).tagName === 'INPUT') return;

    const currentTime = new Date().getTime();
    
    // Scanner types very fast (< 30ms between strokes)
    if (currentTime - lastKeyTime > 50) {
      barcodeBuffer = ''; // Reset if typing too slow (human)
    }
    
    if (e.key === 'Enter') {
      if (barcodeBuffer.length > 3) {
        // Parse weighted barcode
        if (barcodeBuffer.startsWith('21') && barcodeBuffer.length === 13) {
          const itemCode = barcodeBuffer.substring(2, 7);
          const weightVal = parseInt(barcodeBuffer.substring(7, 12), 10);
          onScan(itemCode, weightVal / 1000); // Assuming grams, convert to Kg
        } else {
          onScan(barcodeBuffer);
        }
      }
      barcodeBuffer = '';
    } else if (e.key.length === 1) {
      barcodeBuffer += e.key;
    }

    lastKeyTime = currentTime;
  }, [onScan]);

  useEffect(() => {
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [handleKeyDown]);
};
"@ | Out-File -FilePath "$frontendDir\src\features\pos\hooks\useBarcodeScanner.ts" -Encoding utf8

@"
import { useEffect } from 'react';

type PosShortcuts = {
  onF1Search?: () => void;
  onF4Payment?: () => void;
  onF9Park?: () => void;
};

export const usePosKeyboardShortcuts = ({ onF1Search, onF4Payment, onF9Park }: PosShortcuts) => {
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      switch (e.key) {
        case 'F1':
          e.preventDefault();
          onF1Search?.();
          break;
        case 'F4':
          e.preventDefault();
          onF4Payment?.();
          break;
        case 'F9':
          e.preventDefault();
          onF9Park?.();
          break;
      }
    };

    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [onF1Search, onF4Payment, onF9Park]);
};
"@ | Out-File -FilePath "$frontendDir\src\features\pos\hooks\usePosKeyboardShortcuts.ts" -Encoding utf8

# Components
@"
import React from 'react';
import { CartItem } from '../types';

export const CartView = ({ items }: { items: CartItem[] }) => {
  return (
    <div className="flex-1 bg-white overflow-y-auto">
      <table className="min-w-full divide-y divide-gray-200">
        <thead className="bg-gray-50 sticky top-0">
          <tr>
            <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase">Item</th>
            <th className="px-6 py-3 text-right text-xs font-medium text-gray-500 uppercase">Qty</th>
            <th className="px-6 py-3 text-right text-xs font-medium text-gray-500 uppercase">Price</th>
            <th className="px-6 py-3 text-right text-xs font-medium text-gray-500 uppercase">Total</th>
          </tr>
        </thead>
        <tbody className="divide-y divide-gray-200">
          {items.map((item, idx) => (
            <tr key={idx} className="hover:bg-blue-50">
              <td className="px-6 py-4 text-sm font-medium text-gray-900">{item.name}</td>
              <td className="px-6 py-4 text-sm text-gray-500 text-right">{item.quantity}</td>
              <td className="px-6 py-4 text-sm text-gray-500 text-right">{item.unitPrice.toFixed(2)}</td>
              <td className="px-6 py-4 text-sm text-gray-900 font-bold text-right">{item.totalAmount.toFixed(2)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
};
"@ | Out-File -FilePath "$frontendDir\src\features\pos\components\CartView.tsx" -Encoding utf8

@"
import React from 'react';
import { Invoice } from '../types';

// Used for printing via CSS media queries (@media print)
export const ThermalReceipt = React.forwardRef<HTMLDivElement, { invoice: Invoice }>(({ invoice }, ref) => {
  return (
    <div ref={ref} className="hidden print:block w-[80mm] text-black font-mono text-xs bg-white p-2">
      <div className="text-center font-bold text-lg mb-2">ENTERPRISE SUPERMARKET</div>
      <div className="text-center mb-4">Tax Invoice</div>
      
      <div className="mb-2">Invoice No: {invoice.invoiceNumber}</div>
      <div className="mb-4">Date: {new Date(invoice.businessDate).toLocaleString()}</div>
      
      <table className="w-full text-left mb-4">
        <thead>
          <tr className="border-b border-black">
            <th>Item</th>
            <th className="text-right">Qty</th>
            <th className="text-right">Amt</th>
          </tr>
        </thead>
        <tbody>
          {invoice.items.map((item, idx) => (
            <tr key={idx}>
              <td>{item.name}</td>
              <td className="text-right">{item.quantity}</td>
              <td className="text-right">{item.totalAmount.toFixed(2)}</td>
            </tr>
          ))}
        </tbody>
      </table>
      
      <div className="border-t border-black pt-2 mb-4">
        <div className="flex justify-between font-bold">
          <span>Net Payable:</span>
          <span>{invoice.netPayable.toFixed(2)}</span>
        </div>
      </div>
      
      <div className="text-center mt-8">Thank you for shopping!</div>
    </div>
  );
});
"@ | Out-File -FilePath "$frontendDir\src\features\pos\components\ThermalReceipt.tsx" -Encoding utf8

@"
import React, { useState, useRef } from 'react';
import { useBarcodeScanner } from '../hooks/useBarcodeScanner';
import { usePosKeyboardShortcuts } from '../hooks/usePosKeyboardShortcuts';
import { CartView } from './CartView';
import { ThermalReceipt } from './ThermalReceipt';
import { CartItem, Invoice } from '../types';
import { posDb } from '../db/pos.db';
import { v4 as uuidv4 } from 'uuid';

export const PosTerminal = () => {
  const [cart, setCart] = useState<CartItem[]>([]);
  const receiptRef = useRef<HTMLDivElement>(null);

  // Hook 1: Scanner integration
  useBarcodeScanner(async (barcode, weight) => {
    // Lookup in local Dexie DB
    const product = await posDb.catalog.where('barcode').equals(barcode).or('code').equals(barcode).first();
    
    if (product) {
      const qty = weight ? weight : 1;
      const newItem: CartItem = {
        id: uuidv4(),
        productId: product.id,
        name: product.name,
        quantity: qty,
        unitPrice: product.price,
        discountAmount: 0,
        totalAmount: product.price * qty
      };
      setCart(prev => [...prev, newItem]);
    } else {
      console.warn("Product not found locally: ", barcode);
      // Play error beep
    }
  });

  // Hook 2: F-Keys
  usePosKeyboardShortcuts({
    onF1Search: () => console.log('Open Search Modal'),
    onF4Payment: () => handlePayment(),
    onF9Park: () => console.log('Park Invoice')
  });

  const handlePayment = async () => {
    if (cart.length === 0) return;

    // Construct invoice
    const total = cart.reduce((sum, item) => sum + item.totalAmount, 0);
    const invoice: Invoice = {
      id: uuidv4(),
      businessDate: new Date().toISOString(),
      invoiceNumber: 'TERM1-20260515-001', // Generate real sequence in prod
      terminalId: 'local-term',
      terminalSequence: 1,
      cashierId: 'user',
      subTotal: total,
      discountAmount: 0,
      taxAmount: 0,
      totalAmount: total,
      roundOff: 0,
      netPayable: total,
      paymentMode: 'CASH',
      status: 'COMPLETED',
      items: cart
    };

    // 1. Save locally for sync
    await posDb.sync_queue.add(invoice);
    
    // 2. Print (triggers browser print dialog mapped to receipt printer)
    window.print();
    
    // 3. Clear cart
    setCart([]);
  };

  const totalPayable = cart.reduce((sum, item) => sum + item.totalAmount, 0);

  return (
    <div className="h-screen flex flex-col bg-slate-100">
      <div className="flex-1 flex overflow-hidden">
        {/* Left Panel - Search / Quick Keys */}
        <div className="w-1/3 bg-slate-800 text-white p-4 flex flex-col">
          <div className="text-xl font-bold mb-4">POS Engine</div>
          <div className="flex-1">
            <div className="bg-slate-700 p-4 rounded text-center mb-2">Scan Barcode to Add</div>
            <div className="bg-slate-700 p-4 rounded text-center mb-2">F1 - Search Item</div>
            <div className="bg-slate-700 p-4 rounded text-center mb-2">F4 - Payment</div>
            <div className="bg-slate-700 p-4 rounded text-center mb-2">F9 - Park Bill</div>
          </div>
        </div>

        {/* Right Panel - Cart */}
        <div className="flex-1 flex flex-col">
          <CartView items={cart} />
          
          {/* Totals & Actions */}
          <div className="bg-white p-6 border-t shadow-lg flex justify-between items-center">
            <div className="text-3xl font-bold text-slate-800">
              Total: ₹{totalPayable.toFixed(2)}
            </div>
            <button 
              onClick={handlePayment}
              className="px-8 py-4 bg-emerald-600 text-white text-xl font-bold rounded shadow hover:bg-emerald-700"
            >
              PAY (F4)
            </button>
          </div>
        </div>
      </div>
      
      {/* Hidden Receipt for Printing */}
      {cart.length > 0 && <ThermalReceipt ref={receiptRef} invoice={{...cart} as any} />}
    </div>
  );
};
"@ | Out-File -FilePath "$frontendDir\src\features\pos\components\PosTerminal.tsx" -Encoding utf8

Write-Host "Frontend POS Scaffolded"
