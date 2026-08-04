import axios, { AxiosInstance } from 'axios';
import { IPlugin, IPluginManifest, ITestContext, IHttpClient, IHttpResponse, IEventBus } from '../../engine/interfaces';
import { PluginState, EventName } from '../../engine/types';

export class ApiPlugin implements IPlugin, IHttpClient {
  public state: PluginState = PluginState.UNINITIALIZED;
  private client: AxiosInstance;
  private config: Record<string, any>;
  private eventBus!: IEventBus;

  constructor(config: Record<string, any>) {
    this.config = config; // Ex: { baseURL: 'http://192.168.1.5:8000' }
    this.client = axios.create(config);
  }

  public manifest(): IPluginManifest {
    return {
      name: 'ApiPlugin',
      version: '1.0.0',
      dependencies: []
    };
  }

  public supportedEvents(): EventName[] {
    return [EventName.ApiCalled];
  }

  public configuration(): Record<string, any> {
    return this.config;
  }

  public async initialize(context: ITestContext): Promise<void> {
    this.eventBus = context.eventBus;
    context.di.register('IHttpClient', this);
    
    // Add interceptors for emitting events
    this.client.interceptors.response.use(
      (response) => {
        this.emitApiEvent(response.config.url || '', response.config.method || 'unknown', response.status, 0); // Simplified duration
        return response;
      },
      (error) => {
        if (error.response) {
          this.emitApiEvent(error.config.url || '', error.config.method || 'unknown', error.response.status, 0);
        }
        return Promise.reject(error);
      }
    );
  }

  private emitApiEvent(url: string, method: string, statusCode: number, durationMs: number) {
    this.eventBus.publish(EventName.ApiCalled, {
      timestamp: Date.now(),
      url,
      method: method.toUpperCase(),
      statusCode,
      durationMs
    });
  }

  public async healthCheck(): Promise<boolean> {
    return true; // We could attempt a ping to baseURL if defined
  }

  public async shutdown(): Promise<void> {
    // Axios doesn't require explicit shutdown
  }

  // IHttpClient implementation
  public setAuthToken(token: string): void {
    this.client.defaults.headers.common['Authorization'] = `Bearer ${token}`;
  }

  public async get<T>(url: string, config?: any): Promise<IHttpResponse<T>> {
    const response = await this.client.get<T>(url, config);
    return {
      data: response.data,
      status: response.status,
      headers: response.headers as Record<string, string>
    };
  }

  public async post<T>(url: string, data?: any, config?: any): Promise<IHttpResponse<T>> {
    const response = await this.client.post<T>(url, data, config);
    return {
      data: response.data,
      status: response.status,
      headers: response.headers as Record<string, string>
    };
  }
}
