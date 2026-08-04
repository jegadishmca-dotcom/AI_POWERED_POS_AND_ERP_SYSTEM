import { IUatRunner } from './interfaces/IUatRunner';
import { IUatRunnerConfig } from './interfaces/IUatRunnerConfig';
import { IExecutableScenario } from './interfaces/IExecutableScenario';

export class UatExecutionEngine {
  constructor(
    private readonly runner: IUatRunner,
    private readonly scenario: IExecutableScenario,
    private readonly config: IUatRunnerConfig
  ) {}

  public async run(): Promise<void> {
    try {
      // 1. Start the runner
      const page = await this.runner.start();
      
      // 2. Inject Page into the Scenario
      this.scenario.setPage(page);
      
      // 3. Execute the scenario
      await this.scenario.execute();
      
    } finally {
      // 4. Stop the runner
      await this.runner.stop();
    }
  }
}
