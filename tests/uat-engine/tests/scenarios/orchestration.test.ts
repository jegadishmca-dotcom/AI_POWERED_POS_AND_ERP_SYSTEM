import { BusinessScenarioBase } from '../../src/scenarios/base/BusinessScenarioBase';
import { CashSaleSuccessScenario } from '../../src/scenarios/sales/CashSaleScenario';

describe('Business Scenario Orchestration', () => {
  test('Scenario initializes with status SKIPPED', () => {
    const scenario = new CashSaleSuccessScenario();
    expect(scenario.scenarioId).toBe('SCENARIO-SALES-001-HAPPY');
  });

  test('Scenario orchestration lifecycle works', async () => {
    const scenario = new CashSaleSuccessScenario();
    const mockScreens = {
      pos: {
        scanBarcode: jest.fn(),
        openPayment: jest.fn(),
        enterCash: jest.fn(),
        tender: jest.fn()
      }
    };
    
    const mockContext: any = {
      persona: 'Cashier',
      variant: 'Cash',
      screens: mockScreens
    };
    
    const mockEngineContext: any = {};
    
    const result = await scenario.run(mockEngineContext, mockContext);
    
    expect(result.status).toBe('PASSED');
    expect(result.metrics.durationMs).toBeGreaterThanOrEqual(0);
    expect(mockScreens.pos.scanBarcode).toHaveBeenCalledTimes(2);
    expect(mockScreens.pos.tender).toHaveBeenCalledTimes(1);
    
    // Check if timeline contains expected lifecycle events
    const events = result.timeline.map((t: any) => t.event);
    expect(events).toContain('Scenario Started');
    expect(events).toContain('Scenario Passed');
    expect(events).toContain('Executing Rollback'); // Because CashSaleScenario has rollback policy
  });
});
