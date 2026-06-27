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
    await this.page.waitForTimeout(1500);
  }

  /**
   * Navigate using React Router's SPA navigation to avoid full page reload
   * which would cause Zustand auth store to lose its in-memory state.
   */
  async viewJournalEntries() {
    // Use sidebar link click (SPA navigation) rather than page.goto (full reload)
    const journalLink = this.page.locator('a[href="/finance/journals"], a[href*="journals"]').first();
    const linkVisible = await journalLink.isVisible({ timeout: 3000 }).catch(() => false);
    
    if (linkVisible) {
      await journalLink.click();
    } else {
      // Fall back to direct navigation
      await this.page.goto('/finance/journals');
    }
    await this.page.waitForTimeout(1500);
  }

  async viewGeneralLedger() {
    // Use sidebar link click (SPA navigation) rather than page.goto (full reload)
    const accountsLink = this.page.locator('a[href="/finance/accounts"], a[href*="/finance/accounts"]').first();
    const linkVisible = await accountsLink.isVisible({ timeout: 3000 }).catch(() => false);
    
    if (linkVisible) {
      await accountsLink.click();
    } else {
      // Fall back to direct navigation
      await this.page.goto('/finance/accounts');
    }
    await this.page.waitForTimeout(1500);
  }
}
