import { ElementId, IInteractionEngine, IInteractionMetrics, InteractionEventType } from '../interfaces';
import { IEventBus } from '../../engine/interfaces';
import { EventName } from '../../engine/types';

export class InteractionEngine implements IInteractionEngine {
  private metrics: IInteractionMetrics = {
    durationMs: 0,
    retries: 0,
    resolutionTimeMs: 0,
    executionTimeMs: 0,
    isTimeout: false
  };

  constructor(
    private eventBus: IEventBus,
    private mockBrowserPage: any // In a real implementation, this wraps the IBrowserContext page
  ) {}

  public getMetrics(): IInteractionMetrics {
    return this.metrics;
  }

  private async executeWithAutoWait<T>(actionName: string, elementId: ElementId | null, action: () => Promise<T>): Promise<T> {
    const start = Date.now();
    this.publishEvent(InteractionEventType.Started, { action: actionName, elementId });
    
    let attempt = 0;
    const maxRetries = 3;
    
    while (attempt <= maxRetries) {
      try {
        // Auto-wait logic would normally wait for Visibility/Enabled/Stable here
        const result = await action();
        
        const duration = Date.now() - start;
        this.metrics.durationMs += duration;
        this.metrics.executionTimeMs += duration;
        
        if (attempt > 0) {
          this.publishEvent(InteractionEventType.Recovered, { action: actionName, elementId, retries: attempt });
        }
        this.publishEvent(InteractionEventType.Succeeded, { action: actionName, elementId, durationMs: duration });
        return result;
      } catch (error) {
        attempt++;
        this.metrics.retries++;
        
        if (attempt > maxRetries) {
          this.metrics.isTimeout = true;
          this.publishEvent(InteractionEventType.Timeout, { action: actionName, elementId, error });
          this.publishEvent(InteractionEventType.Failed, { action: actionName, elementId, error });
          throw new Error(`Interaction ${actionName} failed after ${maxRetries} retries on ${elementId}`);
        }
        
        this.publishEvent(InteractionEventType.Retried, { action: actionName, elementId, attempt });
        await new Promise(res => setTimeout(res, 500 * attempt));
      }
    }
    throw new Error('Unreachable');
  }

  private publishEvent(type: InteractionEventType, payload: any) {
    // We map Interaction events into the global EventBus payload
    this.eventBus.publish('InteractionEvent' as any, { type, ...payload }); 
  }

  public async navigate(url: string): Promise<void> {
    await this.executeWithAutoWait('navigate', null, async () => {
      // Mock navigation
    });
  }

  public async setValue(elementId: ElementId, value: string): Promise<void> {
    await this.executeWithAutoWait('setValue', elementId, async () => {
      // Mock fill
    });
  }

  public async choose(elementId: ElementId, option: string): Promise<void> {
    await this.executeWithAutoWait('choose', elementId, async () => {
      // Mock select
    });
  }

  public async submit(elementId: ElementId): Promise<void> {
    await this.executeWithAutoWait('submit', elementId, async () => {
      // Mock click submit
    });
  }

  public async search(elementId: ElementId, query: string): Promise<void> {
    await this.executeWithAutoWait('search', elementId, async () => {
      // Mock fill and enter
    });
  }

  public async confirm(elementId: ElementId): Promise<void> {
    await this.executeWithAutoWait('confirm', elementId, async () => {
      // Mock click confirm
    });
  }

  public async cancel(elementId: ElementId): Promise<void> {
    await this.executeWithAutoWait('cancel', elementId, async () => {
      // Mock click cancel
    });
  }

  public async open(elementId: ElementId): Promise<void> {
    await this.executeWithAutoWait('open', elementId, async () => {
      // Mock click to open
    });
  }

  public async close(elementId: ElementId): Promise<void> {
    await this.executeWithAutoWait('close', elementId, async () => {
      // Mock click close
    });
  }
}
