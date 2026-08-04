import { IRule, IRuleMetadata, IValidationResult, IEvaluationContext, IRuleExplanation, RuleCategory, RuleOwner } from '../../interfaces';

export class DebitEqualsCreditRule implements IRule {
  public get metadata(): IRuleMetadata {
    return {
      ruleId: 'FIN-INVARIANT-01',
      knowledgeRuleId: 'FIN-01',
      category: RuleCategory.Consistency,
      priority: 100,
      tags: ['invariant', 'finance', 'ledger'],
      dependencies: [],
      owner: RuleOwner.Finance,
      preconditions: {
        requiredSnapshots: ['postTxnLedger'],
        requiredArtifacts: [],
        requiredEvidence: []
      },
      version: '1.0.0',
      createdDate: new Date().toISOString(),
      modifiedDate: new Date().toISOString(),
      deprecated: false
    };
  }

  public async evaluate(context: IEvaluationContext): Promise<IValidationResult> {
    const start = Date.now();
    const ledger = context.snapshots['postTxnLedger'];
    const explanation = await this.explain(context);

    let totalDebit = 0;
    let totalCredit = 0;

    if (Array.isArray(ledger)) {
      for (const entry of ledger) {
        totalDebit += (entry.debit || 0);
        totalCredit += (entry.credit || 0);
      }
    }

    if (Math.abs(totalDebit - totalCredit) > 0.001) {
      explanation.actual = `Debit: ${totalDebit}, Credit: ${totalCredit}`;
      explanation.difference = `Diff: ${Math.abs(totalDebit - totalCredit)}`;
      explanation.reason = 'Business Invariant Violation: Double entry accounting requires Debit == Credit.';
      return this.createResult('FAILED', explanation, Date.now() - start, context.executionMetadata.runId);
    }

    explanation.actual = `Debit: ${totalDebit}, Credit: ${totalCredit}`;
    return this.createResult('PASSED', explanation, Date.now() - start, context.executionMetadata.runId);
  }

  public async explain(context: IEvaluationContext): Promise<IRuleExplanation> {
    return {
      inputs: { snapshot: 'postTxnLedger' },
      expected: 'Sum(Debit) == Sum(Credit)',
      actual: '',
      difference: '',
      reason: ''
    };
  }

  private createResult(status: 'PASSED'|'FAILED', explanation: IRuleExplanation, durationMs: number, runId: string): IValidationResult {
    return {
      ruleId: this.metadata.ruleId,
      knowledgeRuleId: this.metadata.knowledgeRuleId,
      scenarioId: runId,
      status,
      severity: 'CRITICAL',
      confidence: 100,
      evidence: [],
      diagnostics: {},
      explanation,
      reasoningContext: { invariant: 'Debit == Credit' },
      durationMs
    };
  }
}
