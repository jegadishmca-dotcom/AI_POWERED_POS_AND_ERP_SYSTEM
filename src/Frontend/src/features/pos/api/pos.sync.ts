import { api } from '@/utils/api';
import { Invoice } from '../types';
import { posDb } from '../db/pos.db';
import { safeRandomUUID } from '@/utils/uuid';

export const syncInvoices = async () => {
  const pending = await posDb.sync_queue.toArray();
  if (pending.length === 0) return;

  try {
    const isUUID = (str: string) => /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(str);
    
    const payload = pending.map((inv: any) => ({
      ...inv,
      id: isUUID(inv.id) ? inv.id : safeRandomUUID(),
      items: inv.items.map((item: any) => ({
        ...item,
        id: isUUID(item.id) ? item.id : safeRandomUUID()
      }))
    }));

    const res = await api.post('/api/pos/sync', { invoices: payload });
    
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
