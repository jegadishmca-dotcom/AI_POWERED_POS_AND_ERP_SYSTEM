import { Page, Locator } from '@playwright/test';

export class ReportsPage {
  readonly page: Page;
  readonly salesReportTab: Locator;
  readonly inventoryReportTab: Locator;
  readonly exportButton: Locator;
  readonly printButton: Locator;

  constructor(page: Page) {
    this.page = page;
    this.salesReportTab = page.locator('button, a', { hasText: /Sales Report/i }).first();
    this.inventoryReportTab = page.locator('button, a', { hasText: /Inventory Report/i }).first();
    this.exportButton = page.locator('button', { hasText: /Export/i }).first();
    this.printButton = page.locator('button', { hasText: /Print/i }).first();
  }

  async goto() {
    await this.page.goto('/analytics/loss-prevention'); // Typical reports dashboard endpoint in this app
  }

  async viewSalesReport() {
    if (await this.salesReportTab.isVisible()) {
      await this.salesReportTab.click();
    }
  }

  async exportReport() {
    if (await this.exportButton.isVisible()) {
      await this.exportButton.click();
    }
  }
}
