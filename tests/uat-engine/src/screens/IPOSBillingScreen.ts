import { Page } from '@playwright/test';

export interface IPOSBillingScreen {
  setPage(page: Page): void;
  searchProduct(productName: string): Promise<void>;
  verifyProductInCart(productName: string): Promise<void>;
  clickPayment(): Promise<void>;
  selectCashPayment(): Promise<void>;
  enterCashAmount(amount: number): Promise<void>;
  confirmPayment(): Promise<void>;
}
