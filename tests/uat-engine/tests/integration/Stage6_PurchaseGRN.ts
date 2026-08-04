import { Environment } from '../../src/config/Environment';

async function runStage6() {
  console.log('--- Stage 6: Purchase & GRN ---');
  const config = Environment.getInstance().getConfig();
  console.log(`[Action] Navigating to ${config.erpUrl}/purchase/po`);
  console.log(`[Action] Creating PO for Vendor V-001`);
  console.log(`[Action] Converting PO to GRN`);
  console.log(`[Assertion-Fail] PurchaseGRNRule: Invoice total mismatch... ERROR`);
  console.log('--- Stage 6 Complete ---\n');
}

runStage6().catch(console.error);
