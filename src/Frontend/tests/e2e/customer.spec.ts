import { test, expect } from '@playwright/test';
import { CustomerPage } from '../pages/CustomerPage';
import { LoginPage } from '../pages/LoginPage';

test.describe('Customer Management Workflows', () => {
  let customerPage: CustomerPage;
  let loginPage: LoginPage;

  test.beforeEach(async ({ page }) => {
    loginPage = new LoginPage(page);
    customerPage = new CustomerPage(page);
    
    await loginPage.goto();
    await loginPage.quickDemoLogin();
    await expect(page).toHaveURL(/.*dashboard.*/, { timeout: 10000 });
    
    // CRM module navigation
    await customerPage.goto();
  });

  test('should create a new customer', async ({ page }) => {
    const timestamp = new Date().getTime();
    await customerPage.createCustomer(`TestUser ${timestamp}`, `98765${timestamp.toString().slice(-5)}`, `test${timestamp}@example.com`);
    
    // Verify creation success
    await expect(page.locator('text=/successfully|created/i')).toBeVisible({ timeout: 5000 });
  });

  test('should search existing customer', async ({ page }) => {
    await customerPage.searchCustomer('98765');
    await expect(customerPage.customerList).toBeVisible();
    // Wait for at least one row in the table/list
    await expect(page.locator('tr, .customer-item').nth(0)).toBeVisible();
  });
});
