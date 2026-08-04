import { IPlugin, IPluginManifest, ITestContext, IDatabaseSession, IDatabaseTransaction } from '../../engine/interfaces';
import { PluginState, EventName } from '../../engine/types';

export class MockDatabaseTransaction implements IDatabaseTransaction {
  public async query<T>(sql: string, params?: any[]): Promise<T[]> {
    return [];
  }
  public async commit(): Promise<void> {}
  public async rollback(): Promise<void> {}
}

export class MockDatabasePlugin implements IPlugin, IDatabaseSession {
  public state: PluginState = PluginState.UNINITIALIZED;
  private initialized = false;

  public manifest(): IPluginManifest {
    return {
      name: 'DatabasePlugin',
      version: '1.0.0-mock',
      dependencies: []
    };
  }

  public supportedEvents(): EventName[] {
    return [EventName.DatabaseValidated];
  }

  public configuration(): Record<string, any> {
    return { host: 'localhost' };
  }

  public async initialize(context: ITestContext): Promise<void> {
    this.initialized = true;
    context.di.register('IDatabaseSession', this);
  }

  public async healthCheck(): Promise<boolean> {
    return this.initialized;
  }

  public async shutdown(): Promise<void> {
    this.initialized = false;
  }

  // IDatabaseSession implementation
  public async connect(): Promise<void> {}
  public async disconnect(): Promise<void> {}
  public async query<T>(sql: string, params?: any[]): Promise<T[]> {
    return [];
  }
  public async beginTransaction(): Promise<IDatabaseTransaction> {
    return new MockDatabaseTransaction();
  }
}
