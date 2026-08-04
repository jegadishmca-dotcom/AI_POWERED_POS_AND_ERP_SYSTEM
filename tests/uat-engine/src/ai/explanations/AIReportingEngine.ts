import * as fs from 'fs';
import * as path from 'path';
import { AITriageResult } from '../contracts/interfaces';

export class AIReportingEngine {
  public generateReports(result: AITriageResult, outDir: string) {
    if (!fs.existsSync(outDir)) fs.mkdirSync(outDir, { recursive: true });

    // 1. Machine Readable JSON
    fs.writeFileSync(path.join(outDir, 'AITriageOutput.json'), JSON.stringify(result, null, 2));

    // 2. AITriageSummary.md
    let summary = `# AI Triage Summary\n\n`;
    summary += `**Scenario ID**: ${result.input.scenarioId}\n`;
    summary += `**Priority**: ${result.priority.value} (Confidence: ${result.priority.explainability.confidence.deterministic}%)\n`;
    summary += `**Owner**: ${result.suggestedOwner.value.team}\n`;
    summary += `**Release Risk**: ${result.releaseRisk.value}\n`;
    summary += `**Regression**: ${result.regression.value ? 'Yes' : 'No'}\n\n`;
    
    summary += `### Top Recommendation\n`;
    result.recommendations.forEach(r => summary += `- ${r}\n`);
    
    fs.writeFileSync(path.join(outDir, 'AITriageSummary.md'), summary);

    // 3. AITriageReport.md (Detailed)
    let report = `# Detailed AI Triage Report\n\n`;
    report += `## Context\n- Workflow: ${result.input.workflowId}\n- Capability: ${result.input.capabilityId}\n\n`;
    
    report += `## Root Cause Hypotheses\n`;
    result.hypotheses.forEach(h => {
      report += `### ${h.title}\n`;
      report += `- **Score**: ${h.score.toFixed(3)} (Prob: ${h.probability}, Evid: ${h.evidenceStrength}, Hist: ${h.historicalSupport})\n`;
      report += `- **Reason**: ${h.explainability.reason}\n`;
      report += `- **Affected Rules**: ${h.affectedRules.join(', ')}\n\n`;
    });

    report += `## Findings & Explainability\n`;
    const writeFinding = (title: string, finding: any) => {
      report += `### ${title}\n`;
      report += `- **Decision**: ${finding.explainability.decision}\n`;
      report += `- **Reason**: ${finding.explainability.reason}\n`;
      report += `- **Confidence**: ${finding.explainability.confidence.deterministic}%\n\n`;
    };

    writeFinding('Ownership', result.suggestedOwner);
    writeFinding('Business Impact', result.businessImpact);
    writeFinding('Release Risk', result.releaseRisk);

    fs.writeFileSync(path.join(outDir, 'AITriageReport.md'), report);
    console.log('AI Reports Generated.');
  }
}
