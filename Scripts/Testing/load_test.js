import http from 'k6/http';
import { check, sleep } from 'k6';

// Run with: k6 run load_test.js

export const options = {
  scenarios: {
    pos_checkout: {
      executor: 'ramping-vus',
      startVUs: 0,
      stages: [
        { duration: '1m', target: 500 }, // Ramp up to 500 VUs
        { duration: '3m', target: 500 }, // Hold 500 VUs for 3 minutes
        { duration: '1m', target: 0 },   // Ramp down
      ],
      gracefulRampDown: '30s',
      exec: 'posCheckout',
    },
    inventory_api: {
      executor: 'ramping-vus',
      startVUs: 0,
      stages: [
        { duration: '1m', target: 200 }, // Ramp up to 200 VUs
        { duration: '2m', target: 200 },
        { duration: '1m', target: 0 },
      ],
      exec: 'inventoryApi',
    },
    ai_api: {
      executor: 'ramping-vus',
      startVUs: 0,
      stages: [
        { duration: '30s', target: 100 }, // Ramp up to 100 VUs
        { duration: '1m', target: 100 },
        { duration: '30s', target: 0 },
      ],
      exec: 'aiApi',
    },
  },
  thresholds: {
    // POS targets
    'http_req_duration{scenario:pos_checkout}': ['p(95)<500', 'p(99)<1000'],
    'http_req_failed{scenario:pos_checkout}': ['rate<0.01'], // < 1% error rate
    
    // Inventory targets
    'http_req_duration{scenario:inventory_api}': ['p(95)<800'],
    
    // AI targets (More compute intensive)
    'http_req_duration{scenario:ai_api}': ['p(95)<1500'],
  },
};

const BASE_URL = 'http://localhost:5000/api';

// POS Checkout Simulation
export function posCheckout() {
  const payload = JSON.stringify({
    storeId: '550e8400-e29b-41d4-a716-446655440000',
    terminalId: '550e8400-e29b-41d4-a716-446655440001',
    businessDate: new Date().toISOString(),
    totalAmount: 1500.50,
    items: [
      { productId: '550e8400-e29b-41d4-a716-446655440002', quantity: 2, unitPrice: 500 },
      { productId: '550e8400-e29b-41d4-a716-446655440003', quantity: 1, unitPrice: 500.50 }
    ],
    payments: [
      { method: 'Card', amount: 1500.50 }
    ]
  });

  const params = { headers: { 'Content-Type': 'application/json' } };
  const res = http.post(`${BASE_URL}/pos/checkout`, payload, params);
  
  check(res, { 'status is 200 or 201': (r) => r.status === 200 || r.status === 201 });
  sleep(1);
}

// Inventory API Simulation
export function inventoryApi() {
  const res = http.get(`${BASE_URL}/inventory/health`);
  check(res, { 'status is 200': (r) => r.status === 200 });
  sleep(2);
}

// AI Intelligence API Simulation
export function aiApi() {
  const res = http.get(`${BASE_URL}/ai/insights?status=New`);
  check(res, { 'status is 200': (r) => r.status === 200 });
  sleep(3);
}
