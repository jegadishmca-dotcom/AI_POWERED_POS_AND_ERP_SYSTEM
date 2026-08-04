import * as fs from 'fs';
import * as path from 'path';
import { DefectTriageEngine } from '../../ai/triage/DefectTriageEngine';
import { NormalizationLayer } from '../../ai/normalization/NormalizationLayer';
import { RegressionAnalyzer, PriorityAnalyzer, OwnershipAnalyzer, BusinessImpactAnalyzer, ReleaseRiskAnalyzer } from '../../ai/analytics/Analyzers';
import { HypothesisGenerator, RecommendationGenerator } from '../../ai/recommendations/Generators';
import { AITriageInput } from '../../ai/contracts/interfaces';
import { AIReportingEngine } from '../../ai/explanations/AIReportingEngine';

export class DefectBacklogBuilder {
  private engine: DefectTriageEngine;
  private artifactsDir: string;

  constructor() {
    this.artifactsDir = path.resolve(__dirname, '../../../../artifacts');
    if (!fs.existsSync(this.artifactsDir)) {
      fs.mkdirSync(this.artifactsDir, { recursive: true });
    }
    
    this.engine = new DefectTriageEngine(
      new NormalizationLayer(),
      new RegressionAnalyzer(),
      new PriorityAnalyzer(),
      new OwnershipAnalyzer(),
      new BusinessImpactAnalyzer(),
      new ReleaseRiskAnalyzer(),
      new HypothesisGenerator(),
      new RecommendationGenerator()
    );
  }

  public generateBacklogAndMetrics(defects: AITriageInput[], benchmarkTruth: Record<string, any>) {
    let backlogMd = `# Defect Backlog\n\nGenerated automatically from Nightly Regression.\n\n`;
    
    const p0: string[] = [];
    const p1: string[] = [];
    const p2: string[] = [];
    const p3: string[] = [];
    
    let truePositives = 0;
    let falsePositives = 0;
    let falseNegatives = 0;
    
    let correctPriorities = 0;
    let correctOwners = 0;
    let correctRootCauses = 0;

    const reporting = new AIReportingEngine();

    for (const defect of defects) {
      const result = this.engine.execute(defect);
      
      // We also trigger the standard AI reporting (JSON & Markdown) for each failure
      reporting.generateReports(result, this.artifactsDir);
      
      const priority = result.priority.value;
      const owner = result.suggestedOwner.value.team;
      const rule = result.hypotheses[0]?.affectedRules[0] || 'Unknown';
      const fp = result.input.failureFingerprint;
      
      const entry = `- **[${fp}]** ${defect.scenarioId} | **Owner**: ${owner} | **Rule Failed**: ${rule}\n`;
      
      if (priority === 'P0') p0.push(entry);
      else if (priority === 'P1') p1.push(entry);
      else if (priority === 'P2') p2.push(entry);
      else p3.push(entry);

      // Deterministic Benchmark Validation
      const truth = benchmarkTruth[fp];
      if (truth) {
        truePositives++; // AI correctly triaged a known issue
        if (priority === truth.priority) correctPriorities++;
        if (owner === truth.owner) correctOwners++;
        if (rule === truth.rootCauseRule) correctRootCauses++;
      } else {
        // If it's not in the benchmark truth, it's considered a false positive for the sake of the exercise
        falsePositives++;
      }
    }
    
    // Simulate False Negatives (defects in truth not caught)
    const totalTruths = Object.keys(benchmarkTruth).length;
    falseNegatives = totalTruths - truePositives;
    
    backlogMd += `## P0 (Blockers)\n${p0.join('') || 'None\n'}\n`;
    backlogMd += `## P1 (Critical)\n${p1.join('') || 'None\n'}\n`;
    backlogMd += `## P2 (High)\n${p2.join('') || 'None\n'}\n`;
    backlogMd += `## P3 (Normal)\n${p3.join('') || 'None\n'}\n`;

    fs.writeFileSync(path.join(this.artifactsDir, 'DefectBacklog.md'), backlogMd);

    // Calculate Metrics
    const precision = truePositives / (truePositives + falsePositives || 1);
    const recall = truePositives / (truePositives + falseNegatives || 1);
    const f1 = 2 * ((precision * recall) / (precision + recall || 1));
    const fpr = falsePositives / (truePositives + falsePositives || 1);
    const fnr = falseNegatives / totalTruths;
    const isReady = p0.length === 0;

    let metricsMd = `# Quality Metrics Report\n\n`;
    metricsMd += `- **Precision**: ${(precision * 100).toFixed(2)}%\n`;
    metricsMd += `- **Recall**: ${(recall * 100).toFixed(2)}%\n`;
    metricsMd += `- **F1 Score**: ${(f1 * 100).toFixed(2)}%\n`;
    metricsMd += `- **False Positive Rate (FPR)**: ${(fpr * 100).toFixed(2)}%\n`;
    metricsMd += `- **False Negative Rate (FNR)**: ${(fnr * 100).toFixed(2)}%\n`;
    
    metricsMd += `\n### Accuracy KPIs\n`;
    metricsMd += `- **Priority Accuracy**: ${((correctPriorities / totalTruths) * 100).toFixed(2)}%\n`;
    metricsMd += `- **Owner Accuracy**: ${((correctOwners / totalTruths) * 100).toFixed(2)}%\n`;
    metricsMd += `- **Root Cause Accuracy**: ${((correctRootCauses / totalTruths) * 100).toFixed(2)}%\n`;
    
    fs.writeFileSync(path.join(this.artifactsDir, 'QualityMetricsReport.md'), metricsMd);

    let readinessMd = `# Release Readiness Report\n\n`;
    readinessMd += `**Status**: ${isReady ? 'READY TO DEPLOY' : 'BLOCKED'}\n\n`;
    readinessMd += `**Blocker Count**: ${p0.length}\n`;
    readinessMd += `**Critical Count**: ${p1.length}\n\n`;
    readinessMd += `All P0 defects must be cleared before the Release Manager can sign off.\n`;
    
    fs.writeFileSync(path.join(this.artifactsDir, 'ReleaseReadinessReport.md'), readinessMd);
    
    console.log('[DefectBacklogBuilder] Generated DefectBacklog.md, QualityMetricsReport.md, and ReleaseReadinessReport.md');
  }
}
