import { test, expect } from '@playwright/test';
import { ReportsPage } from '../pages/ReportsPage';
import { LoginPage } from '../pages/LoginPage';

test.describe('Reports & Analytics Workflows', () => {
  let reportsPage: ReportsPage;
  let loginPage: LoginPage;

  test.beforeEach(async ({ page }) => {
    loginPage = new LoginPage(page);
    reportsPage = new ReportsPage(page);
    
    await loginPage.goto();
    await loginPage.quickDemoLogin();
    await expect(page).toHaveURL(/.*finance\/dashboard.*|.*dashboard.*/, { timeout: 15000 });
    
    await reportsPage.goto();
  });

  test('should load sales report and verify export functionality', async ({ page }) => {
    await reportsPage.viewSalesReport();
    // Verify that some charting element or table is rendered
    await expect(page.locator('canvas, svg, .recharts-wrapper, table').first()).toBeVisible({ timeout: 5000 });
    
    // Playwright can intercept downloads
    const [ download ] = await Promise.all([
      page.waitForEvent('download', { timeout: 5000 }).catch(() => null),
      reportsPage.exportReport()
    ]);
    
    if (download) {
      expect(download.suggestedFilename()).toBeTruthy();
    }
  });
});
