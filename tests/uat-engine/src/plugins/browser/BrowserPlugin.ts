import { chromium, Browser, BrowserContext } from 'playwright';
import { IPlugin, IPluginManifest, ITestContext, IBrowser, IBrowserContext } from '../../engine/interfaces';
import { PluginState, EventName } from '../../engine/types';

export class PlaywrightBrowserContext implements IBrowserContext {
  constructor(private context: BrowserContext) {}

  public async newPage(): Promise<any> {
    return this.context.newPage();
  }

  public async close(): Promise<void> {
    await this.context.close();
  }
}

export class BrowserPlugin implements IPlugin, IBrowser {
  public state: PluginState = PluginState.UNINITIALIZED;
  private browser: Browser | null = null;
  private config: Record<string, any>;

  constructor(config: Record<string, any> = { headless: true }) {
    this.config = config;
  }

  public manifest(): IPluginManifest {
    return {
      name: 'BrowserPlugin',
      version: '1.0.0',
      dependencies: []
    };
  }

  public supportedEvents(): EventName[] {
    return []; // Could emit specific browser events if needed
  }

  public configuration(): Record<string, any> {
    return this.config;
  }

  public async initialize(context: ITestContext): Promise<void> {
    context.di.register('IBrowser', this);
    await this.launch();
  }

  public async healthCheck(): Promise<boolean> {
    return this.browser !== null && this.browser.isConnected();
  }

  public async shutdown(): Promise<void> {
    await this.close();
  }

  // IBrowser implementation
  public async launch(): Promise<void> {
    if (!this.browser) {
      this.browser = await chromium.launch({ headless: this.config.headless });
    }
  }

  public async close(): Promise<void> {
    if (this.browser) {
      await this.browser.close();
      this.browser = null;
    }
  }

  public async newContext(options?: any): Promise<IBrowserContext> {
    if (!this.browser) {
      throw new Error('Browser is not launched.');
    }
    const context = await this.browser.newContext(options);
    return new PlaywrightBrowserContext(context);
  }
}
