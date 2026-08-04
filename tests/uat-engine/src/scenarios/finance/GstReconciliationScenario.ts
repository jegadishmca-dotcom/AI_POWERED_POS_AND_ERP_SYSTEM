import { BusinessScenarioBase } from '../base/BusinessScenarioBase';
import { ScenarioPolicies } from '../interfaces';

export class GstReconciliationSuccessScenario extends BusinessScenarioBase {
  constructor() { super('SCENARIO-FIN-003-HAPPY', 'WF-FIN-003'); }
  public getPolicies(): ScenarioPolicies { return { validation: { validateOnStep: false, validateOnTeardown: true, failFast: true }, cleanup: { strategy: 'rollback' }, evidence: { captureScreenshots: 'always', captureTrace: true, captureNetwork: false }, retry: { maxRetries: 0, retryOnFailure: false }, failure: { continueOnFailure: false } }; }
  protected async executeScenario(): Promise<void> { /* Pure Orchestration */ }
}

export class GstReconciliationFailureScenario extends BusinessScenarioBase {
  constructor() { super('SCENARIO-FIN-003-FAIL', 'WF-FIN-003'); }
  public getPolicies(): ScenarioPolicies { return { validation: { validateOnStep: false, validateOnTeardown: true, failFast: true }, cleanup: { strategy: 'rollback' }, evidence: { captureScreenshots: 'on-failure', captureTrace: false, captureNetwork: false }, retry: { maxRetries: 0, retryOnFailure: false }, failure: { continueOnFailure: false } }; }
  protected async executeScenario(): Promise<void> { /* Pure Orchestration */ }
}
