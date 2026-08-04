export interface Explainability {
  decision: string;
  evidence: string[];
  reason: string;
  confidence: {
    deterministic: number;
    ai: number; // Placeholder for 8B
  };
}

export interface Finding<T> {
  type: string;
  value: T;
  explainability: Explainability;
}

export interface AITriageInput {
  capabilityId: string;
  workflowId: string;
  scenarioId: string;
  persona: string;
  variant: string;
  timeline: any[];
  validationResults: any[];
  evidencePath: string;
  failureFingerprint: string;
  historicalRuns: any[];
  trendData: any;
  baselines: any;
  metrics: any;
  artifacts: string[];
}

export interface RootCauseHypothesis {
  id: string;
  title: string;
  probability: number;
  evidenceStrength: number;
  historicalSupport: number;
  score: number; // probability * evidenceStrength * historicalSupport
  affectedRules: string[];
  affectedWorkflows: string[];
  affectedCapabilities: string[];
  explainability: Explainability;
}

export interface AITriageResult {
  input: AITriageInput;
  normalized: boolean;
  priority: Finding<string>;
  cluster: Finding<string>;
  regression: Finding<boolean>;
  businessImpact: Finding<string>;
  suggestedOwner: Finding<{ team: string; reason: string }>;
  releaseRisk: Finding<string>;
  capabilityImpact: Finding<string>;
  hypotheses: RootCauseHypothesis[];
  recommendations: string[]; // Generated last from top hypothesis
}
