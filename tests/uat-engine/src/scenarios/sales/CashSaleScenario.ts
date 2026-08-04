import { BusinessScenarioBase } from '../base/BusinessScenarioBase';
import { ScenarioPolicies } from '../interfaces';

export class CashSaleSuccessScenario extends BusinessScenarioBase {
  constructor() {
    super('SCENARIO-SALES-001-HAPPY', 'WF-SALES-001');
  }

  public getPolicies(): ScenarioPolicies {
    return {
      validation: { validateOnStep: false, validateOnTeardown: true, failFast: true },
      cleanup: { strategy: 'rollback' },
      evidence: { captureScreenshots: 'always', captureTrace: true, captureNetwork: false },
      retry: { maxRetries: 0, retryOnFailure: false },
      failure: { continueOnFailure: false }
    };
  }

  protected async executeScenario(): Promise<void> {
    const posScreen = this.context.screens.pos;
    await posScreen.scanBarcode('8901234567890');
    await posScreen.openPayment();
    await posScreen.enterCash(500);
    await posScreen.tender();
  }
}

export class CashSaleFailureScenario extends BusinessScenarioBase {
  constructor() {
    super('SCENARIO-SALES-001-FAIL', 'WF-SALES-001');
  }

  public getPolicies(): ScenarioPolicies {
    return {
      validation: { validateOnStep: true, validateOnTeardown: false, failFast: true },
      cleanup: { strategy: 'rollback' },
      evidence: { captureScreenshots: 'on-failure', captureTrace: true, captureNetwork: false },
      retry: { maxRetries: 0, retryOnFailure: false },
      failure: { continueOnFailure: true }
    };
  }

  protected async executeScenario(): Promise<void> {
    const posScreen = this.context.screens.pos;
    await posScreen.scanBarcode('INVALID_SKU');
    await posScreen.openPayment();
    await posScreen.tender();
    // Intentionally fails
  }
}
