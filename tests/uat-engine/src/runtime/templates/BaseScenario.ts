import { IScenarioMetadata } from '../metadata/IScenarioMetadata';
import { IScenarioResult } from '../results/IScenarioResult';
import { ITestContext } from '../../engine/interfaces';

export abstract class BaseScenario {
  public abstract get metadata(): IScenarioMetadata;

  public abstract setup(context: ITestContext): Promise<void>;
  public abstract execute(context: ITestContext): Promise<void>;
  public abstract teardown(context: ITestContext): Promise<void>;

  public async run(context: ITestContext): Promise<IScenarioResult> {
    const startTime = Date.now();
    const result: IScenarioResult = {
      scenarioId: this.metadata.id,
      status: 'SKIPPED',
      durationMs: 0,
      ruleResults: {},
      warnings: [],
      failures: [],
      artifacts: [],
      evidence: [],
      performanceMetrics: {}
    };

    try {
      await this.setup(context);
      await this.execute(context);
      result.status = 'PASSED';
    } catch (error) {
      result.status = 'FAILED';
      result.failures.push(error instanceof Error ? error : new Error(String(error)));
    } finally {
      try {
        await this.teardown(context);
      } catch (error) {
        result.warnings.push(`Teardown failed: ${error}`);
      }
      result.durationMs = Date.now() - startTime;
    }

    return result;
  }
}
