import { chromium, Browser, BrowserContext, Page } from '@playwright/test';
import { IUatRunner } from './interfaces/IUatRunner';
import { IUatRunnerConfig } from './interfaces/IUatRunnerConfig';

export class PlaywrightUatRunner implements IUatRunner {
  private browser: Browser | null = null;
  private context: BrowserContext | null = null;
  private page: Page | null = null;

  constructor(private readonly config: IUatRunnerConfig) {}

  public async start(): Promise<Page> {
    this.browser = await chromium.launch({
      headless: this.config.headless ?? true,
      timeout: this.config.timeoutMs
    });
    
    this.context = await this.browser.newContext();
    this.page = await this.context.newPage();
    
    await this.page.goto(this.config.posUrl);
    
    return this.page;
  }

  public async stop(): Promise<void> {
    if (this.page) await this.page.close();
    if (this.context) await this.context.close();
    if (this.browser) await this.browser.close();
  }
}
