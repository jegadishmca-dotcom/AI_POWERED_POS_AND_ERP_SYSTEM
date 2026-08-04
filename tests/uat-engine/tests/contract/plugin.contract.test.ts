import { PluginLoader } from '../../src/engine/plugins/PluginLoader';
import { EventBus } from '../../src/engine/events/EventBus';
import { StructuredLogger } from '../../src/engine/logging/StructuredLogger';
import { DependencyContainer } from '../../src/engine/di/Container';
import { TestContext } from '../../src/engine/context/TestContext';
import { ConfigurationManager } from '../../src/engine/config/ConfigurationManager';
import { PluginState } from '../../src/engine/types';

import { MockBrowserPlugin } from '../../src/plugins/mocks/MockBrowserPlugin';
import { MockDatabasePlugin } from '../../src/plugins/mocks/MockDatabasePlugin';
import { MockApiPlugin } from '../../src/plugins/mocks/MockApiPlugin';
import { MockEvidencePlugin } from '../../src/plugins/mocks/MockEvidencePlugin';

describe('Plugin Lifecycle Contract Tests', () => {
  let eventBus: EventBus;
  let logger: StructuredLogger;
  let di: DependencyContainer;
  let config: ConfigurationManager;
  let context: TestContext;
  let loader: PluginLoader;

  beforeEach(() => {
    eventBus = new EventBus();
    logger = new StructuredLogger();
    di = new DependencyContainer();
    config = new ConfigurationManager();
    loader = new PluginLoader(logger, eventBus);
    
    context = new TestContext(eventBus, logger, config, di, {
      runId: 'contract-test',
      startTime: Date.now(),
      environment: 'test'
    });
  });

  test('Mocks should initialize successfully and transition states', async () => {
    const browserPlugin = new MockBrowserPlugin();
    const dbPlugin = new MockDatabasePlugin();
    const apiPlugin = new MockApiPlugin();
    const evidencePlugin = new MockEvidencePlugin();

    loader.registerPlugin(browserPlugin);
    loader.registerPlugin(dbPlugin);
    loader.registerPlugin(apiPlugin);
    loader.registerPlugin(evidencePlugin);

    expect(browserPlugin.state).toBe(PluginState.UNINITIALIZED);

    await loader.initializePlugins(context);

    expect(browserPlugin.state).toBe(PluginState.READY);
    expect(dbPlugin.state).toBe(PluginState.READY);
    expect(apiPlugin.state).toBe(PluginState.READY);
    expect(evidencePlugin.state).toBe(PluginState.READY);

    // Verify DI registration happened via mock plugins
    expect(context.di.resolve('IBrowser')).toBe(browserPlugin);
    expect(context.di.resolve('IDatabaseSession')).toBe(dbPlugin);
  });

  test('Health check should pass when initialized', async () => {
    const browserPlugin = new MockBrowserPlugin();
    loader.registerPlugin(browserPlugin);
    await loader.initializePlugins(context);
    
    const isHealthy = await loader.performHealthChecks();
    expect(isHealthy).toBe(true);
  });

  test('Shutdown should reset state', async () => {
    const browserPlugin = new MockBrowserPlugin();
    loader.registerPlugin(browserPlugin);
    await loader.initializePlugins(context);
    
    await loader.teardownPlugins();
    expect(browserPlugin.state).toBe(PluginState.UNINITIALIZED);
  });

  test('PluginLoader should correctly mark plugins as BLOCKED if dependencies are missing', async () => {
    const evidencePlugin = new MockEvidencePlugin();
    // Overriding manifest for test to require a missing dependency
    jest.spyOn(evidencePlugin, 'manifest').mockReturnValue({
      name: 'EvidencePlugin',
      version: '1.0',
      dependencies: ['MissingPlugin']
    });

    loader.registerPlugin(evidencePlugin);
    await loader.initializePlugins(context);

    expect(evidencePlugin.state).toBe(PluginState.BLOCKED);
  });
});
