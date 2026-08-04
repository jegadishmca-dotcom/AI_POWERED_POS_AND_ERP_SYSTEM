import { IPlugin, IPluginManifest, ITestContext, IBrowser, IBrowserContext } from '../../engine/interfaces';
import { PluginState, EventName } from '../../engine/types';

export class MockBrowserContext implements IBrowserContext {
  public async newPage(): Promise<any> {
    return { isMockPage: true };
  }
  public async close(): Promise<void> {}
}

export class MockBrowserPlugin implements IPlugin, IBrowser {
  public state: PluginState = PluginState.UNINITIALIZED;
  private initialized = false;

  public manifest(): IPluginManifest {
    return {
      name: 'BrowserPlugin',
      version: '1.0.0-mock',
      dependencies: []
    };
  }

  public supportedEvents(): EventName[] {
    return [];
  }

  public configuration(): Record<string, any> {
    return { headless: true };
  }

  public async initialize(context: ITestContext): Promise<void> {
    this.initialized = true;
    context.di.register('IBrowser', this);
  }

  public async healthCheck(): Promise<boolean> {
    return this.initialized;
  }

  public async shutdown(): Promise<void> {
    this.initialized = false;
  }

  // IBrowser implementation
  public async launch(): Promise<void> {}
  public async close(): Promise<void> {}
  public async newContext(options?: any): Promise<IBrowserContext> {
    return new MockBrowserContext();
  }
}
