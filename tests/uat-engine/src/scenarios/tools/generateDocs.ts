import * as fs from 'fs';
import * as path from 'path';
import { ScenarioRegistry } from '../registry/ScenarioRegistry';
import { CashSaleSuccessScenario } from '../sales/CashSaleScenario';

function generateScenarioCatalog(registry: ScenarioRegistry, outDir: string) {
  const scenarios = registry.getAllScenarios();
  let markdown = `# Scenario Catalog\n\n`;
  markdown += `| Scenario ID | Linked Workflow | Cleanup Strategy | Retries |\n`;
  markdown += `|-------------|-----------------|------------------|---------|\n`;

  for (const sc of scenarios) {
    const policies = sc.getPolicies();
    markdown += `| ${sc.scenarioId} | ${(sc as any).workflowId} | ${policies.cleanup.strategy} | ${policies.retry.maxRetries} |\n`;
  }

  fs.writeFileSync(path.join(outDir, 'ScenarioCatalog.md'), markdown);
  console.log('ScenarioCatalog.md generated.');
}

function generateDependencyGraph(registry: ScenarioRegistry, outDir: string) {
  const scenarios = registry.getAllScenarios();
  let mermaid = `\`\`\`mermaid\ngraph TD;\n`;

  for (const sc of scenarios) {
    // Simulating dependency graph based on Workflow prerequisites (which would normally be resolved via WorkflowRegistry)
    mermaid += `  ${(sc as any).workflowId} --> ${sc.scenarioId};\n`;
  }
  mermaid += `\`\`\`\n`;

  fs.writeFileSync(path.join(outDir, 'ScenarioDependencyGraph.md'), mermaid);
  console.log('ScenarioDependencyGraph.md generated.');
}

function generateReplayJson(registry: ScenarioRegistry, outDir: string) {
  const scenarios = registry.getAllScenarios();
  const replayData = scenarios.map(sc => ({
    id: sc.scenarioId,
    workflow: (sc as any).workflowId,
    policies: sc.getPolicies()
  }));

  fs.writeFileSync(path.join(outDir, 'ScenarioReplay.json'), JSON.stringify(replayData, null, 2));
  console.log('ScenarioReplay.json generated.');
}

async function main() {
  const registry = new ScenarioRegistry();
  registry.register(new CashSaleSuccessScenario());

  const outDir = path.resolve(__dirname, '../../../artifacts');
  if (!fs.existsSync(outDir)) fs.mkdirSync(outDir, { recursive: true });

  generateScenarioCatalog(registry, outDir);
  generateDependencyGraph(registry, outDir);
  generateReplayJson(registry, outDir);
  
  // Coverage report would map these scenarios to the Workflow Catalog
  fs.writeFileSync(path.join(outDir, 'ScenarioCoverageReport.md'), '# Scenario Coverage Report\n\nTotal Workflows Covered: 1\n');
  console.log('ScenarioCoverageReport.md generated.');
}

main().catch(console.error);
