const { PluginLoader } = require('./dist/src/engine/plugins/PluginLoader');
const { EventBus } = require('./dist/src/engine/events/EventBus');
const { StructuredLogger } = require('./dist/src/engine/logging/StructuredLogger');
const { DependencyContainer } = require('./dist/src/engine/di/Container');
const { TestContext } = require('./dist/src/engine/context/TestContext');
const { ConfigurationManager } = require('./dist/src/engine/config/ConfigurationManager');
const { PluginState } = require('./dist/src/engine/types');

const { MockBrowserPlugin } = require('./dist/src/plugins/mocks/MockBrowserPlugin');
const { MockDatabasePlugin } = require('./dist/src/plugins/mocks/MockDatabasePlugin');
const { MockApiPlugin } = require('./dist/src/plugins/mocks/MockApiPlugin');
const { MockEvidencePlugin } = require('./dist/src/plugins/mocks/MockEvidencePlugin');

async function runContractTests() {
  console.log('Running contract tests...');
  
  const eventBus = new EventBus();
  const logger = new StructuredLogger(0);
  const di = new DependencyContainer();
  const config = new ConfigurationManager();
  const loader = new PluginLoader(logger, eventBus);
  
  const context = new TestContext(eventBus, logger, config, di, {
    runId: 'contract-test',
    startTime: Date.now(),
    environment: 'test'
  });

  const browserPlugin = new MockBrowserPlugin();
  const dbPlugin = new MockDatabasePlugin();
  const apiPlugin = new MockApiPlugin();
  const evidencePlugin = new MockEvidencePlugin();

  loader.registerPlugin(browserPlugin);
  loader.registerPlugin(dbPlugin);
  loader.registerPlugin(apiPlugin);
  loader.registerPlugin(evidencePlugin);

  await loader.initializePlugins(context);
  
  const healthy = await loader.performHealthChecks();
  if (!healthy) throw new Error('Health checks failed');

  if (browserPlugin.state !== PluginState.READY) throw new Error('Browser not ready');
  
  const b = di.resolve('IBrowser');
  if (b !== browserPlugin) throw new Error('DI failed for IBrowser');

  await loader.teardownPlugins();
  if (browserPlugin.state !== PluginState.UNINITIALIZED) throw new Error('Shutdown failed');

  console.log('Contract tests passed successfully!');
}

runContractTests().catch(e => {
  console.error(e);
  process.exit(1);
});
