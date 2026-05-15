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
  held_invoices!: Table<Invoice, string>; // Local holding

  constructor() {
    super('PosDatabase');
    this.version(2).stores({
      catalog: 'id, code, barcode, name' // Proper indexes added for <100ms lookup, 
      invoices: 'id, status',
      sync_queue: 'id',
      held_invoices: 'id' // Held carts
    });
  }
}

export const posDb = new PosDatabase();

