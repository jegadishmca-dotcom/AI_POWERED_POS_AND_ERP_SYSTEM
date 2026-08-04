import { AITriageInput, Finding } from '../contracts/interfaces';

export class RegressionAnalyzer {
  public analyze(input: AITriageInput): Finding<boolean> {
    const historicalFails = input.historicalRuns.filter(r => r.failureFingerprint === input.failureFingerprint);
    const isRegression = historicalFails.length === 0; // If it's never failed exactly like this, it's a regression.

    return {
      type: 'REGRESSION',
      value: isRegression,
      explainability: {
        decision: isRegression ? 'New Regression Detected' : 'Known Historical Failure',
        evidence: [`Historical runs matching fingerprint: ${historicalFails.length}`],
        reason: 'Deterministic fingerprint comparison against historical baseline',
        confidence: { deterministic: 100, ai: 0 }
      }
    };
  }
}

export class PriorityAnalyzer {
  public analyze(input: AITriageInput): Finding<string> {
    let priority = 'P2';
    if (input.capabilityId.includes('SALES') || input.capabilityId.includes('FIN')) priority = 'P0';
    if (input.capabilityId.includes('PUR')) priority = 'P1';

    return {
      type: 'PRIORITY',
      value: priority,
      explainability: {
        decision: priority,
        evidence: [`Capability ID: ${input.capabilityId}`],
        reason: 'Priority derived deterministically from business capability risk mapping',
        confidence: { deterministic: 100, ai: 0 }
      }
    };
  }
}

export class OwnershipAnalyzer {
  public analyze(input: AITriageInput): Finding<{ team: string; reason: string }> {
    let team = 'Operations';
    if (input.capabilityId.includes('FIN')) team = 'Finance';
    if (input.capabilityId.includes('INV')) team = 'Inventory';

    return {
      type: 'OWNERSHIP',
      value: { team, reason: `Capability ${input.capabilityId} falls under ${team} domain.` },
      explainability: {
        decision: team,
        evidence: [`Workflow: ${input.workflowId}`],
        reason: 'Routed via deterministic capability-to-team matrix',
        confidence: { deterministic: 100, ai: 0 }
      }
    };
  }
}

export class BusinessImpactAnalyzer {
  public analyze(input: AITriageInput): Finding<string> {
    return {
      type: 'BUSINESS_IMPACT',
      value: 'High',
      explainability: {
        decision: 'High Impact',
        evidence: [`Failed during: ${input.workflowId}`],
        reason: 'Core business workflow interrupted',
        confidence: { deterministic: 90, ai: 0 }
      }
    };
  }
}

export class ReleaseRiskAnalyzer {
  public analyze(input: AITriageInput, businessImpact: string, isRegression: boolean): Finding<string> {
    let risk = 'Low';
    if (businessImpact === 'High' && isRegression) risk = 'Blocker';

    return {
      type: 'RELEASE_RISK',
      value: risk,
      explainability: {
        decision: risk,
        evidence: [`Impact: ${businessImpact}`, `Regression: ${isRegression}`],
        reason: 'Computed from Business Criticality + Capability Risk + Regression state',
        confidence: { deterministic: 100, ai: 0 }
      }
    };
  }
}
