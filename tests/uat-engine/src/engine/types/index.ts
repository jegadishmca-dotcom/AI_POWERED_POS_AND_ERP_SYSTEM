export enum LogLevel {
  TRACE = 0,
  DEBUG = 1,
  INFO = 2,
  WARN = 3,
  ERROR = 4
}

export enum PluginState {
  UNINITIALIZED = 'UNINITIALIZED',
  INITIALIZING = 'INITIALIZING',
  READY = 'READY',
  BLOCKED = 'BLOCKED',
  ERROR = 'ERROR'
}

export enum Scope {
  TRANSIENT = 'TRANSIENT',
  SCOPED = 'SCOPED',
  SINGLETON = 'SINGLETON'
}

export enum EventName {
  ScenarioStarted = 'ScenarioStarted',
  ScenarioCompleted = 'ScenarioCompleted',
  ScenarioFailed = 'ScenarioFailed',
  RuleStarted = 'RuleStarted',
  RulePassed = 'RulePassed',
  RuleFailed = 'RuleFailed',
  EvidenceCaptured = 'EvidenceCaptured',
  ApiCalled = 'ApiCalled',
  DatabaseValidated = 'DatabaseValidated',
  PluginLoaded = 'PluginLoaded',
  PluginFailed = 'PluginFailed',
  AnalysisCompleted = 'AnalysisCompleted',
  ReportGenerated = 'ReportGenerated'
}
