# GST Module Knowledge Base

## Module Overview
The Goods and Services Tax (GST) module handles Indian tax compliance.

## Configurable Business Policies
- **Tax Rounding Policy**: Determines if tax rounding occurs at the item level or invoice level.

## Business Rules

### GST-01: Tax Calculation
- **Module**: GST
- **Priority**: High
- **Rule Type**: Calculation
- **Configurable**: No
- **Source**: `TaxSlab.cs`
- **Applies To**: Invoice Item
- **Automation Status**: Planned
- **Planned Scenario Count**: 3
- **Description**: Tax is calculated at the line-item level based on `TaxSlab` rates.

### GST-02: Invoice Level Tax
- **Module**: GST
- **Priority**: High
- **Rule Type**: Validation
- **Configurable**: Yes (Tax Rounding Policy)
- **Source**: `layer3_workflows.py`
- **Applies To**: Invoices
- **Automation Status**: Planned
- **Planned Scenario Count**: 2
- **Description**: The Invoice-level `TaxAmount` must exactly equal the sum of `TaxAmount` for all its `InvoiceItem`s.

### GST-03: Intra-state vs Inter-state
- **Module**: GST
- **Priority**: Medium
- **Rule Type**: Logic
- **Configurable**: Yes
- **Source**: General GST Rules
- **Applies To**: Sales & Purchases
- **Automation Status**: Planned
- **Planned Scenario Count**: 2
- **Description**: Intra-state transactions apply CGST + SGST. Inter-state applies IGST.

### GST-04: Tax Transactions
- **Module**: GST
- **Priority**: High
- **Rule Type**: Process
- **Configurable**: No
- **Source**: `TaxTransaction.cs`
- **Applies To**: Compliance
- **Automation Status**: Planned
- **Planned Scenario Count**: 1
- **Description**: Every taxable event must insert a record into `TaxTransaction`.

## Expected Behaviour
- A product is assigned a `TaxSlab` (e.g., GST 18%).
- When sold, 9% CGST and 9% SGST amounts are calculated and saved on the `InvoiceItem`.
- The financial journal entry credits Output CGST and Output SGST accounts.
