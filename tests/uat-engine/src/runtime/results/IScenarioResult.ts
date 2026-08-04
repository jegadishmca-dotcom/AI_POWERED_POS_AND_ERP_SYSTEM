export interface IStabilityMetrics {
  totalRuns: number;
  passes: number;
  failures: number;
  flakyPercentage: number;
  averageDurationMs: number;
  failureTrend: ('PASS' | 'FAIL' | 'FLAKY')[];
}

export interface IScenarioResult {
  scenarioId: string;
  status: 'PASSED' | 'FAILED' | 'SKIPPED' | 'TIMEOUT';
  durationMs: number;
  ruleResults: Record<string, 'PASSED' | 'FAILED'>;
  warnings: string[];
  failures: Error[];
  artifacts: string[]; // Paths to physical files (e.g. downloads)
  evidence: string[]; // Paths to traces, screenshots
  performanceMetrics: Record<string, number>;
  recommendations?: string[]; // AI recommendations
  analysisContext?: Record<string, any>; // Support future AI Root Cause Analysis
}
