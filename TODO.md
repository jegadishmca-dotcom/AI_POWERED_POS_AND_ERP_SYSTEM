# ERP & POS System Tracked Backlog / Technical Debt

## 1. Warehouse & Bin Management Database Persistence
- **Component**: [`WarehouseLocationsList.tsx`](file:///d:/JEGADISH/APPLE_SUPERMARKET_POS_PROJECT/AI_POWERED_POS_AND_ERP_SYSTEM/src/Frontend/src/features/inventory/components/WarehouseLocationsList.tsx)
- **Persistence Scope**: `localStorage` (DOES persist across browser page reloads on the local machine).
- **Backend Gap**: Backend database API controllers (`POST /api/warehouses`, `POST /api/warehouses/{id}/bins`) do NOT exist in C# backend service, so data is not saved to PostgreSQL database or synchronized across devices.
- **Required Action**: Create backend `WarehouseController.cs` and EF Core migration in a future pass.

---

## 2. [BUG-FIN-001] [STATUS: RESOLVED] Trial Balance Contra/Abnormal Account Netting Inversion
- **Issue**: Trial Balance report endpoint `GET /api/financialreports/trial-balance` previously reported an artificial cumulative discrepancy of **₹1,426,017.08** between `totalDebits` (₹291,673,961.02) and `totalCredits` (₹293,099,978.10), despite the underlying PostgreSQL General Ledger being **100.0000% balanced** (Total Posted Debits: ₹563,030,950.63 == Total Posted Credits: ₹563,030,950.63, Delta: ₹0.00).
- **Resolution**: Fixed in `FinancialReportingService.cs` line 431. When `bal.CreditBalance < bal.DebitBalance` for Liability/Revenue/Equity accounts, `-net` is now assigned to `bal.DebitBalance = -net; bal.CreditBalance = 0;`.
- **Reconciliation**:
  - `totalDebits`: **₹292,386,969.56**
  - `totalCredits`: **₹292,386,969.56**
  - Variance: **₹0.0000** (100% Balanced).

---

## 3. [FEAT-FIN-002] [PRIORITY: MEDIUM] Accounts Payable — Supplier Payment Void / Reversal Workflow
- **Issue**: The application currently provides `ProcessSupplierPaymentCommand` to record vendor payments, but has no inverse command or API endpoint (`POST /api/accountspayable/payments/{id}/void`) to reverse/cancel an incorrectly recorded payment.
- **Critical Architectural Rules**:
  - **IMMUTABILITY & AUDIT TRAIL**: A void/reversal MUST NEVER delete the original payment record, delete allocations, or recycle/reset sequence counters (`document_sequences`). 
  - **NON-RECYCLING**: The original `SP-xxxxxx` number is permanently preserved in history.
  - **REVERSING TRANSACTIONS**: Reversals must be executed via an explicit compensating journal entry with its OWN NEW sequence number (e.g. `JE-000056` reversing `JE-000055`), and a compensating ledger entry in `supplier_ledger`.
- **Scope to Implement**:
  1. Backend `VoidSupplierPaymentCommand`:
     - Sets `SupplierPayment.Status = "VOIDED"`.
     - Reverts allocated `PurchaseBill.Status` from `PAID` back to `PENDING_PAYMENT` / `PARTIALLY_PAID`.
     - Retains `SupplierPaymentAllocations` marked `Status = "VOIDED"` (or soft-deleted).
     - Appends reversal entry in `SupplierLedger` (Credit reversal of original payment debit).
     - Generates reversing double-entry Journal Entry with a **NEW sequence number** (Dr Cash/Bank, Cr Accounts Payable).
  2. Frontend UI:
     - Add "Void Payment" button with manager authorization dialog in `SupplierBills.tsx` / `SupplierLedger.tsx`.

---

## 4. [STYLE-FIN-003] [PRIORITY: LOW / COSMETIC] Harmonize PO and GRN Numbering with DocumentSequences
- **Issue**: Purchase Orders ([CreatePurchaseOrderCommand.cs:42](file:///d:/JEGADISH/APPLE_SUPERMARKET_POS_PROJECT/AI_POWERED_POS_AND_ERP_SYSTEM/src/Backend/PosErp.Application/Features/Purchasing/Commands/CreatePurchaseOrder/CreatePurchaseOrderCommand.cs#L42)) and Goods Receipt Notes ([CreateGRNCommand.cs:64](file:///d:/JEGADISH/APPLE_SUPERMARKET_POS_PROJECT/AI_POWERED_POS_AND_ERP_SYSTEM/src/Backend/PosErp.Application/Features/Purchasing/Commands/CreateGRN/CreateGRNCommand.cs#L64)) generate reference numbers using randomized GUID string suffixes (e.g. `PO-20260920-ABCD1234`, `GRN-20260920-EBD8`), whereas Finance transactions (`JOURNAL_ENTRY`, `SUPPLIER_PAYMENT`, `CUSTOMER_RECEIPT`, `SALES_RETURN`) use monotonically incrementing counters from `document_sequences`.
- **Classification**: Architectural style inconsistency / cosmetic only; not a functional defect or numbering collision bug.
- **Recommended Action**: Migrate PO and GRN number generation to `IDocumentSequenceService` in a future refactoring cycle.



