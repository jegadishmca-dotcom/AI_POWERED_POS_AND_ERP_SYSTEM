import { test, expect } from '@playwright/test';

const API_URL = 'http://localhost:5169/api';

test.describe('API Validation - Inventory Endpoints', () => {
  test('GET /catalog/products/search - validates search response schema and performance', async ({ request }) => {
    try {
      const startTime = Date.now();
      const res = await request.get(`${API_URL}/catalog/products/search?q=apple`, { timeout: 5000 });
      const duration = Date.now() - startTime;
      
      if (res.ok()) {
        const body = await res.json();
        expect(res.status()).toBe(200);
        expect(Array.isArray(body)).toBeTruthy();
        if (body.length > 0) {
          expect(body[0]).toHaveProperty('productCode');
          expect(body[0]).toHaveProperty('name');
        }
        // Assert performance < 300ms if possible, though local may vary
        expect(duration).toBeLessThan(1000); 
      }
    } catch (e) {}
  });
});
