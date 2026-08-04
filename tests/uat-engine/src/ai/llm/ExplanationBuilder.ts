import { ILLMClient, AITriageExplanationInput, StructuredExplanationOutput } from './interfaces';
import { PromptBuilder } from './PromptBuilder';

export class ExplanationBuilder {
  constructor(
    private client: ILLMClient,
    private promptBuilder: PromptBuilder
  ) {}

  public async buildExplanation(
    input: AITriageExplanationInput, 
    persona: 'Executive' | 'Developer' | 'QA' | 'ReleaseManager'
  ): Promise<StructuredExplanationOutput> {
    
    const template = this.promptBuilder.buildPrompt(persona);
    const payload = JSON.stringify(input.deterministicResult, null, 2);
    const prompt = this.promptBuilder.compile(template, payload);

    // Call the LLM (Mock or Real)
    const explanation = await this.client.generateExplanation(prompt, input);
    
    // Enforcement: Guard against LLM hallucination altering rigid IDs (Post-processing check)
    if (explanation.affectedWorkflow !== input.deterministicResult.input.workflowId) {
      explanation.affectedWorkflow = input.deterministicResult.input.workflowId;
    }
    if (explanation.affectedCapability !== input.deterministicResult.capabilityImpact.value) {
      explanation.affectedCapability = input.deterministicResult.capabilityImpact.value;
    }

    return explanation;
  }
}
