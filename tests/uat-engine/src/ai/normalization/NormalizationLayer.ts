import { AITriageInput } from '../contracts/interfaces';

export class NormalizationLayer {
  public normalize(input: AITriageInput): AITriageInput {
    // Standardize IDs, Severities, Timestamps, etc.
    return {
      ...input,
      capabilityId: input.capabilityId?.toUpperCase().trim() || 'UNKNOWN-CAP',
      workflowId: input.workflowId?.toUpperCase().trim() || 'UNKNOWN-WF',
      scenarioId: input.scenarioId?.toUpperCase().trim() || 'UNKNOWN-SCENARIO',
      // Mock metrics normalization
      metrics: input.metrics || { durationMs: 0, retries: 0 }
    };
  }
}
