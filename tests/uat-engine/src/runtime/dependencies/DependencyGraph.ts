import { BaseScenario } from '../templates/BaseScenario';
import { EngineException } from '../../engine/exceptions';

export class DependencyGraph {
  public buildExecutionOrder(scenarios: Map<string, BaseScenario>): string[] {
    const order: string[] = [];
    const visited = new Set<string>();
    const visiting = new Set<string>();

    const visit = (id: string) => {
      if (visiting.has(id)) {
        throw new EngineException(`Circular dependency detected involving scenario: ${id}`);
      }
      if (!visited.has(id)) {
        visiting.add(id);
        const scenario = scenarios.get(id);
        if (!scenario) {
          throw new EngineException(`Missing dependency: Scenario ${id} is required but not found.`);
        }
        
        for (const dep of scenario.metadata.dependencies) {
          visit(dep);
        }
        
        visiting.delete(id);
        visited.add(id);
        order.push(id);
      }
    };

    for (const [id] of scenarios.entries()) {
      visit(id);
    }

    return order;
  }
}
