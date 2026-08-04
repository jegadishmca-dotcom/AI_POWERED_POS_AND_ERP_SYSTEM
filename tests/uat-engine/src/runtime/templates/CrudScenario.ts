import { BaseScenario } from './BaseScenario';
import { IScenarioMetadata, ExecutionStrategy, ScenarioCleanup } from '../metadata/IScenarioMetadata';
import { ITestContext } from '../../engine/interfaces';

export abstract class CrudScenario extends BaseScenario {
  public abstract get metadata(): IScenarioMetadata;

  // Enforce typical CRUD structure implicitly by expecting these to be filled
  public abstract create(context: ITestContext): Promise<void>;
  public abstract read(context: ITestContext): Promise<void>;
  public abstract update(context: ITestContext): Promise<void>;
  public abstract delete(context: ITestContext): Promise<void>;

  public async setup(context: ITestContext): Promise<void> {}

  public async execute(context: ITestContext): Promise<void> {
    await this.create(context);
    await this.read(context);
    await this.update(context);
    await this.delete(context);
  }

  public async teardown(context: ITestContext): Promise<void> {
    if (this.metadata.cleanup === ScenarioCleanup.Rollback) {
      // Automatic db rollback logic could go here via context.database
    }
  }
}
