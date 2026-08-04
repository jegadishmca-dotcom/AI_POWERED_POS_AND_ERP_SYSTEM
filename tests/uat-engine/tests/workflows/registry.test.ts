import { WorkflowRegistry } from '../../src/workflows/registry/WorkflowRegistry';
import { CashSaleWorkflow } from '../../src/workflows/sales/SalesWorkflows';

describe('Workflow Registry', () => {
  let registry: WorkflowRegistry;

  beforeEach(() => {
    registry = new WorkflowRegistry();
  });

  test('should register and retrieve a workflow successfully', () => {
    registry.register(CashSaleWorkflow);
    const wf = registry.getWorkflow(CashSaleWorkflow.workflowId);
    expect(wf).toBeDefined();
    expect(wf?.name).toBe('Cash Sale');
  });

  test('should throw error when registering duplicate workflow ID', () => {
    registry.register(CashSaleWorkflow);
    expect(() => registry.register(CashSaleWorkflow)).toThrow(/already registered/);
  });

  test('should return all workflows', () => {
    registry.register(CashSaleWorkflow);
    const all = registry.getAllWorkflows();
    expect(all.length).toBe(1);
    expect(all[0].workflowId).toBe(CashSaleWorkflow.workflowId);
  });
});
