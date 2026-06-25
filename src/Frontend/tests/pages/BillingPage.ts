import { Page, Locator, expect } from '@playwright/test';

export class BillingPage {
  readonly page: Page;
  readonly searchProductInput: Locator;
  readonly searchCustomerInput: Locator;
  readonly payButton: Locator;
  readonly cashPaymentTab: Locator;
  readonly upiPaymentTab: Locator;
  readonly amountTenderedInput: Locator;
  readonly confirmPaymentButton: Locator;
  readonly invoiceSuccessMessage: Locator;
  
  constructor(page: Page) {
    this.page = page;
    // We locate by placeholders assuming they exist or by accessible roles
    this.searchProductInput = page.getByPlaceholder(/search product|barcode/i).first();
    this.searchCustomerInput = page.getByPlaceholder(/search customer/i).first();
    this.payButton = page.locator('button', { hasText: /Pay|Checkout/i }).first();
    this.cashPaymentTab = page.locator('button', { hasText: /Cash/i }).first();
    this.upiPaymentTab = page.locator('button', { hasText: /UPI/i }).first();
    this.amountTenderedInput = page.getByPlaceholder(/amount/i).first();
    this.confirmPaymentButton = page.locator('button', { hasText: /Confirm Payment|Complete/i }).first();
    this.invoiceSuccessMessage = page.locator('text=/Invoice.*generated successfully|Payment successful/i');
  }

  async goto() {
    // Navigating to the dashboard/pos endpoint
    await this.page.goto('/');
  }

  async searchAndAddProduct(productCode: string) {
    await this.searchProductInput.fill(productCode);
    await this.page.keyboard.press('Enter');
    // Wait for product to be added to cart
    await this.page.waitForTimeout(500); // Simulate network or processing
  }

  async completeCashPayment(amountStr: string) {
    await this.payButton.click();
    await this.cashPaymentTab.click();
    if (await this.amountTenderedInput.isVisible()) {
      await this.amountTenderedInput.fill(amountStr);
    }
    await this.confirmPaymentButton.click();
  }
}
