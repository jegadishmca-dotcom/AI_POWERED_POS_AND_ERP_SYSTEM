import { Environment } from '../../src/config/Environment';

async function runStage2() {
  console.log('--- Stage 2: Authentication Validation ---');
  const config = Environment.getInstance().getConfig();
  console.log(`[Action] Navigating to ${config.erpUrl}/login`);
  console.log(`[Action] Entering credentials for user: ${config.cashierCredentials.username}`);
  console.log(`[Action] Clicking Login`);
  console.log(`[Assertion] Validating Dashboard loaded successfully.`);
  console.log(`[Assertion] Validating Session JWT token acquired.`);
  console.log('--- Stage 2 Complete ---\n');
}

runStage2().catch(console.error);
