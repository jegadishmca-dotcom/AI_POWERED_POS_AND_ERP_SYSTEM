import { IEvaluationContext } from '../interfaces';

export class EvaluationContext implements IEvaluationContext {
  public scenarioMetadata: any = {};
  public knowledgeReferences: Record<string, string> = {};
  public artifacts: Record<string, any> = {};
  public snapshots: Record<string, any> = {};
  public configuration: Record<string, any> = {};
  public evidence: Record<string, string> = {};
  public executionMetadata: { timestamp: number; runId: string; };

  constructor(runId: string) {
    this.executionMetadata = {
      timestamp: Date.now(),
      runId
    };
  }
}
