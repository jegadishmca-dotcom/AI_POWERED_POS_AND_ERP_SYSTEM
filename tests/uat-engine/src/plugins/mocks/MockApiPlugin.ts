import { IPlugin, IPluginManifest, ITestContext, IHttpClient, IHttpResponse } from '../../engine/interfaces';
import { PluginState, EventName } from '../../engine/types';

export class MockApiPlugin implements IPlugin, IHttpClient {
  public state: PluginState = PluginState.UNINITIALIZED;
  private initialized = false;

  public manifest(): IPluginManifest {
    return {
      name: 'ApiPlugin',
      version: '1.0.0-mock',
      dependencies: []
    };
  }

  public supportedEvents(): EventName[] {
    return [EventName.ApiCalled];
  }

  public configuration(): Record<string, any> {
    return { baseUrl: 'http://localhost' };
  }

  public async initialize(context: ITestContext): Promise<void> {
    this.initialized = true;
    context.di.register('IHttpClient', this);
  }

  public async healthCheck(): Promise<boolean> {
    return this.initialized;
  }

  public async shutdown(): Promise<void> {
    this.initialized = false;
  }

  // IHttpClient implementation
  public async get<T>(url: string, config?: any): Promise<IHttpResponse<T>> {
    return { data: {} as T, status: 200, headers: {} };
  }
  public async post<T>(url: string, data?: any, config?: any): Promise<IHttpResponse<T>> {
    return { data: {} as T, status: 200, headers: {} };
  }
  public setAuthToken(token: string): void {}
}
