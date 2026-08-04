import { BusinessScenarioBase } from '../base/BusinessScenarioBase';
import { ScenarioPolicies } from '../interfaces';

export class SalesReturnSuccessScenario extends BusinessScenarioBase {
  constructor() { super('SCENARIO-SALES-002-HAPPY', 'WF-SALES-002'); }
  public getPolicies(): ScenarioPolicies { return { validation: { validateOnStep: false, validateOnTeardown: true, failFast: true }, cleanup: { strategy: 'rollback' }, evidence: { captureScreenshots: 'always', captureTrace: true, captureNetwork: false }, retry: { maxRetries: 0, retryOnFailure: false }, failure: { continueOnFailure: false } }; }
  protected async executeScenario(): Promise<void> { /* Pure Orchestration */ }
}

export class SalesReturnFailureScenario extends BusinessScenarioBase {
  constructor() { super('SCENARIO-SALES-002-FAIL', 'WF-SALES-002'); }
  public getPolicies(): ScenarioPolicies { return { validation: { validateOnStep: false, validateOnTeardown: true, failFast: true }, cleanup: { strategy: 'rollback' }, evidence: { captureScreenshots: 'on-failure', captureTrace: false, captureNetwork: false }, retry: { maxRetries: 0, retryOnFailure: false }, failure: { continueOnFailure: false } }; }
  protected async executeScenario(): Promise<void> { /* Pure Orchestration */ }
}
