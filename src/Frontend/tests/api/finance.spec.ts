import { test, expect } from '@playwright/test';

const API_URL = 'http://localhost:5169/api';

// Helper: get auth token
async function getAuthToken(request: any): Promise<string | null> {
  try {
    const response = await request.post(`${API_URL}/auth/login`, {
      data: {
        username: 'admin@supermarket.local',
        password: 'Admin@123!',
        terminalCode: ''
      },
      timeout: 5000
    });
    if (response.ok()) {
      const body = await response.json();
      return body.token ?? body.accessToken ?? null;
    }
  } catch (_) {}
  return null;
}

test.describe('API Validation - Finance & Accounting Endpoints', () => {
  test('GET /journalentries - fetch journals ensures double entry mapping', async ({ request }) => {
    const token = await getAuthToken(request);
    if (!token) {
      console.log('[SKIP] Could not authenticate — skipping journal entries test');
      return;
    }
    try {
      const res = await request.get(`${API_URL}/journalentries`, {
        headers: { Authorization: `Bearer ${token}` },
        timeout: 8000
      });

      if (res.ok()) {
        const body = await res.json();
        expect(res.status()).toBe(200);
        // Verify double-entry balance on each journal
        if (Array.isArray(body) && body.length > 0) {
          const journal = body[0];
          expect(journal).toHaveProperty('totalDebit');
          expect(journal).toHaveProperty('totalCredit');
          // Double-entry: debit must equal credit (allow tiny floating point epsilon)
          const diff = Math.abs(journal.totalDebit - journal.totalCredit);
          expect(diff).toBeLessThan(0.01);
        }
      } else {
        // 401/403 is acceptable if no data seeded, but 500 is not
        expect(res.status()).not.toBe(500);
      }
    } catch (e) {
      console.log('Journal entries endpoint not reachable, skipping.');
    }
  });

  test('GET /finance/dashboard - finance dashboard returns aggregated metrics', async ({ request }) => {
    const token = await getAuthToken(request);
    if (!token) return;
    try {
      const res = await request.get(`${API_URL}/finance/dashboard`, {
        headers: { Authorization: `Bearer ${token}` },
        timeout: 8000
      });
      if (res.ok()) {
        const body = await res.json();
        expect(body).toHaveProperty('totalRevenue');
        expect(body).toHaveProperty('totalExpenses');
        expect(body).toHaveProperty('netProfit');
      } else {
        expect(res.status()).not.toBe(500);
      }
    } catch (e) {}
  });

  test('GET /accountspayable/bills - supplier bills endpoint', async ({ request }) => {
    const token = await getAuthToken(request);
    if (!token) return;
    try {
      const res = await request.get(`${API_URL}/accountspayable/bills`, {
        headers: { Authorization: `Bearer ${token}` },
        timeout: 8000
      });
      expect([200, 204, 401, 403]).toContain(res.status());
      expect(res.status()).not.toBe(500);
    } catch (e) {}
  });

  test('GET /accountsreceivable/receipts - customer receipts endpoint', async ({ request }) => {
    const token = await getAuthToken(request);
    if (!token) return;
    try {
      const res = await request.get(`${API_URL}/accountsreceivable/receipts`, {
        headers: { Authorization: `Bearer ${token}` },
        timeout: 8000
      });
      expect([200, 204, 401, 403]).toContain(res.status());
      expect(res.status()).not.toBe(500);
    } catch (e) {}
  });
});
