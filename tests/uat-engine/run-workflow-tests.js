const { WorkflowRegistry } = require('./dist/src/workflows/registry/WorkflowRegistry');
const { CashSaleWorkflow } = require('./dist/src/workflows/sales/SalesWorkflows');

async function runTests() {
  console.log('Running Workflow Registry tests...');
  const registry = new WorkflowRegistry();

  registry.register(CashSaleWorkflow);
  
  const wf = registry.getWorkflow(CashSaleWorkflow.workflowId);
  if (!wf || wf.name !== 'Cash Sale') throw new Error('Failed to retrieve CashSaleWorkflow');

  try {
    registry.register(CashSaleWorkflow);
    throw new Error('Should have thrown on duplicate register');
  } catch(e) {
    if (!e.message.includes('already registered')) throw e;
  }

  const all = registry.getAllWorkflows();
  if (all.length !== 1 || all[0].workflowId !== CashSaleWorkflow.workflowId) throw new Error('getAllWorkflows failed');

  console.log('Workflow Registry tests passed successfully!');
}

runTests().catch(e => {
  console.error(e);
  process.exit(1);
});
