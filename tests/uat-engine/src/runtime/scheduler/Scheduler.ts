import { ScenarioRegistry } from '../registry/ScenarioRegistry';
import { ITestContext } from '../../engine/interfaces';
import { ExecutionStrategy, ScenarioResource } from '../metadata/IScenarioMetadata';
import { IScenarioResult } from '../results/IScenarioResult';
import { EventName } from '../../engine/types';

export class Scheduler {
  private activeResources = new Set<ScenarioResource>();

  constructor(private registry: ScenarioRegistry) {}

  public async runAll(context: ITestContext): Promise<IScenarioResult[]> {
    const order = this.registry.getExecutionOrder();
    const results: IScenarioResult[] = [];

    for (const id of order) {
      const scenario = this.registry.getScenario(id);
      if (!scenario) continue;

      const metadata = scenario.metadata;
      
      // Wait for resources if Exclusive (Simplified locking)
      if (metadata.strategy === ExecutionStrategy.Exclusive) {
        while (this.activeResources.size > 0) {
          await new Promise(resolve => setTimeout(resolve, 100)); // Polling for simplicity
        }
      } else {
        // Wait for specific resources
        let resourcesAvailable = false;
        while (!resourcesAvailable) {
          resourcesAvailable = metadata.resources.every(r => !this.activeResources.has(r));
          if (!resourcesAvailable) {
            await new Promise(resolve => setTimeout(resolve, 100));
          }
        }
      }

      // Acquire resources
      for (const r of metadata.resources) {
        this.activeResources.add(r);
      }

      context.eventBus.publish(EventName.ScenarioStarted, { timestamp: Date.now(), scenarioId: id });
      
      // Timeout Logic
      const timeoutPromise = new Promise<IScenarioResult>((_, reject) => {
        setTimeout(() => reject(new Error('Scenario Timeout')), metadata.timeoutMs || 30000);
      });

      try {
        const result = await Promise.race([
          scenario.run(context),
          timeoutPromise
        ]) as IScenarioResult;

        results.push(result);
        
        if (result.status === 'FAILED') {
          context.eventBus.publish(EventName.ScenarioFailed, { timestamp: Date.now(), scenarioId: id, error: result.failures[0] });
        } else {
          context.eventBus.publish(EventName.ScenarioCompleted, { timestamp: Date.now(), scenarioId: id, durationMs: result.durationMs });
        }
      } catch (error) {
        results.push({
          scenarioId: id,
          status: 'TIMEOUT',
          durationMs: metadata.timeoutMs || 30000,
          ruleResults: {},
          warnings: [],
          failures: [error instanceof Error ? error : new Error(String(error))],
          artifacts: [],
          evidence: [],
          performanceMetrics: {}
        });
        context.eventBus.publish(EventName.ScenarioFailed, { timestamp: Date.now(), scenarioId: id, error: error as Error });
      } finally {
        // Release resources
        for (const r of metadata.resources) {
          this.activeResources.delete(r);
        }
      }
    }

    return results;
  }
}
