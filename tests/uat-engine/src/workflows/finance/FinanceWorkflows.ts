import { IWorkflowDefinition, Persona, BusinessDataProfile, WorkflowVariant, FailurePath, RiskClassification, BusinessCapability } from '../interfaces';

export const DayCloseWorkflow: IWorkflowDefinition = {
  workflowId: 'WF-FIN-001',
  name: 'Day Close',
  description: 'End of day reconciliation and terminal closing.',
  businessModule: 'Finance',
  requiredPersona: Persona.StoreManager,
  prerequisites: ['WF-SALES-001'],
  businessDataProfiles: [],
  rulePacks: ['PACK-DAY-CLOSE'],
  
  inputs: ['Terminal IDs', 'Cash Count', 'Card Count'],
  outputs: ['Z-Report', 'Ledger Lock'],
  successCriteria: ['All terminals closed', 'Ledger matches physical cash'],
  failureWorkflows: [FailurePath.Unauthorized],
  variants: [WorkflowVariant.Standard],
  
  riskClassification: RiskClassification.Critical,
  businessCapabilities: [BusinessCapability.Reconciliation],
  evidenceRequirements: ['screenshot', 'z_report_pdf'],
  
  estimatedDurationMs: 120000,
  maximumDurationMs: 600000
};

export const SupplierPaymentWorkflow: IWorkflowDefinition = {
  workflowId: 'WF-FIN-002',
  name: 'Supplier Payment',
  description: 'Settle outstanding invoices to a supplier.',
  businessModule: 'Finance',
  requiredPersona: Persona.Accountant,
  prerequisites: ['WF-PUR-002'], // Requires GRN
  businessDataProfiles: [],
  rulePacks: ['PACK-SUPPLIER-PAY'],
  
  inputs: ['Supplier ID', 'Invoices', 'Payment Amount', 'Bank Details'],
  outputs: ['Payment Voucher', 'Updated Ledger'],
  successCriteria: ['Payment voucher generated', 'Supplier balance reduced'],
  failureWorkflows: [FailurePath.PaymentDeclined],
  variants: [WorkflowVariant.Standard],
  
  riskClassification: RiskClassification.High,
  businessCapabilities: [BusinessCapability.Procurement],
  evidenceRequirements: ['screenshot', 'payment_voucher_pdf'],
  
  estimatedDurationMs: 60000,
  maximumDurationMs: 180000
};
