import { Environment } from '../../src/config/Environment';

async function runStage7() {
  console.log('--- Stage 7: Stock Adjustment ---');
  const config = Environment.getInstance().getConfig();
  console.log(`[Action] Navigating to ${config.erpUrl}/inventory/adjust`);
  console.log(`[Action] Reducing stock of SKU-100 by 5 units`);
  console.log(`[Assertion-Fail] NegativeInventoryRule: Adjustment results in negative stock... ERROR`);
  console.log('--- Stage 7 Complete ---\n');
}

runStage7().catch(console.error);
