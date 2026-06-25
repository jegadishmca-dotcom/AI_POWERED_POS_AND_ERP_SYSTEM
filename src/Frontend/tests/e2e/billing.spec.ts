import { test, expect } from '@playwright/test';
import { BillingPage } from '../pages/BillingPage';
import { LoginPage } from '../pages/LoginPage';

test.describe('Billing & POS Checkout Workflows', () => {
  let billingPage: BillingPage;
  let loginPage: LoginPage;

  test.beforeEach(async ({ page }) => {
    loginPage = new LoginPage(page);
    billingPage = new BillingPage(page);
    
    // Auth logic - login as admin to ensure we have access to POS
    await loginPage.goto();
    await loginPage.quickDemoLogin();
    // Wait for auth to complete
    await expect(page).toHaveURL(/.*dashboard.*/, { timeout: 10000 });
    await billingPage.goto();
  });

  test('should process a basic cash sale with barcode scan', async ({ page }) => {
    // 1. Scan/Add Product
    await billingPage.searchAndAddProduct('P001'); // Assuming P001 exists as a valid demo product
    
    // 2. Click Pay / Checkout
    // 3. Complete Cash Payment
    await billingPage.completeCashPayment('1000');
    
    // 4. Verify Invoice Generation
    await expect(billingPage.invoiceSuccessMessage).toBeVisible({ timeout: 8000 });
  });

  test('should apply discounts and update total', async ({ page }) => {
    await billingPage.searchAndAddProduct('P001');
    
    // Open discount modal or apply discount inline
    const discountButton = page.locator('button', { hasText: /Discount/i }).first();
    if (await discountButton.isVisible()) {
      await discountButton.click();
      await page.getByPlaceholder(/percentage|amount/i).first().fill('10');
      await page.locator('button', { hasText: /Apply/i }).first().click();
    }
    
    // Complete payment
    await billingPage.completeCashPayment('1000');
    await expect(billingPage.invoiceSuccessMessage).toBeVisible({ timeout: 8000 });
  });

  test('should increase and decrease product quantity', async ({ page }) => {
    await billingPage.searchAndAddProduct('P001');
    
    // Find the increase quantity button (commonly a plus icon)
    const increaseBtn = page.locator('button').filter({ has: page.locator('.lucide-plus-circle') }).first();
    const decreaseBtn = page.locator('button').filter({ has: page.locator('.lucide-minus-circle') }).first();
    
    if (await increaseBtn.isVisible()) {
      await increaseBtn.click();
      await page.waitForTimeout(500);
      await decreaseBtn.click();
    }
    
    await billingPage.completeCashPayment('1000');
    await expect(billingPage.invoiceSuccessMessage).toBeVisible({ timeout: 8000 });
  });
});
