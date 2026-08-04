# CRM Module Knowledge Base

## Module Overview
The Customer Relationship Management (CRM) module handles customer registration, profiling, loyalty tier management, wallet balances, and DPDP consent tracking.

## Configurable Business Policies
- **Customer Merge Strategy**: Determines how wallet and loyalty points are combined when merging duplicate customers.
- **Phone Number Change Verification**: Defines if OTP verification is required to change a registered phone number.
- **Tier Evaluation Frequency**: Determines if tier downgrades happen dynamically (rolling 12 months) or annually.

## Business Rules

### CRM-01: Unique Phone
- **Module**: CRM
- **Priority**: High
- **Rule Type**: Invariant
- **Configurable**: No
- **Source**: `workflow_2_customer_crm`
- **Applies To**: Registration
- **Automation Status**: Planned
- **Planned Scenario Count**: 2
- **Description**: Customer phone numbers must be unique across the system.

### CRM-02: Optional Fields
- **Module**: CRM
- **Priority**: Low
- **Rule Type**: Validation
- **Configurable**: Yes (Required fields can be customized)
- **Source**: `Customer.cs`
- **Applies To**: Registration
- **Automation Status**: Planned
- **Planned Scenario Count**: 2
- **Description**: `Email`, `Address`, `Dob`, and `Anniversary` are optional during registration.

### CRM-03: DPDP Consent
- **Module**: CRM
- **Priority**: High
- **Rule Type**: Compliance
- **Configurable**: No
- **Source**: `Customer.cs`
- **Applies To**: Registration & Updates
- **Automation Status**: Planned
- **Planned Scenario Count**: 1
- **Description**: If `MarketingConsent` or `AnalyticsConsent` is true, `ConsentRecordedAt` must be populated with the current timestamp.

### CRM-04: Wallet Balance
- **Module**: CRM
- **Priority**: Critical
- **Rule Type**: Financial
- **Configurable**: No
- **Source**: `WalletLedgerEntry.cs`
- **Applies To**: Wallet Processing
- **Automation Status**: Planned
- **Planned Scenario Count**: 3
- **Description**: `RunningWalletBalance` must exactly equal the sum of `Amount` in `wallet_ledger_entries`.

### CRM-05: Tier Evaluation
- **Module**: CRM
- **Priority**: Medium
- **Rule Type**: Process
- **Configurable**: Yes (Tier Evaluation Frequency)
- **Source**: `CustomerTier.cs`
- **Applies To**: Loyalty
- **Automation Status**: Planned
- **Planned Scenario Count**: 2
- **Description**: A customer's `CustomerTierId` is re-evaluated based on `LifetimeSpend` or `LifetimePointsEarned` against configured thresholds.

## Expected Behaviour
- Cashier enters phone number in POS; if not found, a quick registration modal appears.
- Search endpoint supports searching by phone number or name.
- Customers can accumulate wallet balance via returns or direct top-ups.
