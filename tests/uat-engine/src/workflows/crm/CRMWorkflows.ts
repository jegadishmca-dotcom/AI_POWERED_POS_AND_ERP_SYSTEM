import { IWorkflowDefinition, Persona, BusinessDataProfile, WorkflowVariant, FailurePath, RiskClassification, BusinessCapability } from '../interfaces';

export const CustomerRegistrationWorkflow: IWorkflowDefinition = {
  workflowId: 'WF-CRM-001',
  name: 'Customer Registration',
  description: 'Register a new customer for loyalty and billing.',
  businessModule: 'CRM',
  requiredPersona: Persona.Cashier,
  prerequisites: [],
  businessDataProfiles: [BusinessDataProfile.LoyaltyCustomer],
  rulePacks: ['PACK-CUSTOMER-REG'],
  
  inputs: ['Name', 'Phone', 'Email'],
  outputs: ['Customer Record'],
  successCriteria: ['Customer saved', 'Loyalty account created'],
  failureWorkflows: [FailurePath.Unauthorized],
  variants: [WorkflowVariant.Standard],
  
  riskClassification: RiskClassification.Low,
  businessCapabilities: [BusinessCapability.CustomerManagement],
  evidenceRequirements: ['screenshot'],
  
  estimatedDurationMs: 30000,
  maximumDurationMs: 60000
};
