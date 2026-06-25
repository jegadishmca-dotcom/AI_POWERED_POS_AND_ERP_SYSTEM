import { Page, Locator } from '@playwright/test';

export class LoyaltyPage {
  readonly page: Page;
  readonly searchCustomerInput: Locator;
  readonly redeemPointsButton: Locator;
  readonly pointsInput: Locator;
  readonly confirmRedemptionButton: Locator;

  constructor(page: Page) {
    this.page = page;
    this.searchCustomerInput = page.getByPlaceholder(/search customer/i).first();
    this.redeemPointsButton = page.locator('button', { hasText: /Redeem Points/i }).first();
    this.pointsInput = page.getByPlaceholder(/points/i).first();
    this.confirmRedemptionButton = page.locator('button', { hasText: /Confirm/i }).first();
  }

  async goto() {
    await this.page.goto('/crm/loyalty');
  }

  async redeemPoints(points: string) {
    await this.redeemPointsButton.click();
    await this.pointsInput.fill(points);
    await this.confirmRedemptionButton.click();
  }
}
