import * as fs from 'fs';
import * as path from 'path';
import { RuleRegistry } from '../registry/RuleRegistry';
import { InventoryNonNegativeRule } from '../library/invariants/InventoryNonNegativeRule';
import { DebitEqualsCreditRule } from '../library/invariants/DebitEqualsCreditRule';
import { CashSalePack } from '../packs/CashSalePack';

function generateRuleCatalog(registry: RuleRegistry, outDir: string) {
  const rules = registry.getAllRules();
  let markdown = `# Rule Catalog\n\n`;
  markdown += `| Rule ID | Knowledge ID | Module | Category | Version | Dependencies |\n`;
  markdown += `|---------|--------------|--------|----------|---------|--------------|\n`;

  for (const rule of rules) {
    const meta = rule.metadata;
    const deps = meta.dependencies.length ? meta.dependencies.join(', ') : 'None';
    markdown += `| ${meta.ruleId} | ${meta.knowledgeRuleId} | ${meta.owner} | ${meta.category} | ${meta.version} | ${deps} |\n`;
  }

  fs.writeFileSync(path.join(outDir, 'RuleCatalog.md'), markdown);
  console.log('RuleCatalog.md generated.');
}

function generateDependencyGraph(registry: RuleRegistry, outDir: string) {
  const rules = registry.getAllRules();
  let mermaid = `\`\`\`mermaid\ngraph TD;\n`;

  for (const rule of rules) {
    const meta = rule.metadata;
    for (const dep of meta.dependencies) {
      mermaid += `  ${dep} --> ${meta.ruleId};\n`;
    }
    if (meta.dependencies.length === 0) {
      mermaid += `  ${meta.ruleId};\n`;
    }
  }
  mermaid += `\`\`\`\n`;

  fs.writeFileSync(path.join(outDir, 'RuleDependencyGraph.md'), mermaid);
  console.log('RuleDependencyGraph.md generated.');
}

function generateCoverageReport(registry: RuleRegistry, outDir: string, knowledgeDir: string) {
  // Simple extraction of POS-01, INV-01 from knowledge files
  const knowledgeFiles = fs.readdirSync(knowledgeDir).filter(f => f.endsWith('.md'));
  const allKnowledgeIds = new Set<string>();

  for (const file of knowledgeFiles) {
    const content = fs.readFileSync(path.join(knowledgeDir, file), 'utf8');
    const matches = content.match(/[A-Z]+-\d+/g);
    if (matches) {
      matches.forEach(m => allKnowledgeIds.add(m));
    }
  }

  const rules = registry.getAllRules();
  const implementedIds = new Set<string>();
  for (const rule of rules) {
    if (rule.metadata.knowledgeRuleId !== 'COMPOSITE') {
      implementedIds.add(rule.metadata.knowledgeRuleId);
    }
  }

  let markdown = `# Rule Coverage Report\n\n`;
  markdown += `**Total Knowledge Rules Discovered:** ${allKnowledgeIds.size}\n\n`;
  markdown += `**Total Rules Implemented:** ${implementedIds.size}\n\n`;
  
  const coverage = allKnowledgeIds.size > 0 ? ((implementedIds.size / allKnowledgeIds.size) * 100).toFixed(2) : 100;
  markdown += `**Coverage:** ${coverage}%\n\n`;
  
  markdown += `## Missing Implementations\n`;
  for (const id of allKnowledgeIds) {
    if (!implementedIds.has(id)) {
      markdown += `- [ ] ${id}\n`;
    }
  }

  fs.writeFileSync(path.join(outDir, 'RuleCoverageReport.md'), markdown);
  console.log('RuleCoverageReport.md generated.');
}

async function main() {
  const registry = new RuleRegistry();
  
  // Register rules
  registry.register(new InventoryNonNegativeRule());
  registry.register(new DebitEqualsCreditRule());
  // Note: CashSalePack is a composite, normally we might register packs separately, but we can register its rules or the pack itself.
  registry.register(new CashSalePack());

  const outDir = path.resolve(__dirname, '../../../artifacts');
  if (!fs.existsSync(outDir)) fs.mkdirSync(outDir, { recursive: true });

  const knowledgeDir = path.resolve(__dirname, '../../../../knowledge');

  generateRuleCatalog(registry, outDir);
  generateDependencyGraph(registry, outDir);
  generateCoverageReport(registry, outDir, knowledgeDir);
}

main().catch(console.error);
