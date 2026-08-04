import { AllRule, AnyRule } from '../../src/rules/composition/CompositeRules';
import { EvaluationContext } from '../../src/rules/context/EvaluationContext';
import { IRule, IRuleMetadata, IValidationResult, IEvaluationContext, IRuleExplanation, RuleCategory, RuleOwner } from '../../src/rules/interfaces';

class MockRule implements IRule {
  constructor(private id: string, private shouldPass: boolean) {}

  public get metadata(): IRuleMetadata {
    return {
      ruleId: this.id,
      knowledgeRuleId: `K-${this.id}`,
      category: RuleCategory.Validation,
      priority: 1,
      tags: [],
      dependencies: [],
      owner: RuleOwner.CRM,
      preconditions: { requiredSnapshots: [], requiredArtifacts: [], requiredEvidence: [] },
      version: '1.0',
      createdDate: new Date().toISOString(),
      modifiedDate: new Date().toISOString(),
      deprecated: false
    };
  }

  public async evaluate(context: IEvaluationContext): Promise<IValidationResult> {
    return {
      ruleId: this.id,
      knowledgeRuleId: `K-${this.id}`,
      scenarioId: context.executionMetadata.runId,
      status: this.shouldPass ? 'PASSED' : 'FAILED',
      severity: 'HIGH',
      confidence: 100,
      evidence: [],
      diagnostics: {},
      explanation: await this.explain(context),
      durationMs: 10
    };
  }

  public async explain(context: IEvaluationContext): Promise<IRuleExplanation> {
    return {
      inputs: {},
      expected: true,
      actual: this.shouldPass,
      difference: '',
      reason: 'Mock rule'
    };
  }
}

describe('Rule Composition Engine', () => {
  let context: EvaluationContext;

  beforeEach(() => {
    context = new EvaluationContext('test-run');
  });

  test('AllRule should pass if all children pass', async () => {
    const all = new AllRule('all-1', [
      new MockRule('r1', true),
      new MockRule('r2', true)
    ]);
    const result = await all.evaluate(context);
    expect(result.status).toBe('PASSED');
  });

  test('AllRule should fail if any child fails', async () => {
    const all = new AllRule('all-2', [
      new MockRule('r1', true),
      new MockRule('r2', false)
    ]);
    const result = await all.evaluate(context);
    expect(result.status).toBe('FAILED');
    expect(result.explanation.reason).toContain('Child rule r2 failed');
  });

  test('AnyRule should pass if at least one child passes', async () => {
    const any = new AnyRule('any-1', [
      new MockRule('r1', false),
      new MockRule('r2', true)
    ]);
    const result = await any.evaluate(context);
    expect(result.status).toBe('PASSED');
  });

  test('AnyRule should fail if all children fail', async () => {
    const any = new AnyRule('any-2', [
      new MockRule('r1', false),
      new MockRule('r2', false)
    ]);
    const result = await any.evaluate(context);
    expect(result.status).toBe('FAILED');
    expect(result.explanation.reason).toContain('All child rules failed');
  });
});
