import { IEventBus } from '../interfaces';
import { EventName } from '../types';

type EventHandler = (payload: any) => void | Promise<void>;

interface RecordedEvent {
  eventName: EventName;
  payload: any;
  timestamp: number;
}

export class EventBus implements IEventBus {
  private handlers: Map<EventName, Set<EventHandler>> = new Map();
  private eventHistory: RecordedEvent[] = [];
  
  public publish<T>(eventName: EventName, payload: T): void {
    const timestamp = Date.now();
    this.eventHistory.push({ eventName, payload, timestamp });
    
    const eventHandlers = this.handlers.get(eventName);
    if (eventHandlers) {
      eventHandlers.forEach(handler => {
        try {
          // Execute asynchronously to prevent blocking the bus
          Promise.resolve(handler(payload)).catch(err => {
            console.error(`[EventBus] Error in handler for ${eventName}:`, err);
          });
        } catch (err) {
          console.error(`[EventBus] Sync error in handler for ${eventName}:`, err);
        }
      });
    }
  }
  
  public subscribe<T>(eventName: EventName, handler: (payload: T) => void | Promise<void>): void {
    if (!this.handlers.has(eventName)) {
      this.handlers.set(eventName, new Set());
    }
    this.handlers.get(eventName)!.add(handler as EventHandler);
  }
  
  public unsubscribe<T>(eventName: EventName, handler: (payload: T) => void | Promise<void>): void {
    const eventHandlers = this.handlers.get(eventName);
    if (eventHandlers) {
      eventHandlers.delete(handler as EventHandler);
    }
  }
  
  public replayEvents(): void {
    console.log(`[EventBus] Replaying ${this.eventHistory.length} events...`);
    for (const record of this.eventHistory) {
      const eventHandlers = this.handlers.get(record.eventName);
      if (eventHandlers) {
        eventHandlers.forEach(handler => {
          try {
            handler(record.payload);
          } catch (err) {
            console.error(`[EventBus] Replay error for ${record.eventName}:`, err);
          }
        });
      }
    }
  }
  
  public clearHistory(): void {
    this.eventHistory = [];
  }
}
