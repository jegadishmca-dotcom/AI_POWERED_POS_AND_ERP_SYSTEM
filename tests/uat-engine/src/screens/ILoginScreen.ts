import { Page } from '@playwright/test';

export interface ILoginScreen {
  setPage(page: Page): void;
  enterUsername(username: string): Promise<void>;
  enterPassword(password: string): Promise<void>;
  clickLogin(): Promise<void>;
  waitUntilLoggedIn(): Promise<void>;
}
