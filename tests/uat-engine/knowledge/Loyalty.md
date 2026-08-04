# Loyalty Module Knowledge Base

## Module Overview
The Loyalty module manages customer reward points. It handles point accrual (Earn), redemption, expiration, and manual adjustments.

## Configurable Business Policies
- **Loyalty Return Reversal Policy**: Defines if loyalty points can go negative when a return occurs for points already spent.
- **Accrual Rules**: Define `EarnRatioSpendAmount` and `EarnRatioPoints`.

## Business Rules

### LOY-01: Loyalty Ledger Truth
- **Module**: Loyalty
- **Priority**: Critical
- **Rule Type**: Invariant
- **Configurable**: No
- **Source**: `LoyaltyLedgerEntry.cs`
- **Applies To**: Point Calculation
- **Automation Status**: Planned
- **Planned Scenario Count**: 3
- **Description**: `RunningLoyaltyPoints` in `Customer` must equal latest `RunningPoints` in `loyalty_ledger`.

### LOY-02: Earn Math
- **Module**: Loyalty
- **Priority**: High
- **Rule Type**: Calculation
- **Configurable**: Yes (Ratios are configurable)
- **Source**: `workflow_4_loyalty_points`
- **Applies To**: Earn
- **Automation Status**: Planned
- **Planned Scenario Count**: 2
- **Description**: Points earned = `Floor(NetPayable / EarnRatioSpendAmount) * EarnRatioPoints`.

### LOY-03: Redeem Limits
- **Module**: Loyalty
- **Priority**: Medium
- **Rule Type**: Validation
- **Configurable**: Yes
- **Source**: `LoyaltyProgramConfig.cs`
- **Applies To**: Checkout
- **Automation Status**: Planned
- **Planned Scenario Count**: 2
- **Description**: Points redeemed cannot exceed `MaxRedemptionPercentagePerInvoice` % of the invoice `NetPayable`.

### LOY-04: Reversal
- **Module**: Loyalty
- **Priority**: High
- **Rule Type**: Process
- **Configurable**: Yes (Loyalty Return Reversal Policy)
- **Source**: Return processing
- **Applies To**: Sales Returns
- **Automation Status**: Planned
- **Planned Scenario Count**: 2
- **Description**: When a return is processed, points earned must be reversed.

## Expected Behaviour
- Customer is attached to a POS sale.
- If sale completes, an entry is created. Customer balance updates.
