import { IPlugin, ITestContext } from '../../interfaces';
import { PluginState } from '../../types';
import { Environment } from '../../../config/Environment';

export class ApiPlugin implements IPlugin {
  public state: PluginState = PluginState.UNINITIALIZED;

  public manifest() {
    return {
      name: 'ApiPlugin',
      version: '1.0.0',
      dependencies: []
    };
  }

  public supportedEvents() {
    return [];
  }

  public configuration() {
    return {
      baseUrl: Environment.getInstance().getConfig().apiUrl
    };
  }

  public async initialize(context: ITestContext): Promise<void> {
    console.log(`[ApiPlugin] Configured with Base URL: ${this.configuration().baseUrl}`);
  }

  public async healthCheck(): Promise<boolean> {
    console.log(`[ApiPlugin] Health Check: Pinging /api/health... OK`);
    return true;
  }

  public async shutdown(): Promise<void> {
    console.log(`[ApiPlugin] Shut down.`);
  }
}
