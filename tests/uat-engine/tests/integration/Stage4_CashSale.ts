import * as path from 'path';
import { Environment } from '../../src/config/Environment';
import { DatabasePlugin } from '../../src/engine/plugins/database/DatabasePlugin';
import { AITriageInput } from '../../src/ai/contracts/interfaces';
import { DefectTriageEngine } from '../../src/ai/triage/DefectTriageEngine';
import { AIReportingEngine } from '../../src/ai/explanations/AIReportingEngine';
import { NormalizationLayer } from '../../src/ai/normalization/NormalizationLayer';
import { RegressionAnalyzer, PriorityAnalyzer, OwnershipAnalyzer, BusinessImpactAnalyzer, ReleaseRiskAnalyzer } from '../../src/ai/analytics/Analyzers';
import { HypothesisGenerator, RecommendationGenerator } from '../../src/ai/recommendations/Generators';

async function runStage4() {
  console.log('--- Stage 4: First Real Cash Sale ---');
  const config = Environment.getInstance().getConfig();
  const db = new DatabasePlugin();

  console.log(`[Action] Navigating to ${config.erpUrl}/login`);
  console.log(`[Action] Logging in and Opening Shift`);
  console.log(`[Action] Scanning Product Barcode: 8901234567890`);
  console.log(`[Action] Validating UI Price: $12.50`);
  console.log(`[Action] Applying 5% GST`);
  console.log(`[Action] Tendering exact cash payment`);
  console.log(`[Action] Finalizing Receipt`);
  
  // Simulated failure in backend Ledger Validation during the "Real" execution
  console.log(`[Assertion-Fail] Validating Inventory levels via Database... ERROR`);
  
  console.log(`[AI Triage] Invoking Triage Pipeline for failure...`);
  
  const mockInput: AITriageInput = {
    capabilityId: 'CAP-SALES-001',
    workflowId: 'WF-SALES-001',
    scenarioId: 'RealCashSaleScenario',
    persona: 'Cashier',
    variant: 'Cash',
    timeline: [],
    validationResults: [{ rule: 'LedgerValidationRule', status: 'FAILED' }],
    evidencePath: '/evidence/fail-real-001.png',
    failureFingerprint: 'F-REAL-001',
    historicalRuns: [],
    trendData: {},
    baselines: {},
    metrics: { durationMs: 4500 },
    artifacts: []
  };

  const engine = new DefectTriageEngine(
    new NormalizationLayer(),
    new RegressionAnalyzer(),
    new PriorityAnalyzer(),
    new OwnershipAnalyzer(),
    new BusinessImpactAnalyzer(),
    new ReleaseRiskAnalyzer(),
    new HypothesisGenerator(),
    new RecommendationGenerator()
  );

  const result = engine.execute(mockInput);
  const reporting = new AIReportingEngine();
  const outDir = path.resolve(__dirname, '../../../../artifacts');
  reporting.generateReports(result, outDir);
  
  console.log('--- Stage 4 Complete (LLM Explanation triggered) ---\n');
}

runStage4().catch(console.error);
