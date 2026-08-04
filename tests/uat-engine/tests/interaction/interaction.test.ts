import { InteractionEngine } from '../../src/interaction/engine/InteractionEngine';
import { ElementId, InteractionEventType } from '../../src/interaction/interfaces';
import { LoginScreen, POSScreen } from '../../src/screens/Screens';

describe('Interaction Engine and Screens', () => {
  let engine: InteractionEngine;
  let mockEventBus: any;
  let eventsPublished: any[];

  beforeEach(() => {
    eventsPublished = [];
    mockEventBus = {
      publish: (name: string, payload: any) => {
        eventsPublished.push(payload);
      }
    };
    engine = new InteractionEngine(mockEventBus, {});
  });

  test('Engine records duration and success events', async () => {
    await engine.setValue(ElementId.LoginUsername, 'admin');
    
    expect(eventsPublished.length).toBe(2);
    expect(eventsPublished[0].type).toBe(InteractionEventType.Started);
    expect(eventsPublished[1].type).toBe(InteractionEventType.Succeeded);
    expect(eventsPublished[1].durationMs).toBeGreaterThanOrEqual(0);
    
    const metrics = engine.getMetrics();
    expect(metrics.executionTimeMs).toBeGreaterThanOrEqual(0);
    expect(metrics.retries).toBe(0);
  });

  test('LoginScreen uses UI Components correctly', async () => {
    const login = new LoginScreen(engine);
    await login.login('cashier', 'pass123');
    
    // Started, Succeeded for Username
    // Started, Succeeded for Password
    // Started, Succeeded for Submit
    expect(eventsPublished.length).toBe(6);
    expect(eventsPublished[5].type).toBe(InteractionEventType.Succeeded);
    expect(eventsPublished[5].action).toBe('submit');
    expect(eventsPublished[5].elementId).toBe(ElementId.LoginSubmit);
  });

  test('POSScreen operations trigger events', async () => {
    const pos = new POSScreen(engine);
    await pos.scanBarcode('123456789');
    
    // SetValue (2 events), Submit (2 events)
    expect(eventsPublished.length).toBe(4);
    
    await pos.openPayment();
    // Open (2 events)
    expect(eventsPublished.length).toBe(6);
  });
});
