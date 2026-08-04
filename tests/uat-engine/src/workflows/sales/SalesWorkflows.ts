import { IWorkflowDefinition, Persona, BusinessDataProfile, WorkflowVariant, FailurePath, RiskClassification, BusinessCapability } from '../interfaces';

export const CashSaleWorkflow: IWorkflowDefinition = {
  workflowId: 'WF-SALES-001',
  name: 'Cash Sale',
  description: 'Standard checkout process for a customer purchasing items with immediate payment.',
  businessModule: 'Sales',
  requiredPersona: Persona.Cashier,
  prerequisites: ['WF-ADMIN-001'], // Requires Shift Open
  businessDataProfiles: [BusinessDataProfile.GstProduct, BusinessDataProfile.CashCustomer],
  rulePacks: ['PACK-CASH-SALE'],
  
  inputs: ['Product Barcodes', 'Payment Amount'],
  outputs: ['Sales Invoice', 'Updated Inventory', 'Ledger Entry'],
  successCriteria: ['Invoice printed', 'Inventory reduced', 'Ledger balanced'],
  failureWorkflows: [FailurePath.PaymentDeclined, FailurePath.NegativeStock, FailurePath.PrinterFailure],
  variants: [WorkflowVariant.Cash, WorkflowVariant.UPI, WorkflowVariant.Card, WorkflowVariant.Mixed],
  
  riskClassification: RiskClassification.Critical,
  businessCapabilities: [BusinessCapability.Checkout],
  evidenceRequirements: ['screenshot', 'trace', 'invoice_pdf'],
  
  estimatedDurationMs: 45000,
  maximumDurationMs: 120000
};

export const SalesReturnWorkflow: IWorkflowDefinition = {
  workflowId: 'WF-SALES-002',
  name: 'Sales Return',
  description: 'Processing a return and refund for previously purchased items.',
  businessModule: 'Sales',
  requiredPersona: Persona.Supervisor,
  prerequisites: ['WF-SALES-001'], // Requires a previous sale
  businessDataProfiles: [BusinessDataProfile.HighValueProduct],
  rulePacks: ['PACK-SALES-RETURN'],
  
  inputs: ['Original Invoice Number', 'Return Reason', 'Items'],
  outputs: ['Credit Note', 'Updated Inventory', 'Refund Payment'],
  successCriteria: ['Credit Note generated', 'Inventory increased', 'Customer refunded'],
  failureWorkflows: [FailurePath.Unauthorized],
  variants: [WorkflowVariant.Cash, WorkflowVariant.Card],
  
  riskClassification: RiskClassification.High,
  businessCapabilities: [BusinessCapability.Refunds],
  evidenceRequirements: ['screenshot', 'trace', 'credit_note_pdf'],
  
  estimatedDurationMs: 60000,
  maximumDurationMs: 180000
};
