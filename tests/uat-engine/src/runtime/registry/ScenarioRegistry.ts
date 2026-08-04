import { BaseScenario } from '../templates/BaseScenario';
import { DependencyGraph } from '../dependencies/DependencyGraph';
import { IScenarioMetadata } from '../metadata/IScenarioMetadata';

export class ScenarioRegistry {
  private scenarios: Map<string, BaseScenario> = new Map();
  private dependencyGraph: DependencyGraph;

  constructor() {
    this.dependencyGraph = new DependencyGraph();
  }

  public register(scenario: BaseScenario): void {
    const metadata = scenario.metadata;
    if (this.scenarios.has(metadata.id)) {
      throw new Error(`Scenario with ID ${metadata.id} is already registered.`);
    }
    this.scenarios.set(metadata.id, scenario);
  }

  public getScenario(id: string): BaseScenario | undefined {
    return this.scenarios.get(id);
  }

  public getAllMetadata(): IScenarioMetadata[] {
    return Array.from(this.scenarios.values()).map(s => s.metadata);
  }

  public getExecutionOrder(): string[] {
    return this.dependencyGraph.buildExecutionOrder(this.scenarios);
  }
}
