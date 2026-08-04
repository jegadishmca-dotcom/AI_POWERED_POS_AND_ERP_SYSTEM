import { ILLMClient, AITriageExplanationInput, StructuredExplanationOutput } from '../interfaces';

export class MockLLMClient implements ILLMClient {
  public async generateExplanation(prompt: string, input: AITriageExplanationInput): Promise<StructuredExplanationOutput> {
    const result = input.deterministicResult;
    
    // Simulate an LLM generating text based on the persona in the prompt
    let summary = `This is a synthesized explanation of the failure in ${result.input.workflowId}.`;
    let investigation = `Check the logs for ${result.hypotheses[0]?.affectedRules[0] || 'unknown rule'}.`;
    
    if (prompt.includes('Executive')) {
      summary = `Executive Summary: A Priority ${result.priority.value} incident occurred in the ${result.suggestedOwner.value.team} domain, blocking a critical workflow.`;
      investigation = `Allocate resources to the ${result.suggestedOwner.value.team} team immediately to clear the ${result.releaseRisk.value} release risk.`;
    } else if (prompt.includes('Developer')) {
      summary = `Dev Report: Exception caught during execution of capability ${result.input.capabilityId}. Fingerprint ${result.cluster.value} suggests a systemic validation issue.`;
      investigation = `Debug ${result.hypotheses[0]?.affectedRules.join(', ')} and inspect the data payload for variant ${result.input.variant}.`;
    } else if (prompt.includes('QA')) {
      summary = `QA Report: Scenario ${result.input.scenarioId} failed while executing as persona ${result.input.persona}.`;
      investigation = `Reproduce the scenario locally. Check evidence at ${result.input.evidencePath}.`;
    } else if (prompt.includes('ReleaseManager')) {
      summary = `Release Impact: The current build has a ${result.releaseRisk.value} risk level due to a failure in ${result.input.workflowId}.`;
      investigation = `Hold release until ${result.suggestedOwner.value.team} signs off on the fix for ${result.priority.value} defect.`;
    }

    return {
      summary,
      businessImpact: result.businessImpact.value, // Passed straight through deterministically
      rootCauseHypotheses: result.hypotheses.map(h => `${h.title} (Probability: ${h.probability})`),
      supportingEvidence: result.suggestedOwner.explainability.evidence.concat(result.priority.explainability.evidence),
      confidence: `${result.priority.explainability.confidence.deterministic}% deterministic confidence.`,
      recommendedInvestigation: investigation,
      affectedCapability: result.capabilityImpact.value,
      affectedWorkflow: result.input.workflowId,
      affectedRules: result.hypotheses.flatMap(h => h.affectedRules)
    };
  }
}
