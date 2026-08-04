import { BrowserPlugin } from '../../src/engine/plugins/browser/BrowserPlugin';
import { DatabasePlugin } from '../../src/engine/plugins/database/DatabasePlugin';
import { ApiPlugin } from '../../src/engine/plugins/api/ApiPlugin';
import { EvidencePlugin } from '../../src/engine/plugins/evidence/EvidencePlugin';

async function runStage1() {
  console.log('--- Stage 1: Infrastructure Validation ---');
  const browser = new BrowserPlugin();
  const db = new DatabasePlugin();
  const api = new ApiPlugin();
  const evidence = new EvidencePlugin();

  const context: any = {};

  await browser.initialize(context);
  await db.initialize(context);
  await api.initialize(context);
  await evidence.initialize(context);

  await browser.healthCheck();
  await db.healthCheck();
  await api.healthCheck();
  await evidence.healthCheck();

  await evidence.shutdown();
  await api.shutdown();
  await db.shutdown();
  await browser.shutdown();
  console.log('--- Stage 1 Complete ---\n');
}

runStage1().catch(console.error);
