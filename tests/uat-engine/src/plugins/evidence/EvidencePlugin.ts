import * as fs from 'fs';
import * as path from 'path';
import { IPlugin, IPluginManifest, ITestContext, IEvidenceCollector, IEvidenceStorage } from '../../engine/interfaces';
import { PluginState, EventName } from '../../engine/types';

export class EvidencePlugin implements IPlugin, IEvidenceCollector, IEvidenceStorage {
  public state: PluginState = PluginState.UNINITIALIZED;
  private config: Record<string, any>;
  private outputDir: string;

  constructor(config: Record<string, any>) {
    this.config = config;
    this.outputDir = config.outputDir || path.resolve(process.cwd(), 'artifacts');
  }

  public manifest(): IPluginManifest {
    return {
      name: 'EvidencePlugin',
      version: '1.0.0',
      dependencies: []
    };
  }

  public supportedEvents(): EventName[] {
    return [EventName.EvidenceCaptured];
  }

  public configuration(): Record<string, any> {
    return this.config;
  }

  public async initialize(context: ITestContext): Promise<void> {
    context.di.register('IEvidenceCollector', this);
    context.di.register('IEvidenceStorage', this);
    
    if (!fs.existsSync(this.outputDir)) {
      fs.mkdirSync(this.outputDir, { recursive: true });
    }
  }

  public async healthCheck(): Promise<boolean> {
    try {
      fs.accessSync(this.outputDir, fs.constants.W_OK);
      return true;
    } catch {
      return false;
    }
  }

  public async shutdown(): Promise<void> {}

  // IEvidenceCollector implementation
  public async captureScreenshot(name: string): Promise<string> {
    // In a real scenario, this would orchestrate IBrowser.
    // For now, it returns a placeholder path or orchestrates via DI if injected.
    return path.join(this.outputDir, `${name}.png`);
  }

  public async startTracing(): Promise<void> {}

  public async stopTracing(name: string): Promise<string> {
    return path.join(this.outputDir, `${name}.zip`);
  }

  // IEvidenceStorage implementation
  public async saveArtifact(name: string, data: Buffer | string): Promise<string> {
    const artifactPath = path.join(this.outputDir, name);
    fs.writeFileSync(artifactPath, data);
    return artifactPath;
  }

  public getArtifactUrl(name: string): string {
    return `file://${path.join(this.outputDir, name)}`;
  }
}
