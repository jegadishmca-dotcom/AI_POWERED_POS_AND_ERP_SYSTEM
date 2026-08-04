# Executive Triage Report

## Summary
This is a synthesized explanation of the failure in WF-SALES-001.

## Business Impact & Confidence
- **Impact**: High
- **Confidence**: 100% deterministic confidence.

## Context
- **Capability**: CAP-SALES-001
- **Workflow**: WF-SALES-001
- **Rules**: GstCalculationRule

## Hypotheses
- Validation rule failed in execution pipeline (Probability: 0.8)

## Supporting Evidence
- Workflow: WF-SALES-001
- Capability ID: CAP-SALES-001

## Recommended Investigation
Check the logs for GstCalculationRule.
