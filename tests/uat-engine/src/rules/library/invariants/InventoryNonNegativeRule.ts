import { IRule, IRuleMetadata, IValidationResult, IEvaluationContext, IRuleExplanation, RuleCategory, RuleOwner } from '../../interfaces';

export class InventoryNonNegativeRule implements IRule {
  public get metadata(): IRuleMetadata {
    return {
      ruleId: 'INV-INVARIANT-01',
      knowledgeRuleId: 'INV-01',
      category: RuleCategory.Consistency,
      priority: 100,
      tags: ['invariant', 'inventory'],
      dependencies: [],
      owner: RuleOwner.Inventory,
      preconditions: {
        requiredSnapshots: ['postTxnInventory'],
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
    const inventory = context.snapshots['postTxnInventory'];
    const explanation = await this.explain(context);

    // inventory is expected to be an array of stock items
    let hasNegative = false;
    let negativeItem = '';

    if (Array.isArray(inventory)) {
      for (const item of inventory) {
        if (item.stock < 0) {
          hasNegative = true;
          negativeItem = item.sku;
          break;
        }
      }
    }

    if (hasNegative) {
      explanation.actual = `Negative stock found for SKU ${negativeItem}`;
      explanation.difference = 'Stock < 0';
      explanation.reason = 'Business Invariant Violation: Stock cannot be negative.';
      return this.createResult('FAILED', explanation, Date.now() - start, context.executionMetadata.runId);
    }

    explanation.actual = 'All stock >= 0';
    return this.createResult('PASSED', explanation, Date.now() - start, context.executionMetadata.runId);
  }

  public async explain(context: IEvaluationContext): Promise<IRuleExplanation> {
    return {
      inputs: { snapshot: 'postTxnInventory' },
      expected: 'All inventory stock >= 0',
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
      reasoningContext: { invariant: 'Inventory >= 0' },
      durationMs
    };
  }
}
