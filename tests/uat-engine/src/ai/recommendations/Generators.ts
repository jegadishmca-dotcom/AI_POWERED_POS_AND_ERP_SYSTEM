import { AITriageInput, RootCauseHypothesis, Finding } from '../contracts/interfaces';

export class HypothesisGenerator {
  public generate(input: AITriageInput, clusterFinding: Finding<string>): RootCauseHypothesis[] {
    const hypotheses: RootCauseHypothesis[] = [];

    // Mock deterministic hypothesis generation based on failure input
    const baseHyp: RootCauseHypothesis = {
      id: 'HYP-01',
      title: 'Validation rule failed in execution pipeline',
      probability: 0.8,
      evidenceStrength: 0.9,
      historicalSupport: 0.7,
      score: 0.8 * 0.9 * 0.7, // Ordered by Probability * Evidence * History
      affectedRules: ['GstCalculationRule'],
      affectedWorkflows: [input.workflowId],
      affectedCapabilities: [input.capabilityId],
      explainability: {
        decision: 'Rule Failure',
        evidence: ['ValidationResults contain errors'],
        reason: 'Deterministic extraction from engine payload',
        confidence: { deterministic: 90, ai: 0 }
      }
    };

    hypotheses.push(baseHyp);
    // Sort descending by score
    hypotheses.sort((a, b) => b.score - a.score);

    return hypotheses;
  }
}

export class RecommendationGenerator {
  public generate(topHypothesis: RootCauseHypothesis): string[] {
    const recommendations: string[] = [];
    if (topHypothesis.affectedRules.length > 0) {
      recommendations.push(`Inspect ${topHypothesis.affectedRules[0]}`);
    }
    if (topHypothesis.affectedWorkflows.length > 0) {
      recommendations.push(`Check precondition data for ${topHypothesis.affectedWorkflows[0]}`);
    }
    return recommendations;
  }
}
