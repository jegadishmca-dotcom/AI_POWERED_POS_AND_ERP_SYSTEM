export enum ScenarioPrecondition {
  LoggedIn = 'LoggedIn',
  StoreOpen = 'StoreOpen',
  ProductExists = 'ProductExists',
  CustomerExists = 'CustomerExists',
  StockAvailable = 'StockAvailable',
  ShiftOpen = 'ShiftOpen'
}

export enum ScenarioCleanup {
  Rollback = 'rollback',
  KeepTestData = 'keep-test-data',
  DeleteTestData = 'delete-test-data',
  ArchiveTestData = 'archive-test-data'
}

export enum ScenarioResource {
  Browser = 'browser',
  Database = 'database',
  Api = 'api',
  Filesystem = 'filesystem',
  Email = 'email'
}

export enum ScenarioCapability {
  Inventory = 'inventory',
  Finance = 'finance',
  Crm = 'crm',
  Gst = 'gst',
  Loyalty = 'loyalty',
  Offers = 'offers',
  Reports = 'reports',
  Security = 'security',
  Performance = 'performance'
}

export enum ExecutionStrategy {
  Sequential = 'Sequential',
  Parallel = 'Parallel',
  Isolated = 'Isolated',
  Exclusive = 'Exclusive'
}

export interface IScenarioMetadata {
  id: string;
  name: string;
  category: 'Critical' | 'High' | 'Medium' | 'Low';
  priority: number;
  tags: string[];
  dependencies: string[];
  timeoutMs: number;
  retryCount: number;
  estimatedDurationMs: number;
  businessRules: string[];
  evidenceRequirements: ('screenshot' | 'trace' | 'har' | 'log')[];
  
  preconditions: ScenarioPrecondition[];
  cleanup: ScenarioCleanup;
  resources: ScenarioResource[];
  capabilities: ScenarioCapability[];
  strategy: ExecutionStrategy;
}
