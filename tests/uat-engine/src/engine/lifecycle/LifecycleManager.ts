import { IEventBus, ILogger, IConfig, IDependencyContainer, ITestContext } from '../interfaces';
import { PluginLoader } from '../plugins/PluginLoader';
import { TelemetryFramework } from '../telemetry/TelemetryFramework';
import { EngineException } from '../exceptions';
import { TestContext } from '../context/TestContext';

export class LifecycleManager {
  public context!: ITestContext;
  
  constructor(
    private readonly di: IDependencyContainer,
    private readonly eventBus: IEventBus,
    private readonly logger: ILogger,
    private readonly config: IConfig,
    private readonly pluginLoader: PluginLoader,
    private readonly telemetry: TelemetryFramework
  ) {}

  public async boot(runId: string, environment: string): Promise<ITestContext> {
    this.logger.setContext('runId', runId);
    this.logger.info(`Engine booting up for run: ${runId}`);
    
    this.telemetry.startMeasurement('engine_boot');
    
    try {
      this.context = new TestContext(
        this.eventBus,
        this.logger,
        this.config,
        this.di,
        {
          runId,
          startTime: Date.now(),
          environment
        }
      );
      
      this.logger.info('Initializing plugins...');
      await this.pluginLoader.initializePlugins(this.context);
      
      this.logger.info('Performing plugin health checks...');
      const healthy = await this.pluginLoader.performHealthChecks();
      
      if (!healthy) {
        throw new EngineException('One or more plugins failed health checks or were blocked during boot.');
      }
      
      this.telemetry.endMeasurement('engine_boot');
      this.telemetry.recordMemoryUsage('after_boot');
      
      this.logger.info('Engine boot complete.');
      return this.context;
      
    } catch (error) {
      this.logger.error('Fatal error during engine boot', error instanceof Error ? error : undefined);
      throw error;
    }
  }

  public async teardown(): Promise<void> {
    this.logger.info('Engine teardown initiated...');
    this.telemetry.startMeasurement('engine_teardown');
    
    try {
      await this.pluginLoader.teardownPlugins();
      
      this.telemetry.endMeasurement('engine_teardown');
      this.telemetry.recordMemoryUsage('after_teardown');
      
      const report = this.telemetry.getReport();
      this.logger.info('Teardown complete. Telemetry Report:', report);
    } catch (error) {
      this.logger.error('Error during engine teardown', error instanceof Error ? error : undefined);
    }
  }
}
