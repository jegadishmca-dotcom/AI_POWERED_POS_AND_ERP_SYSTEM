export enum Persona {
  Cashier = 'Cashier',
  Supervisor = 'Supervisor',
  StoreManager = 'Store Manager',
  InventoryClerk = 'Inventory Clerk',
  Accountant = 'Accountant',
  PurchasingOfficer = 'Purchasing Officer',
  Administrator = 'Administrator',
  Auditor = 'Auditor'
}

export enum BusinessDataProfile {
  GstProduct = 'GST Product',
  NonGstProduct = 'Non GST Product',
  HighValueProduct = 'High Value Product',
  LowStockProduct = 'Low Stock Product',
  ExpiredProduct = 'Expired Product',
  PromotionalProduct = 'Promotional Product',
  LoyaltyCustomer = 'Loyalty Customer',
  CashCustomer = 'Cash Customer',
  CreditCustomer = 'Credit Customer'
}

export enum WorkflowVariant {
  Cash = 'Cash',
  UPI = 'UPI',
  Card = 'Card',
  Mixed = 'Mixed',
  Credit = 'Credit',
  Standard = 'Standard'
}

export enum FailurePath {
  PaymentDeclined = 'Payment Declined',
  PrinterFailure = 'Printer Failure',
  NegativeStock = 'Negative Stock',
  CreditLimitExceeded = 'Credit Limit Exceeded',
  GstValidationFailure = 'GST Validation Failure',
  Unauthorized = 'Unauthorized'
}

export enum RiskClassification {
  Critical = 'Critical',
  High = 'High',
  Medium = 'Medium',
  Low = 'Low'
}

export enum BusinessCapability {
  Checkout = 'Checkout',
  Refunds = 'Refunds',
  Procurement = 'Procurement',
  StockManagement = 'Stock Management',
  Reconciliation = 'Reconciliation',
  CustomerManagement = 'Customer Management'
}

export interface IWorkflowDefinition {
  workflowId: string;
  name: string;
  description: string;
  businessModule: string; // e.g. Sales, Purchasing
  requiredPersona: Persona;
  prerequisites: string[]; // Workflow IDs that must run before this
  businessDataProfiles: BusinessDataProfile[];
  rulePacks: string[]; // Links to Phase 5 Rule Packs (e.g. PACK-CASH-SALE)
  
  inputs: string[];
  outputs: string[];
  successCriteria: string[];
  failureWorkflows: FailurePath[];
  variants: WorkflowVariant[];
  
  riskClassification: RiskClassification;
  businessCapabilities: BusinessCapability[];
  evidenceRequirements: string[];
  
  estimatedDurationMs: number;
  maximumDurationMs: number;
}
