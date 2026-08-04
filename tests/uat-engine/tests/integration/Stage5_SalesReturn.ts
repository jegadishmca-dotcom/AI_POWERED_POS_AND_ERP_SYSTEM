import { Environment } from '../../src/config/Environment';

async function runStage5() {
  console.log('--- Stage 5: Sales Return ---');
  const config = Environment.getInstance().getConfig();
  console.log(`[Action] Navigating to ${config.erpUrl}/pos/return`);
  console.log(`[Action] Scanning Original Receipt: RCPT-12345`);
  console.log(`[Assertion-Fail] ReturnValidationRule: Original sale not found... ERROR`);
  
  // Note: Triage invocation is now handled uniformly in the NightlyRegression runner 
  // to avoid duplication in each stage script.
  console.log('--- Stage 5 Complete ---\n');
}

runStage5().catch(console.error);
