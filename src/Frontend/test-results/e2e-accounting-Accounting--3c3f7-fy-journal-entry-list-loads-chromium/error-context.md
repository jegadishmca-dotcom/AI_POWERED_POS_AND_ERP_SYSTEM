# Instructions

- Following Playwright test failed.
- Explain why, be concise, respect Playwright best practices.
- Provide a snippet of code with the fix, if possible.

# Test info

- Name: e2e\accounting.spec.ts >> Accounting Integration Workflows >> should verify journal entry list loads
- Location: tests\e2e\accounting.spec.ts:20:3

# Error details

```
Error: expect(page).toHaveURL(expected) failed

Expected pattern: /.*dashboard.*/
Received string:  "http://localhost:5173/login"
Timeout: 10000ms

Call log:
  - Expect "toHaveURL" with timeout 10000ms
    23 × unexpected value "http://localhost:5173/login"

```

```yaml
- heading "Supermarket POS & ERP" [level=2]
- paragraph: Access the point-of-sale terminal or central ERP platform
- button "POS Cashier":
  - img
  - text: POS Cashier
- button "ERP Back-Office":
  - img
  - text: ERP Back-Office
- text: Username
- textbox "Enter username": demo@supermarket.com
- text: Password
- textbox "••••••••": Demo@123456
- button "Sign In to ERP Back-Office"
- text: Want to explore a quick test-drive?
- button "🔑 Quick Login as Demo Admin"
```

# Test source

```ts
  1  | import { test, expect } from '@playwright/test';
  2  | import { AccountingPage } from '../pages/AccountingPage';
  3  | import { LoginPage } from '../pages/LoginPage';
  4  | 
  5  | test.describe('Accounting Integration Workflows', () => {
  6  |   let accountingPage: AccountingPage;
  7  |   let loginPage: LoginPage;
  8  | 
  9  |   test.beforeEach(async ({ page }) => {
  10 |     loginPage = new LoginPage(page);
  11 |     accountingPage = new AccountingPage(page);
  12 |     
  13 |     await loginPage.goto();
  14 |     await loginPage.quickDemoLogin();
> 15 |     await expect(page).toHaveURL(/.*dashboard.*/, { timeout: 10000 });
     |                        ^ Error: expect(page).toHaveURL(expected) failed
  16 |     
  17 |     await accountingPage.goto();
  18 |   });
  19 | 
  20 |   test('should verify journal entry list loads', async ({ page }) => {
  21 |     await accountingPage.viewJournalEntries();
  22 |     await expect(page.locator('table, .journal-list').first()).toBeVisible({ timeout: 5000 });
  23 |   });
  24 | 
  25 |   test('should verify general ledger loads', async ({ page }) => {
  26 |     await accountingPage.viewGeneralLedger();
  27 |     await expect(page.locator('table, .ledger-list').first()).toBeVisible({ timeout: 5000 });
  28 |   });
  29 | });
  30 | 
```