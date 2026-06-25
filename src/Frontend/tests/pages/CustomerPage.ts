import { Page, Locator } from '@playwright/test';

export class CustomerPage {
  readonly page: Page;
  readonly searchInput: Locator;
  readonly newCustomerButton: Locator;
  readonly nameInput: Locator;
  readonly phoneInput: Locator;
  readonly emailInput: Locator;
  readonly submitButton: Locator;
  readonly customerList: Locator;

  constructor(page: Page) {
    this.page = page;
    this.searchInput = page.getByPlaceholder(/search customer/i).first();
    this.newCustomerButton = page.locator('button', { hasText: /New Customer|Add Customer/i }).first();
    this.nameInput = page.getByPlaceholder(/name/i).first();
    this.phoneInput = page.getByPlaceholder(/phone/i).first();
    this.emailInput = page.getByPlaceholder(/email/i).first();
    this.submitButton = page.locator('button', { hasText: /Save|Submit|Register/i }).first();
    this.customerList = page.locator('table, .customer-list').first();
  }

  async goto() {
    await this.page.goto('/crm/customers');
  }

  async createCustomer(name: string, phone: string, email: string) {
    await this.newCustomerButton.click();
    await this.nameInput.fill(name);
    await this.phoneInput.fill(phone);
    await this.emailInput.fill(email);
    await this.submitButton.click();
  }

  async searchCustomer(query: string) {
    await this.searchInput.fill(query);
    await this.page.keyboard.press('Enter');
  }
}
