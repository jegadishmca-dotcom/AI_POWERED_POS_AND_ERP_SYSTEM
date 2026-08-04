export type CompletionStatus = 'Not Started' | 'In Progress' | 'Completed' | 'Blocked';
export type RiskLevel = 'Low' | 'Medium' | 'High' | 'Critical';
export type PriorityLevel = 'P0' | 'P1' | 'P2' | 'P3';

export interface ICapability {
  capabilityId: string;
  name: string;
  description: string;
  owner: string; // e.g. 'Sales Team', 'Finance Team'
  risk: RiskLevel;
  priority: PriorityLevel;
  exitCriteria: string[];
  completionStatus: CompletionStatus;
  
  workflows: string[]; // Linked workflow IDs
  scenarios: string[]; // Linked scenario IDs
  rules: string[]; // Linked rule pack IDs
}
