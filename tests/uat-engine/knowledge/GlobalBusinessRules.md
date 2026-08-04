# Global Business Rules

## Module Overview
Global Business Rules dictate system-wide behaviors, formatting, audit requirements, and configuration-driven policies that span multiple modules.

## Configurable Business Policies
The system supports multiple operational models. The following rules are driven by system configuration rather than hardcoded logic:
- **Negative Stock Policy**: Determines if the system allows `SALE_OVERRIDE` when stock is insufficient. (Strict vs. Lenient).
- **Inventory Costing Method**: Defines how `UnitCost` is calculated (FIFO, Moving Average, Standard Costing).
- **Loyalty Accrual Policy**: Defines if loyalty points can be earned on discounted items or tax amounts.
- **Approval Thresholds**: Defines the monetary value requiring Manager or Admin approval for POs, GRNs, or Adjustments.
- **Shift Requirement**: Defines if a POS terminal requires an explicit `PosSession` (shift) to be opened, or if it's implicitly tied to the business date.

## Global Rules

### GLB-01: Business Date
- **Module**: Global
- **Priority**: Critical
- **Rule Type**: Policy
- **Configurable**: Yes
- **Source**: `StoreBusinessDate.cs`
- **Applies To**: All financial transactions (Invoices, Journals, GRNs).
- **Automation Status**: Pending
- **Planned Scenario Count**: 2
- **Description**: All financial transactions must be recorded against an `OPEN` business date, regardless of the physical timestamp.

### GLB-02: Currency & Decimal Precision
- **Module**: Global
- **Priority**: High
- **Rule Type**: Formatting
- **Configurable**: Yes (Precision can be set per tenant).
- **Source**: Database schema (`decimal(18,4)` for prices/rates, `decimal(18,2)` for display).
- **Applies To**: All monetary values.
- **Automation Status**: Pending
- **Planned Scenario Count**: 3
- **Description**: Internal calculations use 4 decimal places. Final display and ledger postings use 2 decimal places.

### GLB-03: Rounding Policy
- **Module**: Global
- **Priority**: High
- **Rule Type**: Calculation
- **Configurable**: Yes (Round to nearest 1.00, 0.50, or None).
- **Source**: `CreateInvoiceCommand.cs`
- **Applies To**: Invoice `NetPayable` and Cash payments.
- **Automation Status**: Pending
- **Planned Scenario Count**: 2
- **Description**: Invoice totals are rounded according to the tenant policy. The difference is posted to a specific `RoundOff` account.

### GLB-04: Time Zone
- **Module**: Global
- **Priority**: High
- **Rule Type**: Data Standard
- **Configurable**: No (Database level) / Yes (Display level)
- **Source**: System Architecture
- **Applies To**: All `CreatedAt`, `UpdatedAt`, `BusinessDate` fields.
- **Automation Status**: Pending
- **Planned Scenario Count**: 1
- **Description**: All timestamps are stored in UTC in the database. Reports and UI convert to the tenant's configured Time Zone (e.g., IST).

### GLB-05: Document Numbering
- **Module**: Global
- **Priority**: Medium
- **Rule Type**: Generation
- **Configurable**: Yes (Prefix and sequence rules).
- **Source**: `DocumentSequence.cs`
- **Applies To**: Invoices, GRNs, POs, Journal Entries, Return Notes.
- **Automation Status**: Pending
- **Planned Scenario Count**: 2
- **Description**: Every generated document must have a continuous, unbroken alphanumeric sequence based on the financial year and store prefix.

### GLB-06: Soft Delete Policy
- **Module**: Global
- **Priority**: High
- **Rule Type**: Data Integrity
- **Configurable**: No
- **Source**: Base Entity models (`IsDeleted` flag).
- **Applies To**: All master data (Products, Customers, Users).
- **Automation Status**: Pending
- **Planned Scenario Count**: 2
- **Description**: Master data records cannot be hard-deleted if they are referenced by transactional data. They must be marked as `IsDeleted = true`.

### GLB-07: Audit Logging
- **Module**: Global
- **Priority**: High
- **Rule Type**: Audit
- **Configurable**: Yes (Verbosity levels).
- **Source**: `CreatedBy`, `UpdatedBy` fields.
- **Applies To**: All entities.
- **Automation Status**: Pending
- **Planned Scenario Count**: 1
- **Description**: Every insert/update must record the `UserId` of the actor. Sensitive changes (e.g., Price changes, User role changes) must generate an `AuditLog` entry.

### GLB-08: User Roles & Access
- **Module**: Global
- **Priority**: Critical
- **Rule Type**: Security
- **Configurable**: Yes (Role-based permissions).
- **Source**: `User.cs`, `Role.cs`
- **Applies To**: All API endpoints and UI elements.
- **Automation Status**: Pending
- **Planned Scenario Count**: 5
- **Description**: Users are assigned roles (Admin, Manager, Cashier). Endpoints restrict access based on these roles.

### GLB-09: Terminal Rules
- **Module**: Global
- **Priority**: Medium
- **Rule Type**: Physical Security
- **Configurable**: Yes
- **Source**: `Terminal.cs`
- **Applies To**: POS Logins
- **Automation Status**: Pending
- **Planned Scenario Count**: 2
- **Description**: A POS login must specify a known `TerminalId`.

### GLB-10: Shift Rules
- **Module**: Global
- **Priority**: High
- **Rule Type**: Process
- **Configurable**: Yes
- **Source**: `PosSession.cs`
- **Applies To**: Cashiers
- **Automation Status**: Pending
- **Planned Scenario Count**: 3
- **Description**: A cashier must balance their drawer at the end of the shift. The `ActualClosingCash` is recorded against the `ExpectedClosingCash`.

### GLB-11: Approval Workflows
- **Module**: Global
- **Priority**: High
- **Rule Type**: Process
- **Configurable**: Yes (Approval routes).
- **Source**: `WorkflowApprovalEntities.cs`
- **Applies To**: High-value POs, Stock Adjustments, Manual Journal Entries.
- **Automation Status**: Pending
- **Planned Scenario Count**: 4
- **Description**: Transactions exceeding configured thresholds are placed in a `PENDING_APPROVAL` state until reviewed by an authorized role.
