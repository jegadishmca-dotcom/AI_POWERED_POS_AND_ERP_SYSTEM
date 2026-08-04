import { Pool, PoolClient } from 'pg';
import { IPlugin, IPluginManifest, ITestContext, IDatabaseSession, IDatabaseTransaction } from '../../engine/interfaces';
import { PluginState, EventName } from '../../engine/types';

export class PostgresTransaction implements IDatabaseTransaction {
  constructor(private client: PoolClient) {}

  public async query<T>(sql: string, params?: any[]): Promise<T[]> {
    const result = await this.client.query(sql, params);
    return result.rows as T[];
  }

  public async commit(): Promise<void> {
    await this.client.query('COMMIT');
    this.client.release();
  }

  public async rollback(): Promise<void> {
    await this.client.query('ROLLBACK');
    this.client.release();
  }
}

export class DatabasePlugin implements IPlugin, IDatabaseSession {
  public state: PluginState = PluginState.UNINITIALIZED;
  private pool: Pool | null = null;
  private config: Record<string, any>;

  constructor(config: Record<string, any>) {
    this.config = config; // Expect connectionString or host, user, port, etc.
  }

  public manifest(): IPluginManifest {
    return {
      name: 'DatabasePlugin',
      version: '1.0.0',
      dependencies: []
    };
  }

  public supportedEvents(): EventName[] {
    return [EventName.DatabaseValidated];
  }

  public configuration(): Record<string, any> {
    return this.config;
  }

  public async initialize(context: ITestContext): Promise<void> {
    context.di.register('IDatabaseSession', this);
    await this.connect();
  }

  public async healthCheck(): Promise<boolean> {
    if (!this.pool) return false;
    try {
      const res = await this.pool.query('SELECT 1 as alive');
      return res.rows[0].alive === 1;
    } catch {
      return false;
    }
  }

  public async shutdown(): Promise<void> {
    await this.disconnect();
  }

  // IDatabaseSession implementation
  public async connect(): Promise<void> {
    if (!this.pool) {
      this.pool = new Pool(this.config);
    }
  }

  public async disconnect(): Promise<void> {
    if (this.pool) {
      await this.pool.end();
      this.pool = null;
    }
  }

  public async query<T>(sql: string, params?: any[]): Promise<T[]> {
    if (!this.pool) throw new Error('Database not connected.');
    const result = await this.pool.query(sql, params);
    return result.rows as T[];
  }

  public async beginTransaction(): Promise<IDatabaseTransaction> {
    if (!this.pool) throw new Error('Database not connected.');
    const client = await this.pool.connect();
    await client.query('BEGIN');
    return new PostgresTransaction(client);
  }
}
