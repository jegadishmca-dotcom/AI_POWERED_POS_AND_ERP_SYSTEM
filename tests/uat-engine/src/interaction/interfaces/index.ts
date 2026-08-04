export enum ElementId {
  // Common
  SubmitButton = 'SubmitButton',
  CancelButton = 'CancelButton',
  ConfirmButton = 'ConfirmButton',
  CloseButton = 'CloseButton',
  SearchInput = 'SearchInput',
  
  // Login
  LoginUsername = 'LoginUsername',
  LoginPassword = 'LoginPassword',
  LoginSubmit = 'LoginSubmit',
  
  // POS
  PosBarcodeScanner = 'PosBarcodeScanner',
  PosPayButton = 'PosPayButton',
  PosCashAmount = 'PosCashAmount',
  PosTenderButton = 'PosTenderButton',
  
  // Dialogs
  DialogTitle = 'DialogTitle',
  DialogMessage = 'DialogMessage',
  
  // Tables
  TableRow = 'TableRow',
  TableCell = 'TableCell'
}

export enum InteractionEventType {
  Started = 'InteractionStarted',
  Succeeded = 'InteractionSucceeded',
  Failed = 'InteractionFailed',
  Retried = 'InteractionRetried',
  Timeout = 'InteractionTimeout',
  Recovered = 'InteractionRecovered'
}

export interface IInteractionMetrics {
  durationMs: number;
  retries: number;
  resolutionTimeMs: number;
  executionTimeMs: number;
  isTimeout: boolean;
}

export interface IInteractionEngine {
  navigate(url: string): Promise<void>;
  setValue(elementId: ElementId, value: string): Promise<void>;
  choose(elementId: ElementId, option: string): Promise<void>;
  submit(elementId: ElementId): Promise<void>;
  search(elementId: ElementId, query: string): Promise<void>;
  confirm(elementId: ElementId): Promise<void>;
  cancel(elementId: ElementId): Promise<void>;
  open(elementId: ElementId): Promise<void>;
  close(elementId: ElementId): Promise<void>;
  
  getMetrics(): IInteractionMetrics;
}

export interface IUIComponent {
  engine: IInteractionEngine;
}
