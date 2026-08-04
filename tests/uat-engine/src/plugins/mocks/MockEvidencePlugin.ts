import { IPlugin, IPluginManifest, ITestContext, IEvidenceCollector, IEvidenceStorage } from '../../engine/interfaces';
import { PluginState, EventName } from '../../engine/types';

export class MockEvidencePlugin implements IPlugin, IEvidenceCollector, IEvidenceStorage {
  public state: PluginState = PluginState.UNINITIALIZED;
  private initialized = false;

  public manifest(): IPluginManifest {
    return {
      name: 'EvidencePlugin',
      version: '1.0.0-mock',
      dependencies: [] // Note: A real evidence plugin might depend on BrowserPlugin for traces
    };
  }

  public supportedEvents(): EventName[] {
    return [EventName.EvidenceCaptured];
  }

  public configuration(): Record<string, any> {
    return { outputDir: './artifacts' };
  }

  public async initialize(context: ITestContext): Promise<void> {
    this.initialized = true;
    context.di.register('IEvidenceCollector', this);
    context.di.register('IEvidenceStorage', this);
  }

  public async healthCheck(): Promise<boolean> {
    return this.initialized;
  }

  public async shutdown(): Promise<void> {
    this.initialized = false;
  }

  // IEvidenceCollector implementation
  public async captureScreenshot(name: string): Promise<string> {
    return `/mock/screenshot/${name}.png`;
  }
  public async startTracing(): Promise<void> {}
  public async stopTracing(name: string): Promise<string> {
    return `/mock/trace/${name}.zip`;
  }

  // IEvidenceStorage implementation
  public async saveArtifact(name: string, data: Buffer | string): Promise<string> {
    return `/mock/artifact/${name}`;
  }
  public getArtifactUrl(name: string): string {
    return `http://localhost/artifacts/${name}`;
  }
}
