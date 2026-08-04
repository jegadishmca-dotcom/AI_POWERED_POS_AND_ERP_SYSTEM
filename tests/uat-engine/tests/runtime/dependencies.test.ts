import { DependencyGraph } from '../../src/runtime/dependencies/DependencyGraph';
import { BaseScenario } from '../../src/runtime/templates/BaseScenario';
import { IScenarioMetadata, ExecutionStrategy, ScenarioCleanup } from '../../src/runtime/metadata/IScenarioMetadata';
import { EngineException } from '../../src/engine/exceptions';
import { ITestContext } from '../../src/engine/interfaces';

class MockScenario extends BaseScenario {
  constructor(private id: string, private deps: string[]) {
    super();
  }
  public get metadata(): IScenarioMetadata {
    return {
      id: this.id,
      name: `Mock ${this.id}`,
      category: 'Low',
      priority: 1,
      tags: [],
      dependencies: this.deps,
      timeoutMs: 1000,
      retryCount: 0,
      estimatedDurationMs: 100,
      businessRules: [],
      evidenceRequirements: [],
      preconditions: [],
      cleanup: ScenarioCleanup.KeepTestData,
      resources: [],
      capabilities: [],
      strategy: ExecutionStrategy.Sequential
    };
  }
  public async setup(context: ITestContext): Promise<void> {}
  public async execute(context: ITestContext): Promise<void> {}
  public async teardown(context: ITestContext): Promise<void> {}
}

describe('DependencyGraph', () => {
  let graph: DependencyGraph;

  beforeEach(() => {
    graph = new DependencyGraph();
  });

  test('should order scenarios topologically', () => {
    const scenarios = new Map<string, BaseScenario>();
    scenarios.set('C', new MockScenario('C', ['B']));
    scenarios.set('A', new MockScenario('A', []));
    scenarios.set('B', new MockScenario('B', ['A']));

    const order = graph.buildExecutionOrder(scenarios);
    expect(order).toEqual(['A', 'B', 'C']);
  });

  test('should detect circular dependencies', () => {
    const scenarios = new Map<string, BaseScenario>();
    scenarios.set('A', new MockScenario('A', ['B']));
    scenarios.set('B', new MockScenario('B', ['A']));

    expect(() => graph.buildExecutionOrder(scenarios)).toThrow(EngineException);
  });
});
