import { Page } from '@playwright/test';

export interface IExecutableScenario {
  setPage(page: Page): void;
  execute(): Promise<void>;
}
