import { Page, Locator, expect } from '@playwright/test';

export class LoginPage {
  readonly page: Page;
  readonly usernameInput: Locator;
  readonly passwordInput: Locator;
  readonly terminalCodeInput: Locator;
  readonly posCashierTab: Locator;
  readonly erpBackOfficeTab: Locator;
  readonly signInButton: Locator;
  readonly demoAdminButton: Locator;
  readonly errorMessage: Locator;

  constructor(page: Page) {
    this.page = page;
    this.usernameInput = page.locator('input[name="username"]');
    this.passwordInput = page.locator('input[name="password"]');
    this.terminalCodeInput = page.locator('input[name="terminalCode"]');
    this.posCashierTab = page.locator('button', { hasText: 'POS Cashier' });
    this.erpBackOfficeTab = page.locator('button', { hasText: 'ERP Back-Office' });
    this.signInButton = page.locator('button[type="submit"]');
    this.demoAdminButton = page.locator('button', { hasText: /Quick Login as Demo Admin/i });
    // Error messages use red background div
    this.errorMessage = page.locator('.bg-red-50, [class*="bg-red"]').first();
  }

  async goto() {
    await this.page.goto('/login');
    // Wait for the login form to be visible
    await this.signInButton.waitFor({ state: 'visible', timeout: 10000 });
    await this.page.waitForTimeout(500);
  }

  async selectPosCashier() {
    await this.posCashierTab.click();
  }

  async selectErpBackOffice() {
    await this.erpBackOfficeTab.click();
  }

  async login(username: string, password: string, isCashier: boolean = false) {
    if (isCashier) {
      await this.selectPosCashier();
    } else {
      await this.selectErpBackOffice();
    }
    await this.page.waitForTimeout(300);
    await this.usernameInput.fill(username);
    await this.passwordInput.fill(password);
    await this.signInButton.click();
  }

  /**
   * Logs in as admin using the ERP Back-Office credentials.
   * Uses direct form fill with known working credentials.
   */
  async quickDemoLogin() {
    // Switch to ERP Back-Office mode
    await this.erpBackOfficeTab.click();
    await this.page.waitForTimeout(300);

    // Fill credentials directly (admin@supermarket.local / Admin@123!)
    await this.usernameInput.fill('admin@supermarket.local');
    await this.passwordInput.fill('Admin@123!');
    await this.signInButton.click();

    // Wait for navigation away from login page
    await this.page.waitForURL(/(?!.*login).*/, { timeout: 15000 }).catch(() => {
      // If URL doesn't change, the login may have failed - let test handle it
    });
  }
}
