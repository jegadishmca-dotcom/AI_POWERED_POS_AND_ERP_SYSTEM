# Finance Module Knowledge Base

## Module Overview
The Finance module forms the core of the ERP, enforcing double-entry accounting. Every financial event generates a Journal Entry.

## Configurable Business Policies
- **Invoice Voiding Policy**: Determines if a same-day void deletes the original journal entry or posts a reversal entry.
- **Rounding Account Mapping**: Defines which specific account absorbs fractional tax differences.

## Business Rules

### FIN-01: Double-Entry Balance
- **Module**: Finance
- **Priority**: Critical
- **Rule Type**: Invariant
- **Configurable**: No
- **Source**: `layer4_accounting.py`
- **Applies To**: Journal Entries
- **Automation Status**: Planned
- **Planned Scenario Count**: 5
- **Description**: Every `JournalEntry` must be balanced. `Sum(DebitAmount)` must equal `Sum(CreditAmount)`.

### FIN-02: Required Accounts
- **Module**: Finance
- **Priority**: Critical
- **Rule Type**: Validation
- **Configurable**: Yes (Account Codes can be remapped)
- **Source**: `layer4_accounting.py`
- **Applies To**: Chart of Accounts
- **Automation Status**: Planned
- **Planned Scenario Count**: 1
- **Description**: The system relies on a mapped set of core accounts (e.g., 1000 Cash, 4000 Sales).

### FIN-03: Immutability
- **Module**: Finance
- **Priority**: Critical
- **Rule Type**: Core Principle
- **Configurable**: No
- **Source**: `JournalEntry.cs`
- **Applies To**: `POSTED` entries
- **Automation Status**: Planned
- **Planned Scenario Count**: 1
- **Description**: Once a `JournalEntry` is `POSTED`, it cannot be modified or deleted. Errors require a Reversal Journal Entry.

### FIN-04: Trial Balance
- **Module**: Finance
- **Priority**: High
- **Rule Type**: Financial
- **Configurable**: No
- **Source**: Accounting Standard
- **Applies To**: Reporting
- **Automation Status**: Planned
- **Planned Scenario Count**: 2
- **Description**: The sum of all Debit balances across all accounts must equal the sum of all Credit balances.

## Expected Behaviour
- **POS Sale**: Debits Cash/Bank/AR, Credits Sales Revenue & Output Tax. Debits COGS, Credits Inventory.
- **Purchase (GRN)**: Debits Inventory, Credits AP.
- **Supplier Payment**: Debits AP, Credits Cash/Bank.
