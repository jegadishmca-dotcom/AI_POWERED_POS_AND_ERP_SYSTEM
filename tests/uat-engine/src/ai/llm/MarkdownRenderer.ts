import { StructuredExplanationOutput } from './interfaces';

export class MarkdownRenderer {
  public render(explanation: StructuredExplanationOutput, title: string): string {
    let md = `# ${title}\n\n`;
    
    md += `## Summary\n${explanation.summary}\n\n`;
    
    md += `## Business Impact & Confidence\n`;
    md += `- **Impact**: ${explanation.businessImpact}\n`;
    md += `- **Confidence**: ${explanation.confidence}\n\n`;
    
    md += `## Context\n`;
    md += `- **Capability**: ${explanation.affectedCapability}\n`;
    md += `- **Workflow**: ${explanation.affectedWorkflow}\n`;
    if (explanation.affectedRules.length > 0) {
      md += `- **Rules**: ${explanation.affectedRules.join(', ')}\n`;
    }
    md += `\n`;

    md += `## Hypotheses\n`;
    explanation.rootCauseHypotheses.forEach(h => {
      md += `- ${h}\n`;
    });
    md += `\n`;

    md += `## Supporting Evidence\n`;
    explanation.supportingEvidence.forEach(e => {
      md += `- ${e}\n`;
    });
    md += `\n`;

    md += `## Recommended Investigation\n${explanation.recommendedInvestigation}\n`;

    return md;
  }
}
