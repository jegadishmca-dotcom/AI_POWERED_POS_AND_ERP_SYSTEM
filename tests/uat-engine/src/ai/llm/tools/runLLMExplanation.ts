import * as fs from 'fs';
import * as path from 'path';
import { PromptBuilder } from '../PromptBuilder';
import { MockLLMClient } from '../clients/MockLLMClient';
import { ExplanationBuilder } from '../ExplanationBuilder';
import { MarkdownRenderer } from '../MarkdownRenderer';
import { AITriageExplanationInput } from '../interfaces';

async function generateLLMReports() {
  const artifactsDir = path.resolve(__dirname, '../../../../../artifacts');
  const triageOutputFile = path.join(artifactsDir, 'AITriageOutput.json');
  
  if (!fs.existsSync(triageOutputFile)) {
    throw new Error('AITriageOutput.json not found. Run Phase 8A first.');
  }

  const deterministicResult = JSON.parse(fs.readFileSync(triageOutputFile, 'utf8'));
  const input: AITriageExplanationInput = { deterministicResult };

  const promptBuilder = new PromptBuilder();
  const client = new MockLLMClient();
  const explanationBuilder = new ExplanationBuilder(client, promptBuilder);
  const renderer = new MarkdownRenderer();

  const personas = ['Executive', 'Developer', 'QA', 'ReleaseManager'] as const;
  const filenames = {
    Executive: 'ExecutiveReport.md',
    Developer: 'DeveloperReport.md',
    QA: 'QAReport.md',
    ReleaseManager: 'ReleaseManagerReport.md'
  };

  const finalJsonOutput: Record<string, any> = {};

  for (const persona of personas) {
    console.log(`Generating explanation for persona: ${persona}`);
    const explanation = await explanationBuilder.buildExplanation(input, persona);
    
    // Store JSON
    finalJsonOutput[persona] = explanation;

    // Render Markdown
    const markdown = renderer.render(explanation, `${persona} Triage Report`);
    fs.writeFileSync(path.join(artifactsDir, filenames[persona]), markdown);
  }

  // Save the collective structured output
  fs.writeFileSync(path.join(artifactsDir, 'AITriageExplanation.json'), JSON.stringify(finalJsonOutput, null, 2));
  console.log('LLM Triage Explanations generated successfully.');
}

generateLLMReports().catch(console.error);
