const { InteractionEngine } = require('./dist/src/interaction/engine/InteractionEngine');
const { ElementId, InteractionEventType } = require('./dist/src/interaction/interfaces');
const { LoginScreen, POSScreen } = require('./dist/src/screens/Screens');

async function runTests() {
  console.log('Running Interaction Engine tests...');
  
  const eventsPublished = [];
  const mockEventBus = {
    publish: (name, payload) => {
      eventsPublished.push(payload);
    }
  };
  
  const engine = new InteractionEngine(mockEventBus, {});

  await engine.setValue(ElementId.LoginUsername, 'admin');
  if (eventsPublished.length !== 2) throw new Error('Failed to record engine events');
  if (eventsPublished[1].type !== InteractionEventType.Succeeded) throw new Error('Failed to record Succeeded event');

  const metrics = engine.getMetrics();
  if (metrics.executionTimeMs === undefined) throw new Error('Failed to record metrics');

  const login = new LoginScreen(engine);
  await login.login('cashier', 'pass123');
  
  if (eventsPublished.length !== 8) throw new Error('Failed to record composite screen events');
  
  const pos = new POSScreen(engine);
  await pos.scanBarcode('123456789');
  
  if (eventsPublished.length !== 12) throw new Error('Failed to record POS screen events');

  console.log('Interaction Engine and Screens tests passed successfully!');
}

runTests().catch(e => {
  console.error(e);
  process.exit(1);
});
