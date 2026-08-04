import { PlaywrightUatRunner } from '../../src/runner/PlaywrightUatRunner';
import { UatExecutionEngine } from '../../src/runner/UatExecutionEngine';
import { LoginScenario } from '../../src/scenarios/LoginScenario';
import { PlaywrightLoginScreen } from '../../src/screens/PlaywrightLoginScreen';
import { IUatRunnerConfig } from '../../src/runner/interfaces/IUatRunnerConfig';

export async function executeLoginUat() {
  const posUrl = process.env.POS_URL;
  if (!posUrl) {
    throw new Error("Configuration Error: POS_URL environment variable is missing.");
  }

  const username = process.env.UAT_USERNAME;
  if (!username) {
    throw new Error("Configuration Error: UAT_USERNAME environment variable is missing.");
  }

  const password = process.env.UAT_PASSWORD;
  if (!password) {
    throw new Error("Configuration Error: UAT_PASSWORD environment variable is missing.");
  }

  // Configuration
  const config: IUatRunnerConfig = {
    posUrl: posUrl,
    headless: process.env.HEADLESS === 'true',
    timeoutMs: 30000
  };

  // 1. Initialize Screen Object
  const loginScreen = new PlaywrightLoginScreen();

  // 2. Initialize Scenario
  const loginScenario = new LoginScenario(loginScreen, username, password);

  // 3. Initialize Runner
  const runner = new PlaywrightUatRunner(config);

  // 4. Initialize Execution Engine
  const engine = new UatExecutionEngine(runner, loginScenario, config);

  console.info("Starting UAT Execution: Login Scenario");
  
  // 5. Execute Engine
  // The engine flow: runner.start() -> setPage() -> execute() -> stop()
  try {
    await engine.run();
    console.info("Login Scenario executed successfully.");
  } catch (error) {
    console.error("UAT Execution Failed:", error);
    throw error;
  }
}

// Execute the bootstrap if run directly
if (require.main === module) {
  executeLoginUat().catch(() => {
    process.exit(1);
  });
}
