import { ICapability } from '../interfaces';

export const CashSaleCapability: ICapability = {
  capabilityId: 'CAP-SALES-001',
  name: 'Cash Sale',
  description: 'Standard retail checkout flow accepting cash.',
  owner: 'Retail Operations',
  risk: 'Critical',
  priority: 'P0',
  exitCriteria: ['Invoice generated', 'Inventory deducted', 'Cash ledger updated'],
  completionStatus: 'Completed',
  workflows: ['WF-SALES-001'],
  scenarios: ['SCENARIO-SALES-001-HAPPY', 'SCENARIO-SALES-001-FAIL'],
  rules: ['PACK-CASH-SALE']
};

export const SalesReturnCapability: ICapability = {
  capabilityId: 'CAP-SALES-002',
  name: 'Sales Return',
  description: 'Processing a return and refund for previously purchased items.',
  owner: 'Retail Operations',
  risk: 'High',
  priority: 'P1',
  exitCriteria: ['Credit note generated', 'Inventory restored', 'Refund logged'],
  completionStatus: 'Completed',
  workflows: ['WF-SALES-002'],
  scenarios: ['SCENARIO-SALES-002-HAPPY', 'SCENARIO-SALES-002-FAIL'],
  rules: ['PACK-SALES-RETURN']
};

export const PurchaseAndGRNCapability: ICapability = {
  capabilityId: 'CAP-PUR-001',
  name: 'Purchase + GRN',
  description: 'End-to-end procurement process.',
  owner: 'Purchasing',
  risk: 'High',
  priority: 'P1',
  exitCriteria: ['PO Approved', 'GRN Posted', 'Inventory Increased', 'Supplier Ledger Updated'],
  completionStatus: 'Completed',
  workflows: ['WF-PUR-001', 'WF-PUR-002'],
  scenarios: ['SCENARIO-PUR-001-HAPPY', 'SCENARIO-PUR-001-FAIL'],
  rules: ['PACK-PURCHASE', 'PACK-GRN']
};

export const StockAdjustmentCapability: ICapability = {
  capabilityId: 'CAP-INV-001',
  name: 'Stock Adjustment',
  description: 'Manual adjustment of stock levels due to damage or audit.',
  owner: 'Inventory',
  risk: 'Medium',
  priority: 'P2',
  exitCriteria: ['Adjustment approved', 'Ledger balanced', 'Stock updated'],
  completionStatus: 'Completed',
  workflows: ['WF-INV-001'],
  scenarios: ['SCENARIO-INV-001-HAPPY', 'SCENARIO-INV-001-FAIL'],
  rules: ['PACK-STOCK-TRANSFER']
};

export const LoyaltyRedemptionCapability: ICapability = {
  capabilityId: 'CAP-CRM-001',
  name: 'Loyalty Redemption',
  description: 'Redeeming loyalty points during a sale.',
  owner: 'CRM',
  risk: 'Medium',
  priority: 'P2',
  exitCriteria: ['Points deducted', 'Discount applied', 'Customer notified'],
  completionStatus: 'Completed',
  workflows: ['WF-CRM-002'],
  scenarios: ['SCENARIO-CRM-002-HAPPY', 'SCENARIO-CRM-002-FAIL'],
  rules: ['PACK-LOYALTY-REDEMPTION']
};

export const GstReconciliationCapability: ICapability = {
  capabilityId: 'CAP-FIN-003',
  name: 'GST Reconciliation',
  description: 'Reconciling inward and outward GST.',
  owner: 'Finance',
  risk: 'Critical',
  priority: 'P0',
  exitCriteria: ['Tax report matches sales', 'Exceptions flagged'],
  completionStatus: 'Completed',
  workflows: ['WF-FIN-003'],
  scenarios: ['SCENARIO-FIN-003-HAPPY', 'SCENARIO-FIN-003-FAIL'],
  rules: ['PACK-GST-RECONCILIATION']
};

export const DayCloseCapability: ICapability = {
  capabilityId: 'CAP-FIN-001',
  name: 'Day Close',
  description: 'End of day till reconciliation.',
  owner: 'Finance',
  risk: 'Critical',
  priority: 'P0',
  exitCriteria: ['Terminals locked', 'Cash matches Z-report', 'Discrepancies logged'],
  completionStatus: 'Completed',
  workflows: ['WF-FIN-001'],
  scenarios: ['SCENARIO-FIN-001-HAPPY', 'SCENARIO-FIN-001-FAIL'],
  rules: ['PACK-DAY-CLOSE']
};

export const ReportsGenerationCapability: ICapability = {
  capabilityId: 'CAP-REP-001',
  name: 'Reports Generation',
  description: 'Generation of standard analytical reports.',
  owner: 'Management',
  risk: 'Low',
  priority: 'P3',
  exitCriteria: ['Report data complete', 'Formatting correct'],
  completionStatus: 'Completed',
  workflows: ['WF-REP-001'],
  scenarios: ['SCENARIO-REP-001-HAPPY', 'SCENARIO-REP-001-FAIL'],
  rules: ['PACK-REPORTS']
};
