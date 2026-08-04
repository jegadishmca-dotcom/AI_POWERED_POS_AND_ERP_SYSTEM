import { IWorkflowDefinition, Persona, BusinessDataProfile, WorkflowVariant, FailurePath, RiskClassification, BusinessCapability } from '../interfaces';

export const StockTransferWorkflow: IWorkflowDefinition = {
  workflowId: 'WF-INV-001',
  name: 'Stock Transfer',
  description: 'Transfer inventory between branches or warehouses.',
  businessModule: 'Inventory',
  requiredPersona: Persona.StoreManager,
  prerequisites: [],
  businessDataProfiles: [BusinessDataProfile.GstProduct],
  rulePacks: ['PACK-STOCK-TRANSFER'],
  
  inputs: ['Source Location', 'Dest Location', 'Items'],
  outputs: ['Transfer Document', 'Inventory Deducted', 'Inventory Added (Transit)'],
  successCriteria: ['Transfer approved', 'Stock correctly allocated'],
  failureWorkflows: [FailurePath.NegativeStock],
  variants: [WorkflowVariant.Standard],
  
  riskClassification: RiskClassification.High,
  businessCapabilities: [BusinessCapability.StockManagement],
  evidenceRequirements: ['screenshot', 'transfer_pdf'],
  
  estimatedDurationMs: 45000,
  maximumDurationMs: 120000
};

export const InventoryCountWorkflow: IWorkflowDefinition = {
  workflowId: 'WF-INV-002',
  name: 'Inventory Count',
  description: 'Physical stock reconciliation process.',
  businessModule: 'Inventory',
  requiredPersona: Persona.Auditor,
  prerequisites: [],
  businessDataProfiles: [BusinessDataProfile.HighValueProduct, BusinessDataProfile.ExpiredProduct],
  rulePacks: ['PACK-INV-COUNT'],
  
  inputs: ['Location', 'Scanned Barcodes', 'Physical Quantities'],
  outputs: ['Variance Report', 'Adjustment Ledger'],
  successCriteria: ['Count posted', 'Variances approved'],
  failureWorkflows: [FailurePath.Unauthorized],
  variants: [WorkflowVariant.Standard],
  
  riskClassification: RiskClassification.Critical,
  businessCapabilities: [BusinessCapability.Reconciliation, BusinessCapability.StockManagement],
  evidenceRequirements: ['screenshot', 'variance_report_pdf'],
  
  estimatedDurationMs: 180000,
  maximumDurationMs: 600000
};
