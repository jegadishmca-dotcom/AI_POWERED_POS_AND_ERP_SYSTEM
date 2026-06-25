# Instructions

- Following Playwright test failed.
- Explain why, be concise, respect Playwright best practices.
- Provide a snippet of code with the fix, if possible.

# Test info

- Name: e2e\billing.spec.ts >> Billing & POS Checkout Workflows >> should process a basic cash sale with barcode scan
- Location: tests\e2e\billing.spec.ts:21:3

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
  2  | import { BillingPage } from '../pages/BillingPage';
  3  | import { LoginPage } from '../pages/LoginPage';
  4  | 
  5  | test.describe('Billing & POS Checkout Workflows', () => {
  6  |   let billingPage: BillingPage;
  7  |   let loginPage: LoginPage;
  8  | 
  9  |   test.beforeEach(async ({ page }) => {
  10 |     loginPage = new LoginPage(page);
  11 |     billingPage = new BillingPage(page);
  12 |     
  13 |     // Auth logic - login as admin to ensure we have access to POS
  14 |     await loginPage.goto();
  15 |     await loginPage.quickDemoLogin();
  16 |     // Wait for auth to complete
> 17 |     await expect(page).toHaveURL(/.*dashboard.*/, { timeout: 10000 });
     |                        ^ Error: expect(page).toHaveURL(expected) failed
  18 |     await billingPage.goto();
  19 |   });
  20 | 
  21 |   test('should process a basic cash sale with barcode scan', async ({ page }) => {
  22 |     // 1. Scan/Add Product
  23 |     await billingPage.searchAndAddProduct('P001'); // Assuming P001 exists as a valid demo product
  24 |     
  25 |     // 2. Click Pay / Checkout
  26 |     // 3. Complete Cash Payment
  27 |     await billingPage.completeCashPayment('1000');
  28 |     
  29 |     // 4. Verify Invoice Generation
  30 |     await expect(billingPage.invoiceSuccessMessage).toBeVisible({ timeout: 8000 });
  31 |   });
  32 | 
  33 |   test('should apply discounts and update total', async ({ page }) => {
  34 |     await billingPage.searchAndAddProduct('P001');
  35 |     
  36 |     // Open discount modal or apply discount inline
  37 |     const discountButton = page.locator('button', { hasText: /Discount/i }).first();
  38 |     if (await discountButton.isVisible()) {
  39 |       await discountButton.click();
  40 |       await page.getByPlaceholder(/percentage|amount/i).first().fill('10');
  41 |       await page.locator('button', { hasText: /Apply/i }).first().click();
  42 |     }
  43 |     
  44 |     // Complete payment
  45 |     await billingPage.completeCashPayment('1000');
  46 |     await expect(billingPage.invoiceSuccessMessage).toBeVisible({ timeout: 8000 });
  47 |   });
  48 | 
  49 |   test('should increase and decrease product quantity', async ({ page }) => {
  50 |     await billingPage.searchAndAddProduct('P001');
  51 |     
  52 |     // Find the increase quantity button (commonly a plus icon)
  53 |     const increaseBtn = page.locator('button').filter({ has: page.locator('.lucide-plus-circle') }).first();
  54 |     const decreaseBtn = page.locator('button').filter({ has: page.locator('.lucide-minus-circle') }).first();
  55 |     
  56 |     if (await increaseBtn.isVisible()) {
  57 |       await increaseBtn.click();
  58 |       await page.waitForTimeout(500);
  59 |       await decreaseBtn.click();
  60 |     }
  61 |     
  62 |     await billingPage.completeCashPayment('1000');
  63 |     await expect(billingPage.invoiceSuccessMessage).toBeVisible({ timeout: 8000 });
  64 |   });
  65 | });
  66 | 
```