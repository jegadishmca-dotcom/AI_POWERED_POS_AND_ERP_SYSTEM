# Purchasing Module Knowledge Base

## Module Overview
The Purchasing module manages the procurement lifecycle: Purchase Orders (PO) → Goods Receipt Notes (GRN) → Purchase Bills (AP).

## Configurable Business Policies
- **Over-receiving GRN**: Determines if a GRN can accept a quantity greater than the PO's `OrderedQuantity`.
- **Purchase Approval Workflow**: Monetary thresholds requiring manager approval.

## Business Rules

### PUR-01: GRN Flow
- **Module**: Purchasing
- **Priority**: Medium
- **Rule Type**: Process
- **Configurable**: No
- **Source**: `GRNHeader.cs`
- **Applies To**: Receiving
- **Automation Status**: Planned
- **Planned Scenario Count**: 2
- **Description**: A `GRNHeader` must be created against a `SupplierId`, optionally linking to a PO.

### PUR-02: Stock Update
- **Module**: Purchasing
- **Priority**: Critical
- **Rule Type**: Process
- **Configurable**: No
- **Source**: Inventory integration
- **Applies To**: GRN Confirmation
- **Automation Status**: Planned
- **Planned Scenario Count**: 3
- **Description**: Confirming a GRN immediately increases stock via a `StockLedgerEntry`.

### PUR-03: Financial Recognition
- **Module**: Purchasing
- **Priority**: Critical
- **Rule Type**: Financial
- **Configurable**: No
- **Source**: Finance integration
- **Applies To**: GRN / Purchase Bill
- **Automation Status**: Planned
- **Planned Scenario Count**: 2
- **Description**: Confirming a GRN debits Inventory and credits Accounts Payable.

### PUR-04: Quantity Validation
- **Module**: Purchasing
- **Priority**: High
- **Rule Type**: Validation
- **Configurable**: Yes (Over-receiving GRN policy)
- **Source**: `GRNItem.cs`
- **Applies To**: Receiving
- **Automation Status**: Planned
- **Planned Scenario Count**: 2
- **Description**: `ReceivedQuantity = AcceptedQuantity + RejectedQuantity`. Only Accepted increases stock.

### PUR-05: Batch Creation
- **Module**: Purchasing
- **Priority**: Medium
- **Rule Type**: Process
- **Configurable**: No
- **Source**: `ProductBatch.cs`
- **Applies To**: Batch-tracked items
- **Automation Status**: Planned
- **Planned Scenario Count**: 1
- **Description**: If batch-tracked, GRN confirmation generates a new `ProductBatch`.

## Expected Behaviour
- Goods arrive; receiver creates a GRN.
- On GRN confirmation, stock increases, and a Purchase Bill is generated in AP.
