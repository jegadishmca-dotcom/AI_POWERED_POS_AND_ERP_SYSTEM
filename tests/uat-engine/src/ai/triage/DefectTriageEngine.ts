import { AITriageInput, AITriageResult } from '../contracts/interfaces';
import { NormalizationLayer } from '../normalization/NormalizationLayer';
import { RegressionAnalyzer, PriorityAnalyzer, OwnershipAnalyzer, BusinessImpactAnalyzer, ReleaseRiskAnalyzer } from '../analytics/Analyzers';
import { HypothesisGenerator, RecommendationGenerator } from '../recommendations/Generators';

export class DefectTriageEngine {
  constructor(
    private normalizer: NormalizationLayer,
    private regressionAnalyzer: RegressionAnalyzer,
    private priorityAnalyzer: PriorityAnalyzer,
    private ownershipAnalyzer: OwnershipAnalyzer,
    private businessImpactAnalyzer: BusinessImpactAnalyzer,
    private releaseRiskAnalyzer: ReleaseRiskAnalyzer,
    private hypothesisGenerator: HypothesisGenerator,
    private recommendationGenerator: RecommendationGenerator
  ) {}

  public execute(rawInput: AITriageInput): AITriageResult {
    // 1. Normalization
    const input = this.normalizer.normalize(rawInput);
    
    // 2. Analyzers
    const regression = this.regressionAnalyzer.analyze(input);
    const priority = this.priorityAnalyzer.analyze(input);
    const ownership = this.ownershipAnalyzer.analyze(input);
    const businessImpact = this.businessImpactAnalyzer.analyze(input);
    const releaseRisk = this.releaseRiskAnalyzer.analyze(input, businessImpact.value, regression.value);
    
    // Clustering finding (mocking a deterministic hash grouping)
    const cluster = {
      type: 'CLUSTER',
      value: input.failureFingerprint,
      explainability: {
        decision: `Cluster ${input.failureFingerprint}`,
        evidence: [],
        reason: 'Deterministic fingerprint',
        confidence: { deterministic: 100, ai: 0 }
      }
    };

    // 3. Hypotheses & Recommendations
    const hypotheses = this.hypothesisGenerator.generate(input, cluster);
    const recommendations = hypotheses.length > 0 ? this.recommendationGenerator.generate(hypotheses[0]) : [];

    // 4. Output
    return {
      input,
      normalized: true,
      priority,
      cluster,
      regression,
      businessImpact,
      suggestedOwner: ownership,
      releaseRisk,
      capabilityImpact: {
        type: 'CAPABILITY_IMPACT',
        value: input.capabilityId,
        explainability: { decision: 'Impacted', evidence: [], reason: 'Direct mapping', confidence: { deterministic: 100, ai: 0 } }
      },
      hypotheses,
      recommendations
    };
  }
}
