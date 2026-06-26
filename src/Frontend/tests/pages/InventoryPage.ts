import { Page, Locator } from '@playwright/test';

export class InventoryPage {
  readonly page: Page;
  readonly searchInput: Locator;
  readonly productList: Locator;
  readonly batchDetailsButton: Locator;

  constructor(page: Page) {
    this.page = page;
    // StockPositionReport.tsx actual placeholder: "Search Product Code or Name..."
    this.searchInput = page.getByPlaceholder(/Search Product|Product Code/i).first();
    this.productList = page.locator('table, .inventory-list, .stock-list').first();
    this.batchDetailsButton = page.locator('button', { hasText: /Batches|Details|Batch/i }).first();
  }

  async goto() {
    await this.page.goto('/inventory');
  }

  async searchProduct(query: string) {
    await this.searchInput.fill(query);
    await this.page.keyboard.press('Enter');
  }

  async viewBatches() {
    await this.batchDetailsButton.click();
  }
}
