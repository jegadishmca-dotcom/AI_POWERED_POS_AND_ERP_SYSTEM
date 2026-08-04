const { AllRule, AnyRule } = require('./dist/src/rules/composition/CompositeRules');
const { EvaluationContext } = require('./dist/src/rules/context/EvaluationContext');
const { RuleCategory } = require('./dist/src/rules/interfaces');

class MockRule {
  constructor(id, shouldPass) {
    this.id = id;
    this.shouldPass = shouldPass;
  }

  get metadata() {
    return {
      ruleId: this.id,
      knowledgeRuleId: `K-${this.id}`,
      category: RuleCategory.Validation,
      priority: 1,
      tags: [],
      dependencies: [],
      version: '1.0',
      createdDate: new Date().toISOString(),
      modifiedDate: new Date().toISOString(),
      deprecated: false
    };
  }

  async evaluate(context) {
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

  async explain(context) {
    return {
      inputs: {},
      expected: true,
      actual: this.shouldPass,
      difference: '',
      reason: 'Mock rule'
    };
  }
}

async function runRuleTests() {
  console.log('Running Rule Engine tests...');
  const context = new EvaluationContext('test-run');

  const all1 = new AllRule('all-1', [new MockRule('r1', true), new MockRule('r2', true)]);
  const res1 = await all1.evaluate(context);
  if (res1.status !== 'PASSED') throw new Error('AllRule true-true failed');

  const all2 = new AllRule('all-2', [new MockRule('r1', true), new MockRule('r2', false)]);
  const res2 = await all2.evaluate(context);
  if (res2.status !== 'FAILED') throw new Error('AllRule true-false passed');

  const any1 = new AnyRule('any-1', [new MockRule('r1', false), new MockRule('r2', true)]);
  const res3 = await any1.evaluate(context);
  if (res3.status !== 'PASSED') throw new Error('AnyRule false-true failed');

  const any2 = new AnyRule('any-2', [new MockRule('r1', false), new MockRule('r2', false)]);
  const res4 = await any2.evaluate(context);
  if (res4.status !== 'FAILED') throw new Error('AnyRule false-false passed');

  console.log('Rule Engine tests passed successfully!');
}

runRuleTests().catch(e => {
  console.error(e);
  process.exit(1);
});
