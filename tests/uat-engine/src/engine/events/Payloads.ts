import { EventName } from '../types';

export interface IEventPayload {
  readonly timestamp: number;
}

export interface ScenarioStarted extends IEventPayload {
  scenarioId: string;
}

export interface ScenarioCompleted extends IEventPayload {
  scenarioId: string;
  durationMs: number;
}

export interface ScenarioFailed extends IEventPayload {
  scenarioId: string;
  error: Error;
}

export interface RuleStarted extends IEventPayload {
  ruleId: string;
}

export interface RulePassed extends IEventPayload {
  ruleId: string;
}

export interface RuleFailed extends IEventPayload {
  ruleId: string;
  error: Error;
}

export interface EvidenceCaptured extends IEventPayload {
  scenarioId: string;
  evidenceType: 'screenshot' | 'trace' | 'log';
  filePath: string;
}

export interface ApiCalled extends IEventPayload {
  url: string;
  method: string;
  statusCode: number;
  durationMs: number;
}

export interface DatabaseValidated extends IEventPayload {
  query: string;
  rowsMatched: number;
}

export interface PluginLoaded extends IEventPayload {
  pluginName: string;
  version: string;
}

export interface PluginFailed extends IEventPayload {
  pluginName: string;
  error: Error;
}

export interface AnalysisCompleted extends IEventPayload {
  scenarioId: string;
  aiConfidence: number;
  rootCause: string;
}

export interface ReportGenerated extends IEventPayload {
  reportPath: string;
}
