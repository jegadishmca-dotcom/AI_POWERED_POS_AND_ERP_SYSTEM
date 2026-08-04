import { BusinessScenarioBase } from '../base/BusinessScenarioBase';
import { ScenarioPolicies } from '../interfaces';

export class LoyaltyRedemptionSuccessScenario extends BusinessScenarioBase {
  constructor() { super('SCENARIO-CRM-002-HAPPY', 'WF-CRM-002'); }
  public getPolicies(): ScenarioPolicies { return { validation: { validateOnStep: false, validateOnTeardown: true, failFast: true }, cleanup: { strategy: 'rollback' }, evidence: { captureScreenshots: 'always', captureTrace: true, captureNetwork: false }, retry: { maxRetries: 0, retryOnFailure: false }, failure: { continueOnFailure: false } }; }
  protected async executeScenario(): Promise<void> { /* Pure Orchestration */ }
}

export class LoyaltyRedemptionFailureScenario extends BusinessScenarioBase {
  constructor() { super('SCENARIO-CRM-002-FAIL', 'WF-CRM-002'); }
  public getPolicies(): ScenarioPolicies { return { validation: { validateOnStep: false, validateOnTeardown: true, failFast: true }, cleanup: { strategy: 'rollback' }, evidence: { captureScreenshots: 'on-failure', captureTrace: false, captureNetwork: false }, retry: { maxRetries: 0, retryOnFailure: false }, failure: { continueOnFailure: false } }; }
  protected async executeScenario(): Promise<void> { /* Pure Orchestration */ }
}
