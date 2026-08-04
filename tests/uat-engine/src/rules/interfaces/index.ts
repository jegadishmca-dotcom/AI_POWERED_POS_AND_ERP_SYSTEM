export enum RuleCategory {
  Calculation = 'Calculation',
  Validation = 'Validation',
  Workflow = 'Workflow',
  Consistency = 'Consistency',
  Compliance = 'Compliance',
  Security = 'Security',
  Performance = 'Performance',
  Configuration = 'Configuration'
}

export enum RuleOwner {
  Inventory = 'Inventory',
  Finance = 'Finance',
  CRM = 'CRM',
  GST = 'GST',
  Purchasing = 'Purchasing',
  POS = 'POS',
  Security = 'Security'
}

export interface IRulePreconditions {
  requiredSnapshots: string[];
  requiredArtifacts: string[];
  requiredEvidence: string[];
}

export interface IRuleMetadata {
  ruleId: string;
  knowledgeRuleId: string; // e.g., POS-01
  category: RuleCategory;
  priority: number;
  tags: string[];
  dependencies: string[]; // RuleIDs that must run before this
  owner: RuleOwner;
  preconditions: IRulePreconditions;
  
  // Versioning
  version: string;
  createdDate: string;
  modifiedDate: string;
  deprecated: boolean;
  replacedBy?: string;
}

export interface IEvaluationContext {
  scenarioMetadata: any; 
  knowledgeReferences: Record<string, string>;
  artifacts: Record<string, any>;
  snapshots: Record<string, any>; 
  configuration: Record<string, any>;
  evidence: Record<string, string>;
  executionMetadata: {
    timestamp: number;
    runId: string;
  };
}

export interface IRuleExplanation {
  inputs: Record<string, any>;
  expected: any;
  actual: any;
  difference: string;
  reason: string;
}

export interface IValidationResult {
  ruleId: string;
  knowledgeRuleId: string;
  scenarioId: string;
  status: 'PASSED' | 'FAILED' | 'ERROR' | 'SKIPPED';
  severity: 'CRITICAL' | 'HIGH' | 'MEDIUM' | 'LOW';
  confidence: number; // Default 100
  evidence: string[];
  recommendation?: string;
  diagnostics: Record<string, any>;
  explanation: IRuleExplanation;
  reasoningContext?: Record<string, any>;
  durationMs: number;
}

export interface IRule {
  get metadata(): IRuleMetadata;
  evaluate(context: IEvaluationContext): Promise<IValidationResult>;
  explain(context: IEvaluationContext): Promise<IRuleExplanation>;
}

export interface IRulePackMetadata {
  packId: string;
  name: string;
  version: string;
  owner: RuleOwner;
  knowledgeAreas: string[];
  dependencies: string[];
  estimatedRuntimeMs: number;
}
