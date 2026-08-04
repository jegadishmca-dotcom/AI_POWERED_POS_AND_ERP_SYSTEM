import { BusinessScenarioBase } from '../base/BusinessScenarioBase';

export class ScenarioRegistry {
  private scenarios: Map<string, BusinessScenarioBase> = new Map();

  public register(scenario: BusinessScenarioBase): void {
    if (this.scenarios.has(scenario.scenarioId)) {
      throw new Error(`Scenario ${scenario.scenarioId} is already registered.`);
    }
    this.scenarios.set(scenario.scenarioId, scenario);
  }

  public getScenario(id: string): BusinessScenarioBase | undefined {
    return this.scenarios.get(id);
  }

  public getAllScenarios(): BusinessScenarioBase[] {
    return Array.from(this.scenarios.values());
  }
}
