import { PromptTemplate } from './interfaces';

export class PromptBuilder {
  private static readonly GUARDRAILS = [
    "NEVER change the Priority provided in the input.",
    "NEVER change the Suggested Owner.",
    "NEVER change the Release Risk.",
    "NEVER change the Deterministic Confidence scores.",
    "NEVER contradict the deterministic findings.",
    "You may ONLY explain and summarize the data provided."
  ];

  public buildPrompt(persona: 'Executive' | 'Developer' | 'QA' | 'ReleaseManager'): PromptTemplate {
    return {
      version: '1.0.0',
      architectureVersion: '1.0',
      schemaVersion: '1.0',
      persona,
      systemGuardrails: PromptBuilder.GUARDRAILS,
      instructions: this.getPersonaInstructions(persona)
    };
  }

  private getPersonaInstructions(persona: string): string {
    switch (persona) {
      case 'Executive':
        return "Write a high-level executive summary focusing on business impact, risk, and which department owns the resolution. Avoid deep technical jargon.";
      case 'Developer':
        return "Write a deeply technical investigation report. Focus on the affected rules, validation failures, and specific code-level hypotheses.";
      case 'QA':
        return "Write a QA-focused report detailing the test scenario, the specific interaction that failed, and the steps to reproduce or investigate the failure.";
      case 'ReleaseManager':
        return "Write a release risk assessment. Focus on the release blockers, regression status, and overall confidence in the build.";
      default:
        return "Summarize the failure.";
    }
  }

  public compile(template: PromptTemplate, inputPayload: string): string {
    return `
# System Guardrails
${template.systemGuardrails.map(g => `- ${g}`).join('\n')}

# Persona Instructions
${template.instructions}

# Input Data
${inputPayload}

# Output Requirements
Return a JSON object conforming strictly to the StructuredExplanationOutput schema.
    `;
  }
}
