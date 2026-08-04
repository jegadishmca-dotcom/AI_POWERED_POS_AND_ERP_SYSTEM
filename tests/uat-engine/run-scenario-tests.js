const { CashSaleSuccessScenario } = require('./dist/src/scenarios/sales/CashSaleScenario');

async function runTests() {
  console.log('Running Scenario Orchestration tests...');
  
  const scenario = new CashSaleSuccessScenario();
  if (scenario.scenarioId !== 'SCENARIO-SALES-001-HAPPY') throw new Error('Failed to initialize scenario');

  let scannedCount = 0;
  let tenderedCount = 0;
  
  const mockScreens = {
    pos: {
      scanBarcode: async () => { scannedCount++; },
      openPayment: async () => {},
      enterCash: async () => {},
      tender: async () => { tenderedCount++; }
    }
  };
  
  const mockContext = {
    persona: 'Cashier',
    variant: 'Cash',
    screens: mockScreens
  };
  
  const mockEngineContext = {};
  
  const result = await scenario.run(mockEngineContext, mockContext);
  
  if (result.status !== 'PASSED') throw new Error('Scenario failed: ' + result.status);
  if (result.metrics.durationMs === undefined) throw new Error('Metrics missing');
  if (scannedCount !== 1) throw new Error('Expected 1 scan, got ' + scannedCount);
  if (tenderedCount !== 1) throw new Error('Expected 1 tender, got ' + tenderedCount);
  
  const events = result.timeline.map(t => t.event);
  if (!events.includes('Scenario Started')) throw new Error('Missing Scenario Started event');
  if (!events.includes('Scenario Passed')) throw new Error('Missing Scenario Passed event');
  if (!events.includes('Executing Rollback')) throw new Error('Missing Executing Rollback event');
  
  console.log('Scenario Orchestration tests passed successfully!');
}

runTests().catch(e => {
  console.error(e);
  process.exit(1);
});
