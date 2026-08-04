import { PlaywrightUatRunner } from '../../src/runner/PlaywrightUatRunner';
import { UatExecutionEngine } from '../../src/runner/UatExecutionEngine';
import { PlaywrightLoginScreen } from '../../src/screens/PlaywrightLoginScreen';
import { PlaywrightPOSBillingScreen } from '../../src/screens/PlaywrightPOSBillingScreen';
import { PlaywrightReceiptHandler } from '../../src/receipts/PlaywrightReceiptHandler';
import { E2ECashSaleScenario } from '../../src/scenarios/E2ECashSaleScenario';
import { IUatRunnerConfig } from '../../src/runner/interfaces/IUatRunnerConfig';

export async function executeE2ECashSale() {
  const posUrl = process.env.POS_URL;
  if (!posUrl) throw new Error("Configuration Error: POS_URL environment variable is missing.");
  
  const username = process.env.UAT_USERNAME;
  if (!username) throw new Error("Configuration Error: UAT_USERNAME environment variable is missing.");
  
  const password = process.env.UAT_PASSWORD;
  if (!password) throw new Error("Configuration Error: UAT_PASSWORD environment variable is missing.");
  
  const productName = process.env.TEST_PRODUCT || 'Apple';
  const cashAmount = parseFloat(process.env.TEST_AMOUNT || '10.00');

  const config: IUatRunnerConfig = {
    posUrl,
    headless: process.env.HEADLESS === 'true',
    timeoutMs: 60000
  };

  const loginScreen = new PlaywrightLoginScreen();
  const billingScreen = new PlaywrightPOSBillingScreen();
  const receiptHandler = new PlaywrightReceiptHandler();

  const scenario = new E2ECashSaleScenario(
    loginScreen,
    billingScreen,
    receiptHandler,
    username,
    password,
    productName,
    cashAmount
  );

  const runner = new PlaywrightUatRunner(config);
  const engine = new UatExecutionEngine(runner, scenario, config);

  console.info("Starting UAT Execution: E2E Cash Sale Scenario");
  
  try {
    await engine.run();
    console.info("E2E Cash Sale Scenario executed successfully.");
  } catch (error) {
    console.error("UAT Execution Failed:", error);
    throw error;
  }
}

if (require.main === module) {
  executeE2ECashSale().catch(() => process.exit(1));
}
