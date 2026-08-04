# Reports Module Knowledge Base

## Module Overview
The Reports module aggregates data from POS, Inventory, Finance, and CRM.

## Configurable Business Policies
- **Inventory Valuation Method**: Determines how historic stock value is computed (Standard vs MAC).

## Business Rules

### REP-01: Z-Report Integrity
- **Module**: Reports
- **Priority**: Critical
- **Rule Type**: Audit
- **Configurable**: No
- **Source**: `layer2_api_smoke.py`
- **Applies To**: End of Day
- **Automation Status**: Planned
- **Planned Scenario Count**: 2
- **Description**: The Z-Report must accurately reflect the `ActualClosingCash` vs `ExpectedClosingCash`.

### REP-02: Data Freshness
- **Module**: Reports
- **Priority**: High
- **Rule Type**: Invariant
- **Configurable**: No
- **Source**: Reporting Standards
- **Applies To**: Financial Reports
- **Automation Status**: Planned
- **Planned Scenario Count**: 1
- **Description**: Reports must reflect all `POSTED` journal entries up to the requested date.

### REP-03: Inventory Valuation
- **Module**: Reports
- **Priority**: Medium
- **Rule Type**: Calculation
- **Configurable**: Yes
- **Source**: Finance
- **Applies To**: Stock Reports
- **Automation Status**: Planned
- **Planned Scenario Count**: 2
- **Description**: Inventory value is calculated based on configured costing method.

## Expected Behaviour
- Cashier pulls an X-Report mid-shift to check totals.
- Manager pulls a Z-Report at shift close.
