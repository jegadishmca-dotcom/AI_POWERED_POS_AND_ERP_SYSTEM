export interface IBaseline {
  scenarioId: string;
  module: string;
  expectedDurationMs: number;
  expectedPassRate: number;
  version: string;
}
