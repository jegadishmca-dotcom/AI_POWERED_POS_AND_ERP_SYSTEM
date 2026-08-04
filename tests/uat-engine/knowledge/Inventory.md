# Inventory Module Knowledge Base

## Module Overview
The Inventory module tracks the lifecycle and quantities of all products via a perpetual inventory system driven by the `StockLedgerEntry`.

## Configurable Business Policies
- **Negative Stock Policy**: Strict (block sales if stock < 0) vs Lenient (allow `SALE_OVERRIDE` to avoid blocking checkout).
- **Costing Method**: Moving Average Cost (MAC), FIFO, or Standard Costing for inventory valuation.
- **Adjustment Approval Threshold**: Defines the monetary value requiring Manager or Admin approval for stock adjustments.

## Business Rules

### INV-01: Stock Equation
- **Module**: Inventory
- **Priority**: Critical
- **Rule Type**: Invariant
- **Configurable**: No
- **Source**: `layer3_workflows.py`
- **Applies To**: Stock Calculation
- **Automation Status**: Planned
- **Planned Scenario Count**: 4
- **Description**: `Current Stock = Sum(Quantity)` from `stock_ledger_entries`.

### INV-02: Immutable Ledger
- **Module**: Inventory
- **Priority**: Critical
- **Rule Type**: Core Principle
- **Configurable**: No
- **Source**: `StockLedgerEntry.cs`
- **Applies To**: Data Integrity
- **Automation Status**: Planned
- **Planned Scenario Count**: 1
- **Description**: `StockLedgerEntry` records cannot be updated or deleted once created. Corrections require a compensatory entry.

### INV-03: Fractional Quantities
- **Module**: Inventory
- **Priority**: High
- **Rule Type**: Validation
- **Configurable**: No
- **Source**: `CreateProductCommandHandler.cs`
- **Applies To**: Item Management
- **Automation Status**: Planned
- **Planned Scenario Count**: 2
- **Description**: Products where `IsWeighable = true` can have fractional quantities. Non-weighable products must have integer quantities.

### INV-04: Negative Stock Enforcement
- **Module**: Inventory
- **Priority**: Medium
- **Rule Type**: Policy
- **Configurable**: Yes (Negative Stock Policy)
- **Source**: `layer3_workflows.py`
- **Applies To**: Checkout Flow
- **Automation Status**: Planned
- **Planned Scenario Count**: 2
- **Description**: If the Negative Stock Policy is Lenient, sales are permitted even if they drive stock negative (`MovementType = SALE_OVERRIDE`).

### INV-05: Batch Tracking
- **Module**: Inventory
- **Priority**: High
- **Rule Type**: Process
- **Configurable**: Yes (Per product category)
- **Source**: `ProductBatch.cs`
- **Applies To**: GRN, Sales
- **Automation Status**: Planned
- **Planned Scenario Count**: 3
- **Description**: If a product is batch-tracked, the `BatchId` must be provided during GRN and is consumed during sales based on FIFO.

## Expected Behaviour
- Sales automatically insert a `StockLedgerEntry` with negative quantity.
- GRN (Goods Receipt Note) inserts a `StockLedgerEntry` with positive quantity and establishes a new `UnitCost`.
- Stock adjustments require an approval workflow if they exceed the configured threshold.
