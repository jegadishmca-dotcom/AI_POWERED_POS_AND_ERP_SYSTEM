export class EngineException extends Error {
  constructor(message: string, public readonly originalError?: Error) {
    super(message);
    this.name = 'EngineException';
  }
}

export class PluginException extends EngineException {
  constructor(message: string, public readonly pluginName: string, originalError?: Error) {
    super(`[${pluginName}] ${message}`, originalError);
    this.name = 'PluginException';
  }
}

export class RuleException extends EngineException {
  constructor(message: string, public readonly ruleId: string) {
    super(`Rule ${ruleId} failed: ${message}`);
    this.name = 'RuleException';
  }
}

export class ScenarioException extends EngineException {
  constructor(message: string, public readonly scenarioId: string) {
    super(`Scenario ${scenarioId} failed: ${message}`);
    this.name = 'ScenarioException';
  }
}

export class ConfigurationException extends EngineException {
  constructor(message: string) {
    super(`Configuration Error: ${message}`);
    this.name = 'ConfigurationException';
  }
}
