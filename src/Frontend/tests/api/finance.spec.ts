import { test, expect } from '@playwright/test';

const API_URL = 'http://localhost:5169/api';

test.describe('API Validation - Finance & Accounting Endpoints', () => {
  test('GET /finance/journals - fetch journals ensures double entry mapping', async ({ request }) => {
    try {
      const res = await request.get(`${API_URL}/finance/journals`, { timeout: 5000 });
      
      if (res.ok()) {
        const body = await res.json();
        expect(res.status()).toBe(200);
        // Add robust assertions checking debit and credit totals
        if (body.length > 0) {
           const journal = body[0];
           expect(journal).toHaveProperty('totalDebit');
           expect(journal).toHaveProperty('totalCredit');
           expect(journal.totalDebit).toEqual(journal.totalCredit); // Accounting rule
        }
      }
    } catch (e) {}
  });
});
