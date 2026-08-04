# Detailed AI Triage Report

## Context
- Workflow: WF-FIN-001
- Capability: CAP-FIN-001

## Root Cause Hypotheses
### Validation rule failed in execution pipeline
- **Score**: 0.504 (Prob: 0.8, Evid: 0.9, Hist: 0.7)
- **Reason**: Deterministic extraction from engine payload
- **Affected Rules**: GstCalculationRule

## Findings & Explainability
### Ownership
- **Decision**: Finance
- **Reason**: Routed via deterministic capability-to-team matrix
- **Confidence**: 100%

### Business Impact
- **Decision**: High Impact
- **Reason**: Core business workflow interrupted
- **Confidence**: 90%

### Release Risk
- **Decision**: Blocker
- **Reason**: Computed from Business Criticality + Capability Risk + Regression state
- **Confidence**: 100%

