import { test, expect } from '@playwright/test';

test.describe('Pass 3: AI & Analytics Dashboards E2E Suite', () => {
  test.beforeEach(async ({ page }) => {
    // Intercept backend auth refresh endpoint so session validation succeeds
    await page.route('**/api/auth/refresh', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ accessToken: 'mock-jwt-token' })
      });
    });

    // Mock executive dashboard endpoints
    await page.route('**/api/executive/dashboard/kpis', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          dailySales: 125000,
          dailyProfit: 32000,
          grossMarginPct: 25.6,
          totalInventoryValue: 1450000,
          deadStockValue: 24000,
          activeLoyaltyMembers: 14595,
          activeCustomers: 14617
        })
      });
    });

    await page.route('**/api/executive/dashboard/trends*', async (route) => {
      const url = route.request().url();
      if (url.includes('days=365') || url.includes('days=90')) {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify([
            { snapshotDate: '2025-10-01T00:00:00Z', dailySales: 95000, dailyProfit: 8000 },
            { snapshotDate: '2026-01-15T00:00:00Z', dailySales: 105000, dailyProfit: 8840 },
            { snapshotDate: '2026-03-23T00:00:00Z', dailySales: 112000, dailyProfit: 9430 },
            { snapshotDate: '2026-04-10T00:00:00Z', dailySales: 120000, dailyProfit: 10100 },
            { snapshotDate: '2026-09-01T00:00:00Z', dailySales: 110000, dailyProfit: 28000 },
            { snapshotDate: '2026-09-02T00:00:00Z', dailySales: 125000, dailyProfit: 32000 }
          ])
        });
      } else {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify([
            { snapshotDate: '2026-09-01T00:00:00Z', dailySales: 110000, dailyProfit: 28000 },
            { snapshotDate: '2026-09-02T00:00:00Z', dailySales: 125000, dailyProfit: 32000 }
          ])
        });
      }
    });

    await page.route('**/api/ai/insights*', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([])
      });
    });

    await page.route('**/api/ai/forecasts?type=*', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          { forecastDate: '2026-09-04T00:00:00Z', predictedQuantity: 450, lowerBoundQuantity: 400, upperBoundQuantity: 500, entityName: 'Aachi Masala' },
          { forecastDate: '2026-09-05T00:00:00Z', predictedQuantity: 480, lowerBoundQuantity: 420, upperBoundQuantity: 540, entityName: 'Arokya Milk' }
        ])
      });
    });

    await page.route('**/api/ai/forecasts/accuracy', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          { overallMape: 4.25, overallRmse: 12.80 }
        ])
      });
    });

    await page.route('**/api/ai/store-performance', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          { storeName: 'Store 1', region: 'North', rank: 1, revenueVariance: 12.5, percentile: 98, aiScore: 92 },
          { storeName: 'Store 2', region: 'South', rank: 2, revenueVariance: 8.2, percentile: 91, aiScore: 88 },
          { storeName: 'Store 3', region: 'East', rank: 3, revenueVariance: -2.1, percentile: 65, aiScore: 74 },
          { storeName: 'Store 4', region: 'West', rank: 4, revenueVariance: -5.4, percentile: 40, aiScore: 65 }
        ])
      });
    });

    // Seed authenticated admin user in Zustand localStorage before page load
    await page.addInitScript(() => {
      window.localStorage.setItem('pos_server_ip', 'http://localhost:5173');
      window.localStorage.setItem(
        'pos-auth-storage',
        JSON.stringify({
          state: {
            user: {
              id: '00000000-0000-0000-0000-000000000001',
              username: 'admin@supermarket.local',
              fullName: 'System Administrator',
              role: 'Owner'
            }
          },
          version: 0
        })
      );
    });
  });

  test('Executive Dashboard - Filters, Export, and measured-vs-estimated disclosure elements are present', async ({ page }) => {
    await page.goto('/ai/executive');
    await page.waitForLoadState('networkidle');

    await expect(page).not.toHaveURL(/\/login/);

    // Verify title
    await expect(page.getByRole('heading', { name: 'Executive Intelligence', exact: true })).toBeVisible({ timeout: 5000 });

    // --- DISCLOSURE ASSERTIONS ---
    // 1. Measured legend badge must exist
    await expect(page.locator('span', { hasText: 'Measured (Apr 2026+)' })).toBeVisible();
    // 2. Estimated legend badge must exist
    await expect(page.locator('span', { hasText: 'Estimated (pre-Apr 2026)' })).toBeVisible();
    // 3. Two-part disclosure paragraph must exist
    await expect(page.locator('text=Revenue is authentic across all dates')).toBeVisible();
    await expect(page.locator('text=legacy carton-cost calculation defect')).toBeVisible();

    // 4. Verify 7d Daily Average subtitle
    await expect(page.getByText('7-Day Daily Average', { exact: true })).toBeVisible();

    // Open Filters dropdown and select 30 days
    const filterBtn = page.locator('button', { hasText: /Filters/ });
    await expect(filterBtn).toBeVisible();
    await filterBtn.click();

    const opt30d = page.locator('button', { hasText: 'Last 30 Days' });
    await expect(opt30d).toBeVisible();
    await opt30d.click();
    await expect(page.locator('button', { hasText: 'Filters (30d)' })).toBeVisible();

    // 5. Verify 30-Day Daily Average subtitles
    await expect(page.getByText('30-Day Daily Average', { exact: true })).toBeVisible();
    await expect(page.getByText('30-Day Daily Average (Per-line-item measured)')).toBeVisible();

    // Switch to Past Year (365d) to verify estimated amber shading & demarcation line
    await page.locator('button', { hasText: 'Filters (30d)' }).click();
    const opt365d = page.locator('button', { hasText: 'Past Year (365d)' });
    await expect(opt365d).toBeVisible();
    await opt365d.click();
    await expect(page.locator('button', { hasText: 'Filters (365d)' })).toBeVisible();

    // 6. Verify 365-Day Daily Average subtitles
    await expect(page.getByText('365-Day Daily Average', { exact: true })).toBeVisible();
    await expect(page.getByText('365-Day Daily Average (Blend: measured + estimated)')).toBeVisible();

    // 7. ASSERT ACTUAL RENDERED SVG ELEMENTS for ReferenceArea and ReferenceLine
    const refAreaRect = page.locator('.recharts-reference-area-rect').first();
    await expect(refAreaRect).toBeVisible({ timeout: 5000 });
    const areaBox = await refAreaRect.boundingBox();
    expect(areaBox).not.toBeNull();
    expect(areaBox!.width).toBeGreaterThan(10);
    expect(areaBox!.height).toBeGreaterThan(10);

    // 8. Demarcation line at transition date
    const refLine = page.locator('.recharts-reference-line-line').first();
    await expect(refLine).toBeAttached({ timeout: 5000 });
    const lineBox = await refLine.boundingBox();
    expect(lineBox).not.toBeNull();
    expect(lineBox!.height).toBeGreaterThan(50);
    // Demarcation label should be visible
    await expect(page.locator('text=Measured →')).toBeVisible();

    // Click Export Report and verify download includes Profit Source column
    const exportBtn = page.locator('button', { hasText: 'Export Report' });
    await expect(exportBtn).toBeVisible();
    
    const downloadPromise = page.waitForEvent('download', { timeout: 5000 });
    await exportBtn.click();
    const download = await downloadPromise;
    expect(download.suggestedFilename()).toContain('executive_report_');

    // 9. Read the downloaded CSV and verify Profit Source column header and average KPI rows
    const csvContent = await (await download.createReadStream()).toArray();
    const csvText = Buffer.concat(csvContent).toString('utf-8');
    expect(csvText).toContain('Profit Source');
    expect(csvText).toContain('365d Average');
    expect(csvText).toMatch(/Measured \(per-line-item\)|Estimated \(2026 margin applied\)/);
  });

  test('Forecast Dashboard - View type toggle, Export Data, and no profit/margin figures shown', async ({ page }) => {
    await page.goto('/ai/forecast-dashboard');
    await page.waitForLoadState('networkidle');

    await expect(page).not.toHaveURL(/\/login/);

    await expect(page.locator('h1', { hasText: 'AI Demand Forecasting' })).toBeVisible({ timeout: 5000 });

    // --- NEGATIVE DISCLOSURE ASSERTION ---
    // This dashboard shows demand quantity forecasts, NOT profit/margin data.
    // Confirm no profit-related disclosure language is present (it shouldn't need one).
    await expect(page.locator('text=Estimated (pre-Apr 2026)')).not.toBeVisible();
    await expect(page.locator('text=carton-cost')).not.toBeVisible();

    // Switch view to Category
    const categoryBtn = page.locator('button', { hasText: 'Category' });
    await expect(categoryBtn).toBeVisible();
    await categoryBtn.click();
    await expect(categoryBtn).toHaveClass(/bg-indigo-600/);

    // Switch view to Store
    const storeBtn = page.locator('button', { hasText: 'Store' });
    await expect(storeBtn).toBeVisible();
    await storeBtn.click();
    await expect(storeBtn).toHaveClass(/bg-indigo-600/);

    // Export Data CSV download
    const exportBtn = page.locator('button', { hasText: 'Export Data' });
    await expect(exportBtn).toBeVisible();

    const downloadPromise = page.waitForEvent('download', { timeout: 5000 });
    await exportBtn.click();
    const download = await downloadPromise;
    expect(download.suggestedFilename()).toContain('ai_demand_forecast_');
  });

  test('Store Performance Dashboard - Demo Simulation disclosure and Compare Regions modal', async ({ page }) => {
    await page.goto('/ai/store-performance');
    await page.waitForLoadState('networkidle');

    await expect(page).not.toHaveURL(/\/login/);

    await expect(page.locator('h1', { hasText: 'Store Benchmark Dashboard' })).toBeVisible({ timeout: 5000 });

    // --- DEMO SIMULATION DISCLOSURE ASSERTIONS ---
    // 1. Demo Simulation badge must be visible in the page title
    await expect(page.locator('span', { hasText: 'Demo Simulation' }).first()).toBeVisible();
    // 2. Transparency alert banner must explain single-store operation
    await expect(page.locator('text=DEMO PREVIEW: Multi-Store Franchise Simulation')).toBeVisible();
    await expect(page.locator('text=single flagship store (Branch 1')).toBeVisible();

    // Click Compare Regions (Demo)
    const compareBtn = page.locator('button', { hasText: 'Compare Regions' });
    await expect(compareBtn).toBeVisible();
    await compareBtn.click();

    // Verify Modal appears with regions and Simulated Demo badge
    const modalHeader = page.locator('h3', { hasText: 'Regional Performance Benchmarks' });
    await expect(modalHeader).toBeVisible({ timeout: 5000 });
    // 3. Modal must carry Simulated Demo badge
    await expect(page.locator('span', { hasText: 'Simulated Demo' })).toBeVisible();
    await expect(page.locator('span', { hasText: 'North Region' }).first()).toBeVisible();
    await expect(page.locator('span', { hasText: 'South Region' }).first()).toBeVisible();

    // Close modal
    const closeBtn = page.locator('button', { hasText: 'Close Comparison' });
    await expect(closeBtn).toBeVisible();
    await closeBtn.click();
    await expect(modalHeader).not.toBeVisible();
  });
});
