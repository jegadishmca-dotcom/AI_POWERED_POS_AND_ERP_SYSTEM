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
    this.demoAdminButton = page.locator('button', { hasText: 'Quick Login as Demo Admin' });
    // Assuming errors use this specific div based on code inspection
    this.errorMessage = page.locator('.bg-red-50'); 
  }

  async goto() {
    await this.page.goto('/');
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
    
    await this.usernameInput.fill(username);
    await this.passwordInput.fill(password);
    await this.signInButton.click();
  }

  async quickDemoLogin() {
    await this.demoAdminButton.click();
  }
}
