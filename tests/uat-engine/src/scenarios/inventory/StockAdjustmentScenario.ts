import { BusinessScenarioBase } from '../base/BusinessScenarioBase';
import { ScenarioPolicies } from '../interfaces';

export class StockAdjustmentSuccessScenario extends BusinessScenarioBase {
  constructor() { super('SCENARIO-INV-001-HAPPY', 'WF-INV-001'); }
  public getPolicies(): ScenarioPolicies { return { validation: { validateOnStep: false, validateOnTeardown: true, failFast: true }, cleanup: { strategy: 'rollback' }, evidence: { captureScreenshots: 'always', captureTrace: true, captureNetwork: false }, retry: { maxRetries: 0, retryOnFailure: false }, failure: { continueOnFailure: false } }; }
  protected async executeScenario(): Promise<void> { /* Pure Orchestration */ }
}

export class StockAdjustmentFailureScenario extends BusinessScenarioBase {
  constructor() { super('SCENARIO-INV-001-FAIL', 'WF-INV-001'); }
  public getPolicies(): ScenarioPolicies { return { validation: { validateOnStep: false, validateOnTeardown: true, failFast: true }, cleanup: { strategy: 'rollback' }, evidence: { captureScreenshots: 'on-failure', captureTrace: false, captureNetwork: false }, retry: { maxRetries: 0, retryOnFailure: false }, failure: { continueOnFailure: false } }; }
  protected async executeScenario(): Promise<void> { /* Pure Orchestration */ }
}
