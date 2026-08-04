const fs = require('fs');
const path = require('path');

function walkDir(dir, callback) {
  fs.readdirSync(dir).forEach(f => {
    let dirPath = path.join(dir, f);
    let isDirectory = fs.statSync(dirPath).isDirectory();
    isDirectory ? walkDir(dirPath, callback) : callback(dirPath);
  });
}

function runArchTests() {
  console.log('Running AI Architecture tests...');
  const aiPath = path.resolve(__dirname, './src/ai');
  if (!fs.existsSync(aiPath)) return;
  
  const forbiddenImports = [
    'playwright',
    'pg',
    'typeorm',
    '../scenarios',
    '../../scenarios',
    '../workflows',
    '../../workflows',
    '../rules',
    '../../rules'
  ];

  let violations = 0;
  walkDir(aiPath, (filePath) => {
    if (!filePath.endsWith('.ts')) return;
    const content = fs.readFileSync(filePath, 'utf8');
    
    for (const forbidden of forbiddenImports) {
      const regex = new RegExp(`from\\s+['"]${forbidden}.*?['"]`, 'i');
      if (regex.test(content)) {
        console.error(`Forbidden import '${forbidden}' found in AI layer at ${filePath}`);
        violations++;
      }
    }

    if (content.includes('Math.random()')) {
      console.error(`Forbidden non-deterministic logic 'Math.random()' found in AI layer at ${filePath}`);
      violations++;
    }
  });

  if (violations > 0) {
    throw new Error(`Architecture tests failed with ${violations} violations.`);
  }
  
  console.log('AI Architecture tests passed!');
}

function runLLMTests() {
  console.log('Running LLM Prompt validation tests...');
  const llmPath = path.resolve(__dirname, './src/ai/llm');
  if (!fs.existsSync(llmPath)) return;
  
  const forbiddenImports = [
    '../repository',
    '../../repository',
    '../rules',
    '../../rules',
    '../scenarios',
    '../../scenarios',
    '../workflows',
    '../../workflows',
    'playwright'
  ];

  let violations = 0;
  walkDir(llmPath, (filePath) => {
    if (!filePath.endsWith('.ts')) return;
    const content = fs.readFileSync(filePath, 'utf8');
    
    for (const forbidden of forbiddenImports) {
      const regex = new RegExp(`from\\s+['"]${forbidden}.*?['"]`, 'i');
      if (regex.test(content)) {
        console.error(`Forbidden import '${forbidden}' found in LLM layer at ${filePath}`);
        violations++;
      }
    }
  });

  const promptBuilderPath = path.join(llmPath, 'PromptBuilder.ts');
  if (fs.existsSync(promptBuilderPath)) {
    const content = fs.readFileSync(promptBuilderPath, 'utf8');
    
    // Guardrails check
    if (!content.includes('NEVER change the Priority') || 
        !content.includes('NEVER contradict the deterministic findings')) {
      console.error('Missing prompt guardrails in PromptBuilder.ts');
      violations++;
    }
    
    // Persona Instructions
    if (!content.includes('Executive') || !content.includes('Developer') || 
        !content.includes('QA') || !content.includes('ReleaseManager')) {
      console.error('Missing Persona instructions in PromptBuilder.ts');
      violations++;
    }

    // Schema References
    if (!content.includes('StructuredExplanationOutput')) {
      console.error('Missing Schema reference in PromptBuilder.ts');
      violations++;
    }
  }

  if (violations > 0) {
    throw new Error(`LLM Architecture tests failed with ${violations} violations.`);
  }
  console.log('LLM Architecture tests passed!');
}

runArchTests();
runLLMTests();

