import * as fs from 'fs';
import * as path from 'path';

function walkDir(dir: string, callback: (filePath: string) => void) {
  if (!fs.existsSync(dir)) return;
  const files = fs.readdirSync(dir);
  for (const file of files) {
    const fullPath = path.join(dir, file);
    if (fs.statSync(fullPath).isDirectory()) {
      walkDir(fullPath, callback);
    } else if (fullPath.endsWith('.ts')) {
      callback(fullPath);
    }
  }
}

describe('Architecture Rules', () => {
  test('Engine core must not depend on Playwright, PostgreSQL, HTTP, or AI libraries', () => {
    const enginePath = path.resolve(__dirname, '../../src/engine');
    const forbiddenImports = [
      'playwright',
      '@playwright/test',
      'pg',
      'axios',
      'node-fetch',
      'openai',
      '@google/generative-ai',
      '@anthropic-ai/sdk'
    ];
    
    walkDir(enginePath, (filePath) => {
      const content = fs.readFileSync(filePath, 'utf8');
      
      for (const forbidden of forbiddenImports) {
        const regex = new RegExp(`from\\s+['"]${forbidden}['"]`, 'i');
        const regexRequire = new RegExp(`require\\(['"]${forbidden}['"]\\)`, 'i');
        expect(regex.test(content)).toBe(false);
        expect(regexRequire.test(content)).toBe(false);
      }
    });
  });

  test('Runtime must not depend on Playwright or business logic', () => {
    const runtimePath = path.resolve(__dirname, '../../src/runtime');
    const forbiddenImports = [
      'playwright',
      '@playwright/test',
      '../rules',
      '../../rules',
      '../scenarios',
      '../../scenarios',
      '../page_objects',
      '../../page_objects'
    ];
    
    walkDir(runtimePath, (filePath) => {
      const content = fs.readFileSync(filePath, 'utf8');
      for (const forbidden of forbiddenImports) {
        const regex = new RegExp(`from\\s+['"]${forbidden}['"]`, 'i');
        const regexRequire = new RegExp(`require\\(['"]${forbidden}['"]\\)`, 'i');
        if (regex.test(content) || regexRequire.test(content)) {
          throw new Error(`Forbidden import '${forbidden}' found in ${filePath}`);
        }
      }
    });
  });

  test('Rule Engine must not depend on Runtime, Playwright, DB, HTTP, or Plugins', () => {
    const rulesPath = path.resolve(__dirname, '../../src/rules');
    // It's possible the directory might not exist during very early bootstrapping if we skip it,
    // but in this phase we just created it.
    if (!fs.existsSync(rulesPath)) return;
    
    const forbiddenImports = [
      'playwright',
      '@playwright/test',
      'pg',
      'axios',
      '../runtime',
      '../../runtime',
      '../plugins',
      '../../plugins'
    ];
    
    walkDir(rulesPath, (filePath) => {
      const content = fs.readFileSync(filePath, 'utf8');
      for (const forbidden of forbiddenImports) {
        const regex = new RegExp(`from\\s+['"]${forbidden}['"]`, 'i');
        const regexRequire = new RegExp(`require\\(['"]${forbidden}['"]\\)`, 'i');
        if (regex.test(content) || regexRequire.test(content)) {
          throw new Error(`Forbidden import '${forbidden}' found in ${filePath}`);
        }
      }
    });
  });

  test('Workflow Library must not depend on Playwright, UI commands, or Runtime Scheduler', () => {
    const wfPath = path.resolve(__dirname, '../../src/workflows');
    if (!fs.existsSync(wfPath)) return;
    
    const forbiddenImports = [
      'playwright',
      '@playwright/test',
      '../runtime',
      '../../runtime',
      '../plugins/browser',
      '../../plugins/browser'
    ];

    const forbiddenTerms = [
      'page\\.',
      'locator\\(',
      '\\.click\\(',
      '\\.fill\\(',
      'PageObject'
    ];
    
    walkDir(wfPath, (filePath) => {
      const content = fs.readFileSync(filePath, 'utf8');
      
      for (const forbidden of forbiddenImports) {
        const regex = new RegExp(`from\\s+['"]${forbidden}['"]`, 'i');
        const regexRequire = new RegExp(`require\\(['"]${forbidden}['"]\\)`, 'i');
        if (regex.test(content) || regexRequire.test(content)) {
          throw new Error(`Forbidden import '${forbidden}' found in Workflow Library at ${filePath}`);
        }
      }

      for (const term of forbiddenTerms) {
        const regex = new RegExp(term, 'i');
        if (regex.test(content)) {
          throw new Error(`Forbidden UI term '${term}' found in abstract Workflow Library at ${filePath}`);
        }
      }
    });
  });

  test('Interaction and Screen Libraries must not contain Playwright, Rules, Workflows, or CSS', () => {
    const interactionPath = path.resolve(__dirname, '../../src/interaction');
    const screensPath = path.resolve(__dirname, '../../src/screens');
    
    const pathsToCheck = [interactionPath, screensPath].filter(p => fs.existsSync(p));
    
    const forbiddenImports = [
      'playwright',
      '@playwright/test',
      '../rules',
      '../../rules',
      '../workflows',
      '../../workflows'
    ];

    const forbiddenTerms = [
      'page\\.',
      'locator\\(',
      'css=',
      'xpath=',
      '//div',
      '\\.class',
      '#id'
    ];
    
    for (const dirPath of pathsToCheck) {
      walkDir(dirPath, (filePath) => {
        const content = fs.readFileSync(filePath, 'utf8');
        
        for (const forbidden of forbiddenImports) {
          const regex = new RegExp(`from\\s+['"]${forbidden}['"]`, 'i');
          const regexRequire = new RegExp(`require\\(['"]${forbidden}['"]\\)`, 'i');
          if (regex.test(content) || regexRequire.test(content)) {
            throw new Error(`Forbidden import '${forbidden}' found in Phase 6 layer at ${filePath}`);
          }
        }

        // We avoid very generic checks that might match normal words, so we strictly check for page.click etc.
        const specificForbiddenTerms = [
          'page\\.click',
          'page\\.fill',
          'css=',
          'xpath='
        ];
        
        for (const term of specificForbiddenTerms) {
          const regex = new RegExp(term, 'i');
          if (regex.test(content)) {
            throw new Error(`Forbidden UI term '${term}' found in abstract Phase 6 layer at ${filePath}`);
          }
        }
      });
    }
  });

  test('Business Scenarios must contain pure orchestration logic only', () => {
    const scenariosPath = path.resolve(__dirname, '../../src/scenarios');
    if (!fs.existsSync(scenariosPath)) return;
    
    const forbiddenImports = [
      'playwright',
      '@playwright/test',
      'pg',
      'typeorm'
    ];

    const forbiddenTerms = [
      'expect\\(',
      'assert\\(',
      'console\\.log',
      'console\\.error',
      'process\\.env',
      'Date\\.now\\(',
      'SELECT\\s+',
      'INSERT\\s+',
      'UPDATE\\s+'
    ];
    
    walkDir(scenariosPath, (filePath) => {
      // Exclude interfaces and base classes if needed, but the rules apply globally to scenarios
      const content = fs.readFileSync(filePath, 'utf8');
      
      for (const forbidden of forbiddenImports) {
        const regex = new RegExp(`from\\s+['"]${forbidden}['"]`, 'i');
        const regexRequire = new RegExp(`require\\(['"]${forbidden}['"]\\)`, 'i');
        if (regex.test(content) || regexRequire.test(content)) {
          throw new Error(`Forbidden import '${forbidden}' found in Business Scenarios at ${filePath}`);
        }
      }

      for (const term of forbiddenTerms) {
        const regex = new RegExp(term, 'i');
        if (regex.test(content)) {
          // Allow comments containing these words
          const lines = content.split('\n');
          for (const line of lines) {
            if (line.trim().startsWith('//')) continue;
            if (regex.test(line)) {
              throw new Error(`Forbidden logic/term '${term}' found in orchestration Scenario at ${filePath}`);
            }
          }
        }
      }
    });
  });

  test('Repository must not import Scenarios, Rules, or Workflows', () => {
    const repoPath = path.resolve(__dirname, '../../src/repository');
    if (!fs.existsSync(repoPath)) return;
    
    const forbiddenPaths = [
      '../rules',
      '../../rules',
      '../workflows',
      '../../workflows',
      '../scenarios',
      '../../scenarios'
    ];
    
    walkDir(repoPath, (filePath) => {
      const content = fs.readFileSync(filePath, 'utf8');
      for (const forbidden of forbiddenPaths) {
        const regex = new RegExp(`from\\s+['"]${forbidden}.*?['"]`, 'i');
        if (regex.test(content)) {
          throw new Error(`Forbidden import '${forbidden}' found in Repository layer at ${filePath}. Repository must only consume data via Interfaces.`);
        }
      }
    });
  });

  test('AI subsystem must be deterministic and pure', () => {
    const aiPath = path.resolve(__dirname, '../../src/ai');
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

    walkDir(aiPath, (filePath) => {
      const content = fs.readFileSync(filePath, 'utf8');
      
      for (const forbidden of forbiddenImports) {
        const regex = new RegExp(`from\\s+['"]${forbidden}.*?['"]`, 'i');
        if (regex.test(content)) {
          throw new Error(`Forbidden import '${forbidden}' found in AI layer at ${filePath}. AI must consume AITriageInput only.`);
        }
      }

      if (content.includes('Math.random()')) {
        throw new Error(`Forbidden non-deterministic logic 'Math.random()' found in AI layer at ${filePath}. AI heuristics must be deterministic.`);
      }
    });
  });
});
