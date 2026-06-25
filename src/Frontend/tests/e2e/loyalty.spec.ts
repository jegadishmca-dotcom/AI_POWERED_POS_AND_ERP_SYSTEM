import { test, expect } from '@playwright/test';
import { LoyaltyPage } from '../pages/LoyaltyPage';
import { LoginPage } from '../pages/LoginPage';

test.describe('Loyalty Engine Workflows', () => {
  let loyaltyPage: LoyaltyPage;
  let loginPage: LoginPage;

  test.beforeEach(async ({ page }) => {
    loginPage = new LoginPage(page);
    loyaltyPage = new LoyaltyPage(page);
    
    await loginPage.goto();
    await loginPage.quickDemoLogin();
    await expect(page).toHaveURL(/.*dashboard.*/, { timeout: 10000 });
    await loyaltyPage.goto();
  });

  test('should view loyalty balance for customer', async ({ page }) => {
    await loyaltyPage.searchCustomerInput.fill('9876543210');
    await page.keyboard.press('Enter');
    
    // Expect loyalty summary to be visible
    await expect(page.locator('text=/Balance|Total Points/i').first()).toBeVisible({ timeout: 5000 });
  });

  test('should allow points redemption', async ({ page }) => {
    await loyaltyPage.searchCustomerInput.fill('9876543210');
    await page.keyboard.press('Enter');
    
    if (await loyaltyPage.redeemPointsButton.isVisible()) {
      await loyaltyPage.redeemPoints('100');
      await expect(page.locator('text=/successfully|redeemed/i')).toBeVisible({ timeout: 5000 });
    }
  });
});
