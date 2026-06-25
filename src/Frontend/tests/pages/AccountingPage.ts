import { Page, Locator } from '@playwright/test';

export class AccountingPage {
  readonly page: Page;
  readonly journalTab: Locator;
  readonly ledgerTab: Locator;
  readonly dateRangeInput: Locator;

  constructor(page: Page) {
    this.page = page;
    this.journalTab = page.locator('button, a', { hasText: /Journal Entries/i }).first();
    this.ledgerTab = page.locator('button, a', { hasText: /General Ledger/i }).first();
    this.dateRangeInput = page.getByPlaceholder(/date/i).first();
  }

  async goto() {
    await this.page.goto('/finance');
  }

  async viewJournalEntries() {
    await this.journalTab.click();
  }

  async viewGeneralLedger() {
    await this.ledgerTab.click();
  }
}
