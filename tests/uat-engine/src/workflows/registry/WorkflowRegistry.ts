import { IWorkflowDefinition } from '../interfaces';

export class WorkflowRegistry {
  private workflows: Map<string, IWorkflowDefinition> = new Map();

  public register(workflow: IWorkflowDefinition): void {
    if (this.workflows.has(workflow.workflowId)) {
      throw new Error(`Workflow ${workflow.workflowId} is already registered.`);
    }
    this.workflows.set(workflow.workflowId, workflow);
  }

  public getWorkflow(id: string): IWorkflowDefinition | undefined {
    return this.workflows.get(id);
  }

  public getAllWorkflows(): IWorkflowDefinition[] {
    return Array.from(this.workflows.values());
  }
}
