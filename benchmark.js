const axios = require('axios');
const { performance } = require('perf_hooks');

const API_BASE = 'http://localhost:5030';
const NUM_OFFERS_TO_CREATE = 50;

async function runBenchmark() {
    console.log('Starting Phase 2 Verification Benchmark...');

    try {
        console.log('Authenticating as Manager...');
        const loginRes = await axios.post(`${API_BASE}/api/auth/login`, {
            username: 'demomanager',
            password: 'Password123!',
            terminalId: '00000000-0000-0000-0000-000000000000'
        });
        const token = loginRes.data.token;
        const config = { headers: { Authorization: `Bearer ${token}` } };

        // 1. Create 50 Offers
        console.log(`Creating ${NUM_OFFERS_TO_CREATE} active offers...`);
        const offers = [];
        for (let i = 1; i <= NUM_OFFERS_TO_CREATE; i++) {
            const type = i % 2 === 0 ? 'Combo' : 'PercentageDiscount';
            offers.push({
                name: `Stress Offer ${i}`,
                description: `Generated for stress testing`,
                offerType: type,
                rulesJson: type === 'PercentageDiscount' 
                    ? '{"conditions":[{"type":"CartTotal","operator":">=","value":500}],"actions":[{"type":"ApplyDiscount","discountType":"Percentage","value":10}]}'
                    : '{"conditions":[{"type":"HasProducts","productIds":["00000000-0000-0000-0000-000000000001"],"minQuantity":2}],"actions":[{"type":"ApplyDiscount","discountType":"FixedAmount","value":50}]}',
                priority: i,
                isStackable: i % 5 !== 0,
                isExclusive: i === 50,
                isActive: true,
                startDate: new Date().toISOString(),
                endDate: new Date(Date.now() + 86400000).toISOString()
            });
        }

        const startCreate = performance.now();
        for (const offer of offers) {
            await axios.post(`${API_BASE}/api/offers`, offer, config);
        }
        console.log(`Successfully created ${NUM_OFFERS_TO_CREATE} offers in ${(performance.now() - startCreate).toFixed(2)}ms`);

        // 2. Test Cart Calculation with 1 item, 10 items, 50 items
        const testCartSizes = [1, 10, 50];
        for (const size of testCartSizes) {
            const items = [];
            for (let i = 1; i <= size; i++) {
                items.push({
                    id: `item-${i}`,
                    productId: `00000000-0000-0000-0000-00000000000${(i%9)+1}`,
                    qty: 2,
                    unitPrice: 100,
                    lineTotal: 200,
                    cgstRate: 9,
                    sgstRate: 9
                });
            }
            const payload = {
                items,
                customerTier: 'Base',
                promoCode: null,
                applyOffers: true
            };

            const calcStart = performance.now();
            const res = await axios.post(`${API_BASE}/api/pos/calculate-cart`, payload, config);
            const calcTime = performance.now() - calcStart;
            
            console.log(`Cart Size ${size} items -> Eval Time: ${calcTime.toFixed(2)}ms, Applied Offers: ${res.data.appliedOfferNames.length}`);
        }

    } catch (err) {
        console.error('Benchmark failed:', err.response?.data || err.message);
    }
}

runBenchmark().catch(console.error);
