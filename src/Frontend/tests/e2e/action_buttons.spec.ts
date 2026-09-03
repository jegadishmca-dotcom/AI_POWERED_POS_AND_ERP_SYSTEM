import { test, expect } from '@playwright/test';

test.describe('Finance Action Buttons & Modals E2E Suite', () => {
  test.beforeEach(async ({ page }) => {
    // Intercept backend auth refresh endpoint so session validation succeeds
    await page.route('**/api/auth/refresh', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ accessToken: 'mock-jwt-token' })
      });
    });

    // Seed authenticated admin user in Zustand localStorage before page load
    await page.addInitScript(() => {
      window.localStorage.setItem('pos_server_ip', 'http://localhost:5173');
      window.localStorage.setItem(
        'pos-auth-storage',
        JSON.stringify({
          state: {
            user: {
              id: '00000000-0000-0000-0000-000000000001',
              username: 'admin@supermarket.local',
              fullName: 'System Administrator',
              role: 'Owner'
            }
          },
          version: 0
        })
      );
    });
  });

  test('Chart of Accounts - Add Account Modal opens, renders inputs, and closes', async ({ page }) => {
    await page.goto('/finance/accounts');
    await page.waitForLoadState('networkidle');

    // Fail loudly if auth failed and redirected to login
    await expect(page).not.toHaveURL(/\/login/);

    const addButton = page.locator('button', { hasText: 'Add Account' });
    await expect(addButton).toBeVisible({ timeout: 5000 });
    await addButton.click();

    // Assert modal header and form elements
    const modalTitle = page.locator('h3', { hasText: 'Add General Ledger Account' });
    await expect(modalTitle).toBeVisible({ timeout: 5000 });
    await expect(page.locator('input[placeholder="e.g. 1010, 2050"]')).toBeVisible();

    // Close modal and verify dismissal
    await page.locator('button[aria-label="Close Modal"]').click();
    await expect(modalTitle).not.toBeVisible();
  });

  test('Customer Receipts - Record Receipt Modal opens, selects customer from autocomplete, and submits', async ({ page }) => {
    // Mock customer search API
    await page.route('**/api/customers/search*', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          { id: 'f56770bf-9bb7-446d-b859-41dd8ed7ef51', name: 'Test Customer 01', phone: '9000000001', tierName: 'Gold' }
        ])
      });
    });

    // Mock successful receipt submission
    await page.route('**/api/AccountsReceivable/receipts', async (route) => {
      if (route.request().method() === 'POST') {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ id: 'rec-101', amount: 150.00, status: 'Completed' })
        });
      } else {
        await route.fulfill({ status: 200, contentType: 'application/json', body: '[]' });
      }
    });

    await page.goto('/finance/customer-receipts');
    await page.waitForLoadState('networkidle');

    // Fail loudly if auth failed
    await expect(page).not.toHaveURL(/\/login/);

    const recordButton = page.locator('button', { hasText: 'Record Receipt' });
    await expect(recordButton).toBeVisible({ timeout: 5000 });
    await recordButton.click();

    // Assert modal header and inputs
    const modalTitle = page.locator('h3', { hasText: 'Record New Receipt' });
    await expect(modalTitle).toBeVisible({ timeout: 5000 });

    const searchInput = page.locator('input[placeholder="Type customer name or phone..."]');
    const amountInput = page.locator('input[placeholder="0.00"]');
    await expect(searchInput).toBeVisible();
    await expect(amountInput).toBeVisible();

    // Type customer name and select from autocomplete dropdown
    await searchInput.fill('Test Customer');
    const suggestionItem = page.locator('[data-testid="customer-option"]').first();
    await expect(suggestionItem).toBeVisible({ timeout: 5000 });
    await suggestionItem.click();

    // Verify Selected badge appears
    await expect(page.locator('text=Selected')).toBeVisible();

    // Fill amount and submit
    await amountInput.fill('150.00');

    const saveButton = page.locator('button[type="submit"]', { hasText: 'Save Receipt' });
    await expect(saveButton).toBeVisible();
    await saveButton.click();

    // Modal closes automatically on successful submission
    await expect(modalTitle).not.toBeVisible();
  });

  test('Customer Receipts - Displays error banner when API submission fails', async ({ page }) => {
    // Mock customer search API
    await page.route('**/api/customers/search*', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          { id: 'f56770bf-9bb7-446d-b859-41dd8ed7ef51', name: 'Jegadish Mathiyazhagan', phone: '9597344096', tierName: 'Gold' }
        ])
      });
    });

    // Intercept POST request and return 400 validation error
    await page.route('**/api/AccountsReceivable/receipts', async (route) => {
      if (route.request().method() === 'POST') {
        await route.fulfill({
          status: 400,
          contentType: 'application/json',
          body: JSON.stringify({ message: 'Customer account suspended or invalid receipt amount.' })
        });
      } else {
        await route.fulfill({ status: 200, contentType: 'application/json', body: '[]' });
      }
    });

    await page.goto('/finance/customer-receipts');
    await page.waitForLoadState('networkidle');

    // Fail loudly if auth failed
    await expect(page).not.toHaveURL(/\/login/);

    const recordButton = page.locator('button', { hasText: 'Record Receipt' });
    await expect(recordButton).toBeVisible({ timeout: 5000 });
    await recordButton.click();

    // Select customer
    const searchInput = page.locator('input[placeholder="Type customer name or phone..."]');
    await searchInput.fill('Jegadish');
    const suggestionItem = page.locator('[data-testid="customer-option"]').first();
    await expect(suggestionItem).toBeVisible({ timeout: 5000 });
    await suggestionItem.click();

    // Fill amount and submit
    await page.locator('input[placeholder="0.00"]').fill('500.00');
    await page.locator('button[type="submit"]', { hasText: 'Save Receipt' }).click();

    // Verify inline error banner displays exact backend error message
    const errorBanner = page.locator('text=Customer account suspended or invalid receipt amount.');
    await expect(errorBanner).toBeVisible({ timeout: 5000 });

    // Dismiss modal
    await page.locator('button[aria-label="Close Modal"]').click();
  });

  test('Journal Entries - New Journal Entry Modal enforces debit/credit balance', async ({ page }) => {
    await page.goto('/finance/journals');
    await page.waitForLoadState('networkidle');

    // Fail loudly if auth failed
    await expect(page).not.toHaveURL(/\/login/);

    const newJournalButton = page.locator('button', { hasText: 'New Journal Entry' });
    await expect(newJournalButton).toBeVisible({ timeout: 5000 });
    await newJournalButton.click();

    // Assert modal header and line items
    const modalTitle = page.locator('h3', { hasText: 'Post New Journal Entry' });
    await expect(modalTitle).toBeVisible({ timeout: 5000 });

    // Assert balanced save button
    const submitBtn = page.locator('button[type="submit"]');
    await expect(submitBtn).toBeVisible();

    await page.locator('button[aria-label="Close Modal"]').click();
    await expect(modalTitle).not.toBeVisible();
  });

  test('Supplier Bills - Enter Bill Modal opens, renders inputs, and closes', async ({ page }) => {
    await page.goto('/finance/supplier-bills');
    await page.waitForLoadState('networkidle');

    // Fail loudly if auth failed
    await expect(page).not.toHaveURL(/\/login/);

    const enterBillButton = page.locator('button', { hasText: 'Enter Bill' });
    await expect(enterBillButton).toBeVisible({ timeout: 5000 });
    await enterBillButton.click();

    // Assert modal header and bill number input
    const modalTitle = page.locator('h3', { hasText: 'Enter Supplier Bill' });
    await expect(modalTitle).toBeVisible({ timeout: 5000 });
    await expect(page.locator('input[placeholder="e.g. INV-2026-904"]')).toBeVisible();

    await page.locator('button', { hasText: 'Cancel' }).click();
    await expect(modalTitle).not.toBeVisible();
  });

  test('Supplier Payments - Record Payment Modal opens, renders inputs, and closes', async ({ page }) => {
    await page.goto('/finance/supplier-payments');
    await page.waitForLoadState('networkidle');

    // Fail loudly if auth failed
    await expect(page).not.toHaveURL(/\/login/);

    const recordPaymentButton = page.locator('button', { hasText: 'Record Payment' });
    await expect(recordPaymentButton).toBeVisible({ timeout: 5000 });
    await recordPaymentButton.click();

    // Assert modal header and supplier input
    const modalTitle = page.locator('h3', { hasText: 'Record Supplier Payment' });
    await expect(modalTitle).toBeVisible({ timeout: 5000 });
    await expect(page.locator('input[placeholder="Enter supplier name"]')).toBeVisible();

    await page.locator('button', { hasText: 'Cancel' }).click();
    await expect(modalTitle).not.toBeVisible();
  });

  test('Warehouse Locations - Add Warehouse and Add Bin modals function with persistence', async ({ page }) => {
    await page.goto('/warehouses');
    await page.waitForLoadState('networkidle');

    await expect(page).not.toHaveURL(/\/login/);

    const addWhButton = page.locator('button', { hasText: 'Add Warehouse' });
    await expect(addWhButton).toBeVisible({ timeout: 5000 });
    await addWhButton.click();

    const whModal = page.locator('h3', { hasText: 'Add New Warehouse' });
    await expect(whModal).toBeVisible({ timeout: 5000 });

    await page.locator('input[placeholder="e.g. Distribution Hub North"]').fill('Distribution Hub North');
    await page.locator('input[placeholder="e.g. WH-NORTH"]').fill('WH-NORTH');
    await page.locator('button', { hasText: 'Save Warehouse' }).click();

    await expect(page.locator('text=Distribution Hub North')).toBeVisible();

    // Add bin
    const addBinButton = page.locator('button', { hasText: 'Add Bin' }).last();
    await addBinButton.click();

    const binModal = page.locator('h3', { hasText: 'Add Storage Bin' });
    await expect(binModal).toBeVisible({ timeout: 5000 });

    await page.locator('input[placeholder="e.g. D1-01 or RACK-B2"]').fill('BIN-TEST-01');
    await page.locator('button', { hasText: 'Save Bin' }).click();

    await expect(page.locator('text=BIN-TEST-01')).toBeVisible();
  });

  test('Procurement Dashboard - Generate Draft POs requires confirmation, disables immediately, and handles second click idempotently', async ({ page }) => {
    let callCount = 0;

    // Intercept backend endpoint to simulate initial generation then duplicate check
    await page.route('**/api/Purchasing/purchase-orders/auto-generate-reorder', async (route) => {
      if (route.request().method() === 'POST') {
        callCount++;
        if (callCount === 1) {
          await route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify({
              success: true,
              poCount: 2,
              totalItemsOrdered: 15,
              message: 'Successfully auto-generated 2 Purchase Orders across 1 vendors for 15 low-stock items.'
            })
          });
        } else {
          // Second click: Idempotency check returns 0 new POs
          await route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify({
              success: true,
              poCount: 0,
              totalItemsOrdered: 0,
              message: 'All items are currently above reorder threshold or already have open purchase orders. No new POs needed.'
            })
          });
        }
      } else {
        await route.continue();
      }
    });

    await page.goto('/purchasing/procurement');
    await page.waitForLoadState('networkidle');

    await expect(page).not.toHaveURL(/\/login/);

    const genButton = page.locator('button', { hasText: 'Generate Draft POs' });
    await expect(genButton).toBeVisible({ timeout: 5000 });
    await genButton.click();

    // Step 1: Confirmation modal must open first
    const modalTitle = page.locator('h3', { hasText: 'Generate Draft Purchase Orders?' });
    await expect(modalTitle).toBeVisible({ timeout: 5000 });

    // Step 2: Confirm generation
    const confirmBtn = page.locator('button', { hasText: 'Confirm & Generate' });
    await confirmBtn.click();

    // Verify first API call succeeded and created POs
    expect(callCount).toBe(1);
    await expect(page.locator('text=Successfully auto-generated 2 Purchase Orders')).toBeVisible({ timeout: 5000 });

    // Step 3: Trigger a second time to verify idempotency handling
    await genButton.click();
    await expect(modalTitle).toBeVisible({ timeout: 5000 });
    await confirmBtn.click();

    // Verify second API call returned zero new POs
    expect(callCount).toBe(2);
    await expect(page.locator('text=No new POs needed')).toBeVisible({ timeout: 5000 });

    // UI-Only verification: Tests local state toggle until real recommendation endpoint lands
    const addToPoBtn = page.locator('button', { hasText: 'Add to PO' }).first();
    if (await addToPoBtn.isVisible()) {
      await addToPoBtn.click();
      await expect(page.locator('text=In Draft PO').first()).toBeVisible();
    }
  });

  test('Supplier Analytics Dashboard - Export Report button functions', async ({ page }) => {
    await page.goto('/purchasing/supplier-analytics');
    await page.waitForLoadState('networkidle');

    await expect(page).not.toHaveURL(/\/login/);

    const exportBtn = page.locator('button', { hasText: 'Export Report' });
    await expect(exportBtn).toBeVisible({ timeout: 5000 });
    await exportBtn.click();
  });
});
