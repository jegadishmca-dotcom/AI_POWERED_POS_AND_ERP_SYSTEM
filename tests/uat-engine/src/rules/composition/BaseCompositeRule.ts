import { IRule, IRuleMetadata, IValidationResult, IEvaluationContext, IRuleExplanation, RuleCategory, RuleOwner } from '../interfaces';

export abstract class BaseCompositeRule implements IRule {
  constructor(
    public readonly id: string,
    protected rules: IRule[]
  ) {}

  public get metadata(): IRuleMetadata {
    return {
      ruleId: this.id,
      knowledgeRuleId: 'COMPOSITE',
      category: RuleCategory.Configuration,
      priority: 0,
      tags: ['composite'],
      dependencies: [],
      owner: RuleOwner.Security, // Default placeholder
      preconditions: { requiredSnapshots: [], requiredArtifacts: [], requiredEvidence: [] },
      version: '1.0.0',
      createdDate: new Date().toISOString(),
      modifiedDate: new Date().toISOString(),
      deprecated: false
    };
  }

  public abstract evaluate(context: IEvaluationContext): Promise<IValidationResult>;
  public abstract explain(context: IEvaluationContext): Promise<IRuleExplanation>;
  
  protected createBaseResult(status: 'PASSED' | 'FAILED', durationMs: number, context: IEvaluationContext): IValidationResult {
    return {
      ruleId: this.id,
      knowledgeRuleId: 'COMPOSITE',
      scenarioId: context.executionMetadata?.runId || 'unknown',
      status,
      severity: 'HIGH',
      confidence: 100,
      evidence: [],
      diagnostics: { childCount: this.rules.length },
      explanation: {
        inputs: {},
        expected: null,
        actual: null,
        difference: '',
        reason: ''
      },
      durationMs
    };
  }
}
