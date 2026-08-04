import { EventName, LogLevel, PluginState, Scope } from '../types';

export interface IEventBus {
  publish<T>(eventName: EventName, payload: T): void;
  subscribe<T>(eventName: EventName, handler: (payload: T) => void | Promise<void>): void;
  unsubscribe<T>(eventName: EventName, handler: (payload: T) => void | Promise<void>): void;
  replayEvents(): void;
}

export interface ILogger {
  info(message: string, context?: Record<string, any>): void;
  warn(message: string, context?: Record<string, any>): void;
  error(message: string, error?: Error, context?: Record<string, any>): void;
  debug(message: string, context?: Record<string, any>): void;
  trace(message: string, context?: Record<string, any>): void;
  setContext(key: string, value: string): void;
}

export interface IConfig {
  get<T>(key: string, defaultValue?: T): T;
  set<T>(key: string, value: T): void;
  loadFromFile(filePath: string): void;
  loadFromEnv(): void;
}

export interface IDependencyContainer {
  register<T>(token: string | symbol, instance: T): void;
  registerFactory<T>(token: string | symbol, factory: (container: IDependencyContainer) => T, scope?: Scope): void;
  registerClass<T>(token: string | symbol, constructor: { new(...args: any[]): T }, scope?: Scope): void;
  resolve<T>(token: string | symbol): T;
}

export interface IPluginManifest {
  name: string;
  version: string;
  dependencies: string[];
  description?: string;
}

export interface IPlugin {
  manifest(): IPluginManifest;
  supportedEvents(): EventName[];
  configuration(): Record<string, any>;
  state: PluginState;
  
  initialize(context: ITestContext): Promise<void>;
  shutdown(): Promise<void>;
  healthCheck(): Promise<boolean>;
}

export interface ITestContext {
  eventBus: IEventBus;
  logger: ILogger;
  config: IConfig;
  di: IDependencyContainer;
  
  runMetadata: {
    runId: string;
    startTime: number;
    environment: string;
  };
}

// === Infrastructure Adapters ===

export interface IBrowser {
  launch(): Promise<void>;
  close(): Promise<void>;
  newContext(options?: any): Promise<IBrowserContext>;
}

export interface IBrowserContext {
  newPage(): Promise<any>; // Using any for page to keep it abstract for now
  close(): Promise<void>;
}

export interface IHttpClient {
  get<T>(url: string, config?: any): Promise<IHttpResponse<T>>;
  post<T>(url: string, data?: any, config?: any): Promise<IHttpResponse<T>>;
  setAuthToken(token: string): void;
}

export interface IHttpResponse<T> {
  data: T;
  status: number;
  headers: Record<string, string>;
}

export interface IDatabaseSession {
  connect(): Promise<void>;
  disconnect(): Promise<void>;
  query<T>(sql: string, params?: any[]): Promise<T[]>;
  beginTransaction(): Promise<IDatabaseTransaction>;
}

export interface IDatabaseTransaction {
  query<T>(sql: string, params?: any[]): Promise<T[]>;
  commit(): Promise<void>;
  rollback(): Promise<void>;
}

export interface IEvidenceCollector {
  captureScreenshot(name: string): Promise<string>;
  startTracing(): Promise<void>;
  stopTracing(name: string): Promise<string>;
}

export interface IEvidenceStorage {
  saveArtifact(name: string, data: Buffer | string): Promise<string>;
  getArtifactUrl(name: string): string;
}
