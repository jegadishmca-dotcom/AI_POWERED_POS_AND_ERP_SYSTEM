# Instructions

- Following Playwright test failed.
- Explain why, be concise, respect Playwright best practices.
- Provide a snippet of code with the fix, if possible.

# Test info

- Name: e2e\customer.spec.ts >> Customer Management Workflows >> should create a new customer
- Location: tests\e2e\customer.spec.ts:21:3

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
  2  | import { CustomerPage } from '../pages/CustomerPage';
  3  | import { LoginPage } from '../pages/LoginPage';
  4  | 
  5  | test.describe('Customer Management Workflows', () => {
  6  |   let customerPage: CustomerPage;
  7  |   let loginPage: LoginPage;
  8  | 
  9  |   test.beforeEach(async ({ page }) => {
  10 |     loginPage = new LoginPage(page);
  11 |     customerPage = new CustomerPage(page);
  12 |     
  13 |     await loginPage.goto();
  14 |     await loginPage.quickDemoLogin();
> 15 |     await expect(page).toHaveURL(/.*dashboard.*/, { timeout: 10000 });
     |                        ^ Error: expect(page).toHaveURL(expected) failed
  16 |     
  17 |     // CRM module navigation
  18 |     await customerPage.goto();
  19 |   });
  20 | 
  21 |   test('should create a new customer', async ({ page }) => {
  22 |     const timestamp = new Date().getTime();
  23 |     await customerPage.createCustomer(`TestUser ${timestamp}`, `98765${timestamp.toString().slice(-5)}`, `test${timestamp}@example.com`);
  24 |     
  25 |     // Verify creation success
  26 |     await expect(page.locator('text=/successfully|created/i')).toBeVisible({ timeout: 5000 });
  27 |   });
  28 | 
  29 |   test('should search existing customer', async ({ page }) => {
  30 |     await customerPage.searchCustomer('98765');
  31 |     await expect(customerPage.customerList).toBeVisible();
  32 |     // Wait for at least one row in the table/list
  33 |     await expect(page.locator('tr, .customer-item').nth(0)).toBeVisible();
  34 |   });
  35 | });
  36 | 
```