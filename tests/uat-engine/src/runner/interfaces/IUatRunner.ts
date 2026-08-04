import { Page } from '@playwright/test';

export interface IUatRunner {
  start(): Promise<Page>;
  stop(): Promise<void>;
}
