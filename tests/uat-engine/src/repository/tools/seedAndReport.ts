import * as path from 'path';
import { ExecutionRepository, FailureRepository, MetricRepository, TrendRepository } from '../modules/Repositories';
import { AnalyticsEngine } from '../analytics/AnalyticsEngine';
import { ReportingEngine } from '../reporting/ReportingEngine';
import { ExecutionRecord, FailureRecord } from '../interfaces';

async function seedAndReport() {
  const execRepo = new ExecutionRepository();
  const failRepo = new FailureRepository();
  const metricRepo = new MetricRepository();
  const trendRepo = new TrendRepository();

  console.log('Seeding Execution Data...');
  const now = Date.now();
  
  // Seed Mock Run
  const runRecord: ExecutionRecord = {
    id: `RUN-${now}`,
    timestamp: now,
    scenarios: [
      { scenarioId: 'SCENARIO-SALES-001-HAPPY', status: 'PASSED' },
      { scenarioId: 'SCENARIO-SALES-001-FAIL', status: 'FAILED' }
    ]
  };
  await execRepo.save(runRecord);

  // Seed Mock Failure
  const failureRecord: FailureRecord = {
    id: `FAIL-${now}`,
    timestamp: now,
    scenarioId: 'SCENARIO-SALES-001-FAIL',
    error: 'Payment declined timeout',
    fingerprint: {
      hash: 'F-8A9B2C',
      workflow: 'WF-SALES-001',
      scenario: 'SCENARIO-SALES-001-FAIL',
      capability: 'CAP-SALES-001',
      ruleFailures: [],
      validationResults: [],
      evidenceHash: 'EV-123'
    }
  };
  await failRepo.save(failureRecord);

  console.log('Running Reporting Engine...');
  const analytics = new AnalyticsEngine(execRepo, failRepo, metricRepo, trendRepo);
  const reporting = new ReportingEngine(analytics, execRepo, failRepo);

  const outDir = path.resolve(__dirname, '../../../../artifacts');
  await reporting.generateDashboards(outDir);
}

seedAndReport().catch(console.error);
