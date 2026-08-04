import { EventBus } from '../../src/engine/events/EventBus';
import { EventName } from '../../src/engine/types';

describe('EventBus', () => {
  let eventBus: EventBus;

  beforeEach(() => {
    eventBus = new EventBus();
  });

  test('should subscribe and receive events synchronously (mocked execution)', () => {
    const handler = jest.fn();
    eventBus.subscribe(EventName.ScenarioStarted, handler);
    
    eventBus.publish(EventName.ScenarioStarted, { timestamp: 123, scenarioId: 'TEST-1' });
    
    expect(handler).toHaveBeenCalledWith({ timestamp: 123, scenarioId: 'TEST-1' });
  });

  test('should support event replay', () => {
    const handler1 = jest.fn();
    const handler2 = jest.fn();
    
    eventBus.publish(EventName.ScenarioStarted, { timestamp: 1, scenarioId: 'A' });
    
    eventBus.subscribe(EventName.ScenarioStarted, handler1);
    eventBus.replayEvents();
    
    expect(handler1).toHaveBeenCalledTimes(1);
  });
});
