import { AITriageResult } from '../contracts/interfaces';

export interface AITriageExplanationInput {
  deterministicResult: AITriageResult;
}

export interface StructuredExplanationOutput {
  summary: string;
  businessImpact: string;
  rootCauseHypotheses: string[];
  supportingEvidence: string[];
  confidence: string;
  recommendedInvestigation: string;
  affectedCapability: string;
  affectedWorkflow: string;
  affectedRules: string[];
}

export interface PromptTemplate {
  version: string;
  architectureVersion: string;
  schemaVersion: string;
  persona: string;
  systemGuardrails: string[];
  instructions: string;
}

export interface ILLMClient {
  generateExplanation(prompt: string, input: AITriageExplanationInput): Promise<StructuredExplanationOutput>;
}
