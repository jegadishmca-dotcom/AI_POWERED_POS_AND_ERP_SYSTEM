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
