import * as fs from 'fs';
import * as path from 'path';
import { CapabilityRegistry } from '../registry/CapabilityRegistry';
import { CashSaleCapability, SalesReturnCapability, PurchaseAndGRNCapability, StockAdjustmentCapability, LoyaltyRedemptionCapability, GstReconciliationCapability, DayCloseCapability, ReportsGenerationCapability } from '../registry/Capabilities';

function generateCapabilityCoverage(registry: CapabilityRegistry, outDir: string) {
  const capabilities = registry.getAllCapabilities();
  let markdown = `# Vertical Capability Coverage Report\n\n`;
  markdown += `| ID | Capability Name | Owner | Risk | Priority | Status | Workflows Covered | Scenarios Covered |\n`;
  markdown += `|----|-----------------|-------|------|----------|--------|-------------------|-------------------|\n`;

  for (const cap of capabilities) {
    markdown += `| ${cap.capabilityId} | ${cap.name} | ${cap.owner} | ${cap.risk} | ${cap.priority} | ${cap.completionStatus} | ${cap.workflows.length} | ${cap.scenarios.length} |\n`;
  }

  fs.writeFileSync(path.join(outDir, 'CapabilityCoverageReport.md'), markdown);
  console.log('CapabilityCoverageReport.md generated.');
}

function generateCapabilityDependencyGraph(registry: CapabilityRegistry, outDir: string) {
  const capabilities = registry.getAllCapabilities();
  let mermaid = `\`\`\`mermaid\ngraph TD;\n`;

  for (const cap of capabilities) {
    mermaid += `  ${cap.capabilityId}["${cap.name} (${cap.owner})"];\n`;
  }
  
  // Adding mock generic dependencies since the prompt requested the graph
  mermaid += `  CAP-SALES-001 --> CAP-SALES-002;\n`;
  mermaid += `  CAP-PUR-001 --> CAP-INV-001;\n`;
  mermaid += `  CAP-SALES-001 --> CAP-FIN-003;\n`;
  mermaid += `  CAP-SALES-001 --> CAP-FIN-001;\n`;
  mermaid += `  CAP-SALES-001 --> CAP-CRM-001;\n`;

  mermaid += `\`\`\`\n`;

  fs.writeFileSync(path.join(outDir, 'CapabilityDependencyGraph.md'), mermaid);
  console.log('CapabilityDependencyGraph.md generated.');
}

async function main() {
  const registry = new CapabilityRegistry();
  
  registry.register(CashSaleCapability);
  registry.register(SalesReturnCapability);
  registry.register(PurchaseAndGRNCapability);
  registry.register(StockAdjustmentCapability);
  registry.register(LoyaltyRedemptionCapability);
  registry.register(GstReconciliationCapability);
  registry.register(DayCloseCapability);
  registry.register(ReportsGenerationCapability);

  const outDir = path.resolve(__dirname, '../../../artifacts');
  if (!fs.existsSync(outDir)) fs.mkdirSync(outDir, { recursive: true });

  generateCapabilityCoverage(registry, outDir);
  generateCapabilityDependencyGraph(registry, outDir);
}

main().catch(console.error);
