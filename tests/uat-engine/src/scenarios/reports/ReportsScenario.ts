import { BusinessScenarioBase } from '../base/BusinessScenarioBase';
import { ScenarioPolicies } from '../interfaces';

export class GenerateReportSuccessScenario extends BusinessScenarioBase {
  constructor() { super('SCENARIO-REP-001-HAPPY', 'WF-REP-001'); }
  public getPolicies(): ScenarioPolicies { return { validation: { validateOnStep: false, validateOnTeardown: true, failFast: true }, cleanup: { strategy: 'rollback' }, evidence: { captureScreenshots: 'always', captureTrace: true, captureNetwork: false }, retry: { maxRetries: 0, retryOnFailure: false }, failure: { continueOnFailure: false } }; }
  protected async executeScenario(): Promise<void> { /* Pure Orchestration */ }
}

export class GenerateReportFailureScenario extends BusinessScenarioBase {
  constructor() { super('SCENARIO-REP-001-FAIL', 'WF-REP-001'); }
  public getPolicies(): ScenarioPolicies { return { validation: { validateOnStep: false, validateOnTeardown: true, failFast: true }, cleanup: { strategy: 'rollback' }, evidence: { captureScreenshots: 'on-failure', captureTrace: false, captureNetwork: false }, retry: { maxRetries: 0, retryOnFailure: false }, failure: { continueOnFailure: false } }; }
  protected async executeScenario(): Promise<void> { /* Pure Orchestration */ }
}
