import { IWorkflowDefinition, Persona, WorkflowVariant, BusinessDataProfile } from '../workflows/interfaces';
import { IValidationResult } from '../rules/interfaces';

export interface ValidationPolicy {
  validateOnStep: boolean;
  validateOnTeardown: boolean;
  failFast: boolean;
}

export interface CleanupPolicy {
  strategy: 'rollback' | 'keep-test-data' | 'delete-test-data' | 'archive';
}

export interface EvidencePolicy {
  captureScreenshots: 'always' | 'on-failure' | 'never';
  captureTrace: boolean;
  captureNetwork: boolean;
}

export interface RetryPolicy {
  maxRetries: number;
  retryOnFailure: boolean;
}

export interface FailurePolicy {
  continueOnFailure: boolean;
}

export interface ScenarioPolicies {
  validation: ValidationPolicy;
  cleanup: CleanupPolicy;
  evidence: EvidencePolicy;
  retry: RetryPolicy;
  failure: FailurePolicy;
}

export interface ScenarioContext {
  workflow: IWorkflowDefinition;
  persona: Persona;
  variant: WorkflowVariant;
  dataProfile: BusinessDataProfile[];
  screens: any; // Instantiated screen objects
  rulePacks: string[];
  validationEngine: any; // Reference to Phase 4 engine
  evidencePlugin: any;
  configuration: Record<string, any>;
}

export interface ScenarioResultTimelineEvent {
  timestamp: number;
  event: string;
  durationMs?: number;
}

export interface ExtendedScenarioResult {
  scenarioId: string;
  workflow: string;
  persona: string;
  variant: string;
  status: 'PASSED' | 'FAILED' | 'SKIPPED';
  validation: IValidationResult[];
  evidence: string[]; // Paths to evidence files
  metrics: {
    durationMs: number;
    interactionRetries: number;
  };
  timeline: ScenarioResultTimelineEvent[];
  recommendations: string[];
  rootCause?: string; // Placeholder for future AI triage
}
