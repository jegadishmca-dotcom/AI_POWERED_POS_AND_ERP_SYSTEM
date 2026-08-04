import { IWorkflowDefinition, Persona, BusinessDataProfile, WorkflowVariant, FailurePath, RiskClassification, BusinessCapability } from '../interfaces';

export const PurchaseWorkflow: IWorkflowDefinition = {
  workflowId: 'WF-PUR-001',
  name: 'Purchase Order',
  description: 'Creation of a purchase order to a supplier.',
  businessModule: 'Purchasing',
  requiredPersona: Persona.PurchasingOfficer,
  prerequisites: [],
  businessDataProfiles: [BusinessDataProfile.GstProduct],
  rulePacks: ['PACK-PURCHASE'],
  
  inputs: ['Supplier Details', 'Items', 'Quantities', 'Expected Rates'],
  outputs: ['Purchase Order Document'],
  successCriteria: ['PO generated and approved'],
  failureWorkflows: [FailurePath.Unauthorized],
  variants: [WorkflowVariant.Standard],
  
  riskClassification: RiskClassification.Medium,
  businessCapabilities: [BusinessCapability.Procurement],
  evidenceRequirements: ['screenshot', 'po_pdf'],
  
  estimatedDurationMs: 60000,
  maximumDurationMs: 300000
};

export const ReceiveGRNWorkflow: IWorkflowDefinition = {
  workflowId: 'WF-PUR-002',
  name: 'Receive GRN',
  description: 'Goods Receipt Note for delivered items.',
  businessModule: 'Purchasing',
  requiredPersona: Persona.InventoryClerk,
  prerequisites: ['WF-PUR-001'],
  businessDataProfiles: [BusinessDataProfile.GstProduct],
  rulePacks: ['PACK-GRN'],
  
  inputs: ['PO Number', 'Received Quantities', 'Batch/Expiry'],
  outputs: ['GRN Document', 'Updated Inventory'],
  successCriteria: ['GRN posted', 'Inventory increased'],
  failureWorkflows: [],
  variants: [WorkflowVariant.Standard],
  
  riskClassification: RiskClassification.High,
  businessCapabilities: [BusinessCapability.Procurement, BusinessCapability.StockManagement],
  evidenceRequirements: ['screenshot', 'trace'],
  
  estimatedDurationMs: 90000,
  maximumDurationMs: 300000
};
