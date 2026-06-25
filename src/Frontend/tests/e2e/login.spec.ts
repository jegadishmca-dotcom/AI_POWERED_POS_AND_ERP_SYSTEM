import { test, expect } from '@playwright/test';
import { LoginPage } from '../pages/LoginPage';

test.describe('Authentication Workflows', () => {
  let loginPage: LoginPage;

  test.beforeEach(async ({ page }) => {
    loginPage = new LoginPage(page);
    await loginPage.goto();
  });

  test('Valid admin login via Demo button', async ({ page }) => {
    await loginPage.quickDemoLogin();
    // Wait for navigation or specific element on dashboard
    await expect(page).toHaveURL(/.*dashboard.*/, { timeout: 10000 });
  });

  test('Invalid password', async ({ page }) => {
    await loginPage.login('admin', 'wrongpassword', false);
    await expect(loginPage.errorMessage).toBeVisible({ timeout: 5000 });
  });

  test('Invalid username', async ({ page }) => {
    await loginPage.login('unknownuser', 'password123', false);
    await expect(loginPage.errorMessage).toBeVisible({ timeout: 5000 });
  });

  test('Cashier login requires terminal code', async ({ page }) => {
    // If we haven't registered terminal code, it should show error locally
    // Playwright test runner won't have the localStorage set by default.
    await loginPage.login('cashier', 'password123', true);
    // There is a specific error for terminal code missing on client side
    await expect(page.locator('text=Terminal Code is required for POS cashier login')).toBeVisible();
  });
});
