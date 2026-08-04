# POS Module Knowledge Base

## Module Overview
The Point of Sale (POS) module handles retail billing, shift management, payment collection, and invoicing. It interfaces with Inventory, Finance, CRM, and Offers/Loyalty.

## Configurable Business Policies
- **Shift Requirement**: Defines if a POS terminal requires an explicit `PosSession` (shift) to be opened, or if it's implicitly tied to the business date.
- **Offline Sync Policy**: Determines if offline POS transactions are allowed and how sync collisions are resolved.
- **Direct POS Returns**: Defines if negative quantities are allowed for direct inline returns, or if all returns must use a dedicated workflow.

## Business Rules

### POS-01: Business Date Enforcement
- **Module**: POS
- **Priority**: Critical
- **Rule Type**: Validation
- **Configurable**: Yes (Can be disabled via `RequireOpenBusinessDate` config)
- **Source**: `layer3_workflows.py`, `StoreBusinessDate.cs`
- **Applies To**: Invoice Creation
- **Automation Status**: Planned
- **Planned Scenario Count**: 2
- **Description**: An invoice can only be created if there is an `OPEN` `StoreBusinessDate`.

### POS-02: Session Enforcement
- **Module**: POS
- **Priority**: High
- **Rule Type**: Process
- **Configurable**: Yes (Driven by Shift Requirement policy)
- **Source**: `PosSession.cs`
- **Applies To**: Terminal login and transactions
- **Automation Status**: Planned
- **Planned Scenario Count**: 2
- **Description**: A cashier must have an `OPEN` `PosSession` associated with a valid `TerminalId` to process transactions.

### POS-03: Pricing Validation
- **Module**: POS
- **Priority**: Critical
- **Rule Type**: Invariant
- **Configurable**: No
- **Source**: `CreateProductCommandHandler.cs`
- **Applies To**: Pricing Engine
- **Automation Status**: Planned
- **Planned Scenario Count**: 3
- **Description**: Product `SellingPrice` must be strictly > 0 and <= `Mrp`. `PurchasePrice` must be >= 0.

### POS-04: Cart Total Equation
- **Module**: POS
- **Priority**: Critical
- **Rule Type**: Financial
- **Configurable**: No
- **Source**: `layer4_accounting.py`
- **Applies To**: Checkout
- **Automation Status**: Planned
- **Planned Scenario Count**: 3
- **Description**: The invoice `NetPayable` must exactly equal `TotalAmount` + `TaxAmount` - `DiscountAmount` + `RoundOff`.

### POS-05: Payment Split Equation
- **Module**: POS
- **Priority**: Critical
- **Rule Type**: Financial
- **Configurable**: No
- **Source**: `Invoice` entity
- **Applies To**: Payment Processing
- **Automation Status**: Planned
- **Planned Scenario Count**: 2
- **Description**: The sum of `CashAmount` + `UpiAmount` + `CardAmount` + `WalletAmount` must exactly equal `NetPayable`.

### POS-06: Data Immutability
- **Module**: POS
- **Priority**: Critical
- **Rule Type**: Core Principle
- **Configurable**: No
- **Source**: Standard ERP Practice
- **Applies To**: `COMPLETED` Invoices
- **Automation Status**: Planned
- **Planned Scenario Count**: 1
- **Description**: Once an Invoice is created with `Status = COMPLETED`, it cannot be modified; it can only be cancelled or returned via a Sales Return.

## Expected Behaviour
- Cashier logs in, opens a shift (session).
- Scans items (Barcode/ProductCode).
- Applies promo code or customer loyalty points.
- Selects payment mode (Cash, Card, UPI, Split).
- Submits checkout. System synchronously creates invoice, deducts stock via ledger, and posts a balanced journal entry.

## Validation Rules
- `InvoiceNumber`: Required, must follow configured prefix/sequence.
- `Quantity`: Must be > 0 for sales (unless Direct POS Returns policy is enabled).
- `TerminalSequence`: Must monotonically increment per `TerminalId`.
