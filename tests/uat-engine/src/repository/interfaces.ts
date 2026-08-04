export interface IRepositoryQuery {
  startDate?: number;
  endDate?: number;
  limit?: number;
  filter?: Record<string, any>;
}

export interface IRepository<T> {
  save(record: T): Promise<void>;
  load(id: string): Promise<T | null>;
  query(query: IRepositoryQuery): Promise<T[]>;
  latest(): Promise<T | null>;
  history(limit?: number): Promise<T[]>;
  betweenDates(start: number, end: number): Promise<T[]>;
  statistics(): Promise<Record<string, any>>;
}

export interface ExecutionRecord {
  id: string; // runId
  timestamp: number;
  scenarios: any[]; // Decoupled from ExtendedScenarioResult
}

export interface MetricRecord {
  id: string;
  timestamp: number;
  durationMs: number;
  retries: number;
  capabilityId: string;
}

export interface TrendRecord {
  id: string;
  timestamp: number;
  passRate: number;
  failRate: number;
  totalRuns: number;
}

export interface FailureFingerprint {
  hash: string;
  workflow: string;
  scenario: string;
  capability: string;
  ruleFailures: string[];
  validationResults: any[];
  evidenceHash: string;
}

export interface FailureRecord {
  id: string;
  timestamp: number;
  scenarioId: string;
  error: string;
  fingerprint: FailureFingerprint;
}

export interface BaselineRecord {
  id: string; // capabilityId or scenarioId
  timestamp: number;
  type: 'Performance' | 'Validation' | 'Capability';
  averageDurationMs: number;
  expectedPassRate: number;
}

export interface ArtifactRecord {
  id: string;
  timestamp: number;
  scenarioId: string;
  type: 'Screenshot' | 'Trace' | 'Network' | 'JSON';
  path: string;
}

export interface IndexRecord {
  id: string;
  timestamp: number;
  type: string;
  references: string[];
}
