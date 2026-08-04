import * as path from 'path';
import { AITriageInput } from '../contracts/interfaces';
import { NormalizationLayer } from '../normalization/NormalizationLayer';
import { RegressionAnalyzer, PriorityAnalyzer, OwnershipAnalyzer, BusinessImpactAnalyzer, ReleaseRiskAnalyzer } from '../analytics/Analyzers';
import { HypothesisGenerator, RecommendationGenerator } from '../recommendations/Generators';
import { DefectTriageEngine } from '../triage/DefectTriageEngine';
import { AIReportingEngine } from '../explanations/AIReportingEngine';

function runTriage() {
  const mockInput: AITriageInput = {
    capabilityId: 'CAP-SALES-001',
    workflowId: 'WF-SALES-001',
    scenarioId: 'SCENARIO-SALES-001-FAIL',
    persona: 'Cashier',
    variant: 'Cash',
    timeline: [],
    validationResults: [{ rule: 'GstCalculationRule', status: 'FAILED' }],
    evidencePath: '/evidence/fail-123.png',
    failureFingerprint: 'F-8A9B2C',
    historicalRuns: [], // Simulating a regression
    trendData: {},
    baselines: {},
    metrics: { durationMs: 1200 },
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
}

runTriage();
