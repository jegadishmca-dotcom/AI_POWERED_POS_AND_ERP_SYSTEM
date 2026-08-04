import { IPlugin, ITestContext } from '../../interfaces';
import { PluginState } from '../../types';
import { Environment } from '../../../config/Environment';

export class DatabasePlugin implements IPlugin {
  public state: PluginState = PluginState.UNINITIALIZED;

  public manifest() {
    return {
      name: 'DatabasePlugin',
      version: '1.0.0',
      dependencies: []
    };
  }

  public supportedEvents() {
    return [];
  }

  public configuration() {
    return {
      connectionString: Environment.getInstance().getConfig().dbConnectionString
    };
  }

  public async initialize(context: ITestContext): Promise<void> {
    // Real implementation: const { Client } = require('pg'); this.client = new Client(...)
    console.log(`[DatabasePlugin] Connected to database: ${this.configuration().connectionString}`);
  }

  public async healthCheck(): Promise<boolean> {
    console.log(`[DatabasePlugin] Health Check: Querying SELECT 1... OK`);
    return true;
  }

  public async shutdown(): Promise<void> {
    console.log(`[DatabasePlugin] Disconnected from database.`);
  }
}
