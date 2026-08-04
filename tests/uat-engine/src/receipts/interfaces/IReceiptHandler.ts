import { Page } from '@playwright/test';

export interface IReceiptHandler {
  setPage(page: Page): void;
  waitUntilOpened(): Promise<void>;
  validateReceipt(): Promise<void>;
  close(): Promise<void>;
}
