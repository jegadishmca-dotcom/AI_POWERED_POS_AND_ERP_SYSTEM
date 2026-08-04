import { BaseCompositeRule } from './BaseCompositeRule';
import { IValidationResult, IEvaluationContext, IRuleExplanation } from '../interfaces';

export class AllRule extends BaseCompositeRule {
  public async evaluate(context: IEvaluationContext): Promise<IValidationResult> {
    const start = Date.now();
    for (const rule of this.rules) {
      const result = await rule.evaluate(context);
      if (result.status !== 'PASSED') {
        const res = this.createBaseResult('FAILED', Date.now() - start, context);
        res.explanation = await this.explain(context);
        res.explanation.reason = `Child rule ${rule.metadata.ruleId} failed.`;
        return res;
      }
    }
    const res = this.createBaseResult('PASSED', Date.now() - start, context);
    res.explanation = await this.explain(context);
    return res;
  }

  public async explain(context: IEvaluationContext): Promise<IRuleExplanation> {
    return {
      inputs: { rules: this.rules.map(r => r.metadata.ruleId) },
      expected: 'All rules PASSED',
      actual: 'Determined at runtime',
      difference: '',
      reason: 'Logical AND composition'
    };
  }
}

export class AnyRule extends BaseCompositeRule {
  public async evaluate(context: IEvaluationContext): Promise<IValidationResult> {
    const start = Date.now();
    for (const rule of this.rules) {
      const result = await rule.evaluate(context);
      if (result.status === 'PASSED') {
        const res = this.createBaseResult('PASSED', Date.now() - start, context);
        res.explanation = await this.explain(context);
        return res;
      }
    }
    const res = this.createBaseResult('FAILED', Date.now() - start, context);
    res.explanation = await this.explain(context);
    res.explanation.reason = 'All child rules failed.';
    return res;
  }

  public async explain(context: IEvaluationContext): Promise<IRuleExplanation> {
    return {
      inputs: { rules: this.rules.map(r => r.metadata.ruleId) },
      expected: 'At least one rule PASSED',
      actual: 'Determined at runtime',
      difference: '',
      reason: 'Logical OR composition'
    };
  }
}
