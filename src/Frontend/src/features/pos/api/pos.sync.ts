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
    
    // Server returns syncedIds: string[] — the invoice IDs that were committed.
    // Remove ONLY those invoices from the local sync queue.
    const syncedIds: string[] = res.data.syncedIds || res.data.SyncedIds || [];

    if (syncedIds.length > 0) {
      // Normalize to lowercase for case-insensitive matching against Dexie keys
      const syncedSet = new Set(syncedIds.map((id: string) => id.toLowerCase()));

      const idsToRemove = pending
        .filter((inv: any) => syncedSet.has(String(inv.id).toLowerCase()))
        .map((inv: any) => inv.id);

      if (idsToRemove.length > 0) {
        await posDb.sync_queue.bulkDelete(idsToRemove);
        console.info(`[Sync] Removed ${idsToRemove.length} synced invoice(s) from queue.`);
      }
    }

    // Log any failures so the operator is aware queued invoices remain
    if (res.data.failed > 0) {
      const errors: string[] = res.data.errors || res.data.Errors || [];
      console.warn(
        `[Sync] ${res.data.failed} invoice(s) failed to sync and remain queued for retry.`,
        errors
      );
    }
  } catch (error) {
    console.error('[Sync] Offline mode: Sync failed, will retry later.', error);
  }
};
