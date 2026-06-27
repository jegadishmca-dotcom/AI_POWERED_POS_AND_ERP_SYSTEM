import { Page, Locator } from '@playwright/test';

export class AccountingPage {
  readonly page: Page;
  readonly journalTab: Locator;
  readonly ledgerTab: Locator;
  readonly dateRangeInput: Locator;

  constructor(page: Page) {
    this.page = page;
    this.journalTab = page.locator('button, a', { hasText: /Journal Entries/i }).first();
    this.ledgerTab = page.locator('button, a', { hasText: /Chart of Accounts/i }).first();
    this.dateRangeInput = page.getByPlaceholder(/date/i).first();
  }

  async goto() {
    await this.page.goto('/finance/dashboard');
    await this.page.waitForTimeout(1000);
  }

  async viewJournalEntries() {
    // Navigate directly to avoid sidebar link DOM detach issues
    await this.page.goto('/finance/journals');
    await this.page.waitForTimeout(1500);
  }

  async viewGeneralLedger() {
    // Navigate directly to avoid sidebar link DOM detach issues
    await this.page.goto('/finance/accounts');
    await this.page.waitForTimeout(1500);
  }
}
