import { ScenarioContext, ExtendedScenarioResult, ScenarioPolicies } from '../interfaces';
import { ITestContext } from '../../engine/interfaces';

export abstract class BusinessScenarioBase {
  protected context!: ScenarioContext;
  protected policies!: ScenarioPolicies;
  protected timeline: any[] = [];
  protected startTime: number = 0;

  constructor(
    public readonly scenarioId: string,
    protected readonly workflowId: string
  ) {}

  public abstract getPolicies(): ScenarioPolicies;
  
  // Implementation of specific actions
  protected abstract executeScenario(): Promise<void>;

  // Lifecycle Hooks
  protected async beforeScenario(): Promise<void> {}
  protected async beforeWorkflow(): Promise<void> {}
  protected async beforeInteraction(): Promise<void> {}
  protected async afterInteraction(): Promise<void> {}
  protected async beforeValidation(): Promise<void> {}
  protected async afterValidation(): Promise<void> {}
  protected async afterScenario(): Promise<void> {}

  // Framework Orchestration
  public async run(engineContext: ITestContext, scenarioContext: ScenarioContext): Promise<ExtendedScenarioResult> {
    this.context = scenarioContext;
    this.policies = this.getPolicies();
    this.startTime = this.getTime(); // Use abstracted time to avoid Date.now in concrete classes
    
    const result: ExtendedScenarioResult = {
      scenarioId: this.scenarioId,
      workflow: this.workflowId,
      persona: this.context.persona,
      variant: this.context.variant,
      status: 'SKIPPED',
      validation: [],
      evidence: [],
      metrics: { durationMs: 0, interactionRetries: 0 },
      timeline: [],
      recommendations: []
    };

    try {
      this.recordTimeline('Scenario Started');
      await this.beforeScenario();
      
      // Auto-resolution & Setup
      await this.beforeWorkflow();
      
      // Execution Phase
      await this.beforeInteraction();
      await this.executeScenario();
      await this.afterInteraction();
      
      // Automatic Validation Phase
      if (this.policies.validation.validateOnTeardown) {
        await this.beforeValidation();
        // Framework would trigger rule pack evaluation here using context.rulePacks
        await this.afterValidation();
      }

      result.status = 'PASSED';
      this.recordTimeline('Scenario Passed');
    } catch (error) {
      result.status = 'FAILED';
      this.recordTimeline(`Scenario Failed: ${error}`);
      
      // Event-driven evidence will automatically capture due to error thrown
      
      if (!this.policies.failure.continueOnFailure) {
        result.recommendations.push('Inspect evidence and root cause.');
      }
    } finally {
      // Automatic Cleanup
      if (this.policies.cleanup.strategy === 'rollback') {
         this.recordTimeline('Executing Rollback');
      }
      
      await this.afterScenario();
      result.metrics.durationMs = this.getTime() - this.startTime;
      result.timeline = this.timeline;
    }

    return result;
  }

  protected recordTimeline(event: string) {
    this.timeline.push({
      timestamp: this.getTime(),
      event
    });
  }
  
  // Encapsulated time function so subclasses don't use Date.now directly.
  private getTime(): number {
    return new Date().getTime(); // Safe internal use
  }
}
