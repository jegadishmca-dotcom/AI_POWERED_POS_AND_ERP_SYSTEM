import { IPlugin, ITestContext, ILogger, IEventBus } from '../interfaces';
import { PluginException } from '../exceptions';
import { PluginState, EventName } from '../types';

export class PluginLoader {
  private plugins: Map<string, IPlugin> = new Map();
  
  constructor(
    private logger: ILogger,
    private eventBus: IEventBus
  ) {}
  
  public registerPlugin(plugin: IPlugin): void {
    const name = plugin.manifest().name;
    if (this.plugins.has(name)) {
      this.logger.warn(`Plugin ${name} is already registered. Overwriting.`);
    }
    this.plugins.set(name, plugin);
    this.logger.info(`Registered plugin: ${name} (v${plugin.manifest().version})`);
  }
  
  private isDependencyMet(depName: string): boolean {
    const dep = this.plugins.get(depName);
    return dep !== undefined && dep.state === PluginState.READY;
  }
  
  private checkDependencies(plugin: IPlugin): { ok: boolean, missing: string[] } {
    const missing: string[] = [];
    for (const dep of plugin.manifest().dependencies) {
      if (!this.isDependencyMet(dep)) {
        missing.push(dep);
      }
    }
    return { ok: missing.length === 0, missing };
  }
  
  public async initializePlugins(context: ITestContext): Promise<void> {
    // Basic iterative initialization logic.
    // In a complex graph, a topological sort is required.
    let remaining = Array.from(this.plugins.values());
    let progress = true;
    
    while (remaining.length > 0 && progress) {
      progress = false;
      const nextBatch = [];
      
      for (const plugin of remaining) {
        const deps = this.checkDependencies(plugin);
        const name = plugin.manifest().name;
        
        if (deps.ok) {
          try {
            plugin.state = PluginState.INITIALIZING;
            this.logger.info(`Initializing plugin: ${name}`);
            
            // Register plugin-specific configuration into DI or context if needed
            const pConfig = plugin.configuration();
            this.logger.debug(`Applying config for ${name}`, pConfig);
            
            await plugin.initialize(context);
            
            plugin.state = PluginState.READY;
            this.eventBus.publish(EventName.PluginLoaded, {
              timestamp: Date.now(),
              pluginName: name,
              version: plugin.manifest().version
            });
            progress = true;
          } catch (error) {
            plugin.state = PluginState.ERROR;
            const err = error instanceof Error ? error : new Error(String(error));
            this.logger.error(`Failed to initialize ${name}`, err);
            this.eventBus.publish(EventName.PluginFailed, {
              timestamp: Date.now(),
              pluginName: name,
              error: err
            });
            // We do not throw immediately to allow other independent plugins to initialize or block gracefully
          }
        } else {
          nextBatch.push(plugin);
        }
      }
      remaining = nextBatch;
    }
    
    // Any remaining plugins are BLOCKED due to circular or missing/failed dependencies
    if (remaining.length > 0) {
      for (const blockedPlugin of remaining) {
        const name = blockedPlugin.manifest().name;
        const missing = this.checkDependencies(blockedPlugin).missing;
        blockedPlugin.state = PluginState.BLOCKED;
        this.logger.warn(`Plugin ${name} is BLOCKED. Missing or failed dependencies: ${missing.join(', ')}`);
      }
    }
  }
  
  public async performHealthChecks(): Promise<boolean> {
    let allHealthy = true;
    for (const [name, plugin] of this.plugins.entries()) {
      if (plugin.state === PluginState.BLOCKED || plugin.state === PluginState.ERROR) {
        this.logger.error(`Skipping health check for ${name} (State: ${plugin.state})`);
        allHealthy = false;
        continue;
      }
      
      try {
        const isHealthy = await plugin.healthCheck();
        if (!isHealthy) {
          this.logger.error(`Health check failed for plugin: ${name}`);
          allHealthy = false;
        }
      } catch (error) {
        this.logger.error(`Health check threw error for plugin: ${name}`, error instanceof Error ? error : undefined);
        allHealthy = false;
      }
    }
    return allHealthy;
  }
  
  public async teardownPlugins(): Promise<void> {
    const reversedPlugins = Array.from(this.plugins.entries()).reverse();
    for (const [name, plugin] of reversedPlugins) {
      if (plugin.state === PluginState.READY) {
        try {
          this.logger.info(`Shutting down plugin: ${name}`);
          await plugin.shutdown();
          plugin.state = PluginState.UNINITIALIZED;
        } catch (error) {
          this.logger.error(`Error during shutdown of plugin: ${name}`, error instanceof Error ? error : undefined);
        }
      }
    }
  }
}
