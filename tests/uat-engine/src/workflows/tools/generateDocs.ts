import * as fs from 'fs';
import * as path from 'path';
import { WorkflowRegistry } from '../registry/WorkflowRegistry';
import { CashSaleWorkflow, SalesReturnWorkflow } from '../sales/SalesWorkflows';
import { PurchaseWorkflow, ReceiveGRNWorkflow } from '../purchasing/PurchasingWorkflows';
import { CustomerRegistrationWorkflow } from '../crm/CRMWorkflows';
import { StockTransferWorkflow, InventoryCountWorkflow } from '../inventory/InventoryWorkflows';
import { DayCloseWorkflow, SupplierPaymentWorkflow } from '../finance/FinanceWorkflows';

function generateWorkflowCatalog(registry: WorkflowRegistry, outDir: string) {
  const workflows = registry.getAllWorkflows();
  let markdown = `# Workflow Catalog\n\n`;
  markdown += `| Workflow ID | Name | Module | Persona | Risk | Variants | Duration (ms) |\n`;
  markdown += `|-------------|------|--------|---------|------|----------|---------------|\n`;

  for (const wf of workflows) {
    const variants = wf.variants.join(', ');
    markdown += `| ${wf.workflowId} | ${wf.name} | ${wf.businessModule} | ${wf.requiredPersona} | ${wf.riskClassification} | ${variants} | ${wf.estimatedDurationMs} |\n`;
  }

  fs.writeFileSync(path.join(outDir, 'WorkflowCatalog.md'), markdown);
  console.log('WorkflowCatalog.md generated.');
}

function generateDependencyGraph(registry: WorkflowRegistry, outDir: string) {
  const workflows = registry.getAllWorkflows();
  let mermaid = `\`\`\`mermaid\ngraph TD;\n`;

  for (const wf of workflows) {
    for (const dep of wf.prerequisites) {
      mermaid += `  ${dep} --> ${wf.workflowId};\n`;
    }
    if (wf.prerequisites.length === 0) {
      mermaid += `  ${wf.workflowId};\n`;
    }
  }
  mermaid += `\`\`\`\n`;

  fs.writeFileSync(path.join(outDir, 'WorkflowDependencyGraph.md'), mermaid);
  console.log('WorkflowDependencyGraph.md generated.');
}

function generateCapabilityReport(registry: WorkflowRegistry, outDir: string) {
  const workflows = registry.getAllWorkflows();
  const caps = new Map<string, string[]>();

  for (const wf of workflows) {
    for (const cap of wf.businessCapabilities) {
      if (!caps.has(cap)) caps.set(cap, []);
      caps.get(cap)!.push(wf.workflowId);
    }
  }

  let markdown = `# Workflow Capability Report\n\n`;
  for (const [cap, wfs] of caps.entries()) {
    markdown += `### ${cap}\n`;
    for (const wf of wfs) {
      markdown += `- ${wf}\n`;
    }
    markdown += `\n`;
  }

  fs.writeFileSync(path.join(outDir, 'WorkflowCapabilityReport.md'), markdown);
  console.log('WorkflowCapabilityReport.md generated.');
}

function generateRiskMatrix(registry: WorkflowRegistry, outDir: string) {
  const workflows = registry.getAllWorkflows();
  let markdown = `# Workflow Risk Matrix\n\n`;
  markdown += `| Risk Level | Workflows |\n`;
  markdown += `|------------|-----------|\n`;

  const risks = ['Critical', 'High', 'Medium', 'Low'];
  for (const risk of risks) {
    const wfs = workflows.filter(w => w.riskClassification === risk).map(w => w.workflowId).join(', ');
    markdown += `| **${risk}** | ${wfs} |\n`;
  }

  fs.writeFileSync(path.join(outDir, 'WorkflowRiskMatrix.md'), markdown);
  console.log('WorkflowRiskMatrix.md generated.');
}

function generateCoverageReport(registry: WorkflowRegistry, outDir: string, knowledgeDir: string) {
  const workflows = registry.getAllWorkflows();
  const allModules = new Set(['Inventory', 'Finance', 'CRM', 'GST', 'Purchasing', 'POS', 'Security', 'Reports']);
  const coveredModules = new Set(workflows.map(w => w.businessModule.toUpperCase()));

  let markdown = `# Workflow Coverage Report\n\n`;
  markdown += `**Total Knowledge Modules:** ${allModules.size}\n\n`;
  
  markdown += `## Covered Modules\n`;
  for (const wf of workflows) {
    markdown += `- ${wf.businessModule}: ${wf.workflowId} (${wf.name})\n`;
  }

  fs.writeFileSync(path.join(outDir, 'WorkflowCoverageReport.md'), markdown);
  console.log('WorkflowCoverageReport.md generated.');
}

async function main() {
  const registry = new WorkflowRegistry();
  
  registry.register(CashSaleWorkflow);
  registry.register(SalesReturnWorkflow);
  registry.register(PurchaseWorkflow);
  registry.register(ReceiveGRNWorkflow);
  registry.register(CustomerRegistrationWorkflow);
  registry.register(StockTransferWorkflow);
  registry.register(InventoryCountWorkflow);
  registry.register(DayCloseWorkflow);
  registry.register(SupplierPaymentWorkflow);

  const outDir = path.resolve(__dirname, '../../../artifacts');
  if (!fs.existsSync(outDir)) fs.mkdirSync(outDir, { recursive: true });

  const knowledgeDir = path.resolve(__dirname, '../../../../knowledge');

  generateWorkflowCatalog(registry, outDir);
  generateDependencyGraph(registry, outDir);
  generateCapabilityReport(registry, outDir);
  generateRiskMatrix(registry, outDir);
  generateCoverageReport(registry, outDir, knowledgeDir);
}

main().catch(console.error);
