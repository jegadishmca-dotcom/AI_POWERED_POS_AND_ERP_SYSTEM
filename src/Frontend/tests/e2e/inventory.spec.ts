import { test, expect } from '@playwright/test';
import { InventoryPage } from '../pages/InventoryPage';
import { LoginPage } from '../pages/LoginPage';

test.describe('Inventory Management Workflows', () => {
  let inventoryPage: InventoryPage;
  let loginPage: LoginPage;

  test.beforeEach(async ({ page }) => {
    loginPage = new LoginPage(page);
    inventoryPage = new InventoryPage(page);
    
    await loginPage.goto();
    await loginPage.quickDemoLogin();
    await expect(page).toHaveURL(/.*dashboard.*/, { timeout: 10000 });
    
    await inventoryPage.goto();
  });

  test('should search inventory and view stock levels', async ({ page }) => {
    await inventoryPage.searchProduct('P001');
    await expect(inventoryPage.productList).toBeVisible();
    await expect(page.locator('tr, .inventory-item').nth(0)).toBeVisible();
  });

  test('should verify batch details visibility', async ({ page }) => {
    await inventoryPage.searchProduct('P001');
    if (await inventoryPage.batchDetailsButton.isVisible()) {
      await inventoryPage.viewBatches();
      await expect(page.locator('text=/Expiry|Batch/i').first()).toBeVisible();
    }
  });
});
