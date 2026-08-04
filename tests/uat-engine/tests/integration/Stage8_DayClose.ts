import { Environment } from '../../src/config/Environment';

async function runStage8() {
  console.log('--- Stage 8: Day Close ---');
  const config = Environment.getInstance().getConfig();
  console.log(`[Action] Navigating to ${config.erpUrl}/finance/day-close`);
  console.log(`[Action] Reconciling Cash Register`);
  console.log(`[Assertion-Fail] DayCloseValidationRule: Cash drawer variance exceeds $5.00 limit... ERROR`);
  console.log('--- Stage 8 Complete ---\n');
}

runStage8().catch(console.error);
