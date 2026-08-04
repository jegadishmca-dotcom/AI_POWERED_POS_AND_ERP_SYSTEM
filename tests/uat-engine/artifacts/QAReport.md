# QA Triage Report

## Summary
QA Report: Scenario SCENARIO-SALES-001-FAIL failed while executing as persona Cashier.

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
Reproduce the scenario locally. Check evidence at /evidence/fail-123.png.
