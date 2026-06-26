import { Page, Locator } from '@playwright/test';

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
    // POS Terminal actual placeholder: "F2: Scan Barcode or Type Product Name (Press Enter)..."
    this.searchProductInput = page.getByPlaceholder(/Scan Barcode|Product Name/i).first();
    // POS customer search: "F1: Search Customer (Phone/Name)..."
    this.searchCustomerInput = page.getByPlaceholder(/Search Customer|F1:/i).first();
    this.payButton = page.locator('button', { hasText: /Pay|Checkout/i }).first();
    this.cashPaymentTab = page.locator('button', { hasText: /Cash/i }).first();
    this.upiPaymentTab = page.locator('button', { hasText: /UPI/i }).first();
    this.amountTenderedInput = page.getByPlaceholder(/amount|tender/i).first();
    this.confirmPaymentButton = page.locator('button', { hasText: /Confirm Payment|Complete/i }).first();
    this.invoiceSuccessMessage = page.locator('[class*="success"], [class*="invoice"]').first();
  }

  async goto() {
    await this.page.goto('/pos');
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
