import { IRule, IValidationResult, IEvaluationContext } from '../interfaces';

export class RulePipeline {
  private preprocessors: Array<(context: IEvaluationContext) => Promise<void>> = [];
  private postprocessors: Array<(result: IValidationResult, context: IEvaluationContext) => Promise<void>> = [];
  
  public metrics = {
    runs: 0,
    passes: 0,
    failures: 0,
    totalDuration: 0
  };

  public addPreprocessor(processor: (context: IEvaluationContext) => Promise<void>): void {
    this.preprocessors.push(processor);
  }

  public addPostprocessor(processor: (result: IValidationResult, context: IEvaluationContext) => Promise<void>): void {
    this.postprocessors.push(processor);
  }

  public async execute(rule: IRule, context: IEvaluationContext): Promise<IValidationResult> {
    const preconditions = rule.metadata.preconditions;
    
    // Check Preconditions (Skip evaluation if missing)
    for (const snap of preconditions.requiredSnapshots) {
      if (!context.snapshots[snap]) {
        return this.createSkippedResult(rule, context, `Missing required snapshot: ${snap}`);
      }
    }
    for (const art of preconditions.requiredArtifacts) {
      if (!context.artifacts[art]) {
        return this.createSkippedResult(rule, context, `Missing required artifact: ${art}`);
      }
    }
    for (const ev of preconditions.requiredEvidence) {
      if (!context.evidence[ev]) {
        return this.createSkippedResult(rule, context, `Missing required evidence: ${ev}`);
      }
    }

    // 1. Preprocessing
    for (const pre of this.preprocessors) {
      await pre(context);
    }

    // 2. Validation
    this.metrics.runs++;
    const start = Date.now();
    const result = await rule.evaluate(context);
    const duration = Date.now() - start;
    
    this.metrics.totalDuration += duration;
    if (result.status === 'PASSED') {
      this.metrics.passes++;
    } else if (result.status === 'FAILED') {
      this.metrics.failures++;
    }

    // 3. Postprocessing
    for (const post of this.postprocessors) {
      await post(result, context);
    }

    return result;
  }

  private createSkippedResult(rule: IRule, context: IEvaluationContext, reason: string): IValidationResult {
    return {
      ruleId: rule.metadata.ruleId,
      knowledgeRuleId: rule.metadata.knowledgeRuleId,
      scenarioId: context.executionMetadata.runId,
      status: 'SKIPPED',
      severity: 'LOW',
      confidence: 0,
      evidence: [],
      diagnostics: {},
      explanation: {
        inputs: {},
        expected: 'Preconditions met',
        actual: 'Preconditions missing',
        difference: '',
        reason
      },
      durationMs: 0
    };
  }
}
