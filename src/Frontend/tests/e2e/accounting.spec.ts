import { test, expect } from '@playwright/test';
import { AccountingPage } from '../pages/AccountingPage';
import { LoginPage } from '../pages/LoginPage';

test.describe('Accounting Integration Workflows', () => {
  let accountingPage: AccountingPage;
  let loginPage: LoginPage;

  test.beforeEach(async ({ page }) => {
    loginPage = new LoginPage(page);
    accountingPage = new AccountingPage(page);
    
    await loginPage.goto();
    await loginPage.quickDemoLogin();
    await expect(page).toHaveURL(/.*dashboard.*/, { timeout: 10000 });
    
    await accountingPage.goto();
  });

  test('should verify journal entry list loads', async ({ page }) => {
    await accountingPage.viewJournalEntries();
    await expect(page.locator('table, .journal-list').first()).toBeVisible({ timeout: 5000 });
  });

  test('should verify general ledger loads', async ({ page }) => {
    await accountingPage.viewGeneralLedger();
    // Use valid CSS selectors only (no text= in CSS context); check for table or ledger list
    const ledgerElement = page.locator('table, .ledger-list, .chart-of-accounts').first();
    await expect(ledgerElement).toBeVisible({ timeout: 8000 });
  });
});
