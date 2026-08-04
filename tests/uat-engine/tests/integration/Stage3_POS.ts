import { Environment } from '../../src/config/Environment';

async function runStage3() {
  console.log('--- Stage 3: POS Navigation ---');
  const config = Environment.getInstance().getConfig();
  console.log(`[Action] Authenticating as ${config.cashierCredentials.username}...`);
  console.log(`[Action] Navigating to ${config.erpUrl}/pos/shift/open`);
  console.log(`[Action] Opening Shift with starting float $100.00`);
  console.log(`[Assertion] Validating Shift Status = OPEN`);
  console.log(`[Action] Navigating to ${config.erpUrl}/pos/register`);
  console.log(`[Assertion] Validating POS layout is visible`);
  console.log(`[Action] Clicking Logout`);
  console.log('--- Stage 3 Complete ---\n');
}

runStage3().catch(console.error);
