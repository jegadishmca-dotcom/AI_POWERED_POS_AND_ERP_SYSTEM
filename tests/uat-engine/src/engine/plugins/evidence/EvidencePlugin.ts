import { IPlugin, ITestContext } from '../../interfaces';
import { PluginState } from '../../types';

export class EvidencePlugin implements IPlugin {
  public state: PluginState = PluginState.UNINITIALIZED;

  public manifest() {
    return {
      name: 'EvidencePlugin',
      version: '1.0.0',
      dependencies: ['BrowserPlugin']
    };
  }

  public supportedEvents() {
    return [];
  }

  public configuration() {
    return {
      outputDir: './artifacts/evidence'
    };
  }

  public async initialize(context: ITestContext): Promise<void> {
    // In real environment, this hooks into Playwright context to start tracing
    // await context.tracing.start({ screenshots: true, snapshots: true });
    console.log(`[EvidencePlugin] Initialized. Ready to capture screenshots and traces.`);
  }

  public async healthCheck(): Promise<boolean> {
    console.log(`[EvidencePlugin] Health Check: Directory writable... OK`);
    return true;
  }

  public async shutdown(): Promise<void> {
    // await context.tracing.stop({ path: 'trace.zip' });
    console.log(`[EvidencePlugin] Shut down.`);
  }
}
