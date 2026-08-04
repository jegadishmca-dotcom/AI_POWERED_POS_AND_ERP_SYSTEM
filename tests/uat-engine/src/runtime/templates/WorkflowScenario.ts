import { BaseScenario } from './BaseScenario';
import { ITestContext } from '../../engine/interfaces';

export abstract class WorkflowScenario extends BaseScenario {
  // Workflows are multi-step processes
  public abstract get steps(): Array<(context: ITestContext) => Promise<void>>;

  public async setup(context: ITestContext): Promise<void> {}

  public async execute(context: ITestContext): Promise<void> {
    for (let i = 0; i < this.steps.length; i++) {
      context.logger.info(`Executing step ${i + 1}/${this.steps.length} of workflow ${this.metadata.id}`);
      await this.steps[i].call(this, context);
    }
  }

  public async teardown(context: ITestContext): Promise<void> {}
}
