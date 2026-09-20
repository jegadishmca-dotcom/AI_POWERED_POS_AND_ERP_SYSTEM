# ERP & POS System Tracked Backlog / Technical Debt

## 1. Warehouse & Bin Management Database Persistence
- **Component**: [`WarehouseLocationsList.tsx`](file:///d:/JEGADISH/APPLE_SUPERMARKET_POS_PROJECT/AI_POWERED_POS_AND_ERP_SYSTEM/src/Frontend/src/features/inventory/components/WarehouseLocationsList.tsx)
- **Status**: OPEN (Future Pass)
- **Persistence Scope**: `localStorage` (DOES persist across browser page reloads on the local machine).
- **Backend Gap**: Backend database API controllers (`POST /api/warehouses`, `POST /api/warehouses/{id}/bins`) do NOT exist in C# backend service, so data is not saved to PostgreSQL database or synchronized across devices.
- **Required Action**: Create backend `WarehouseController.cs` and EF Core migration in a future pass.

---

## 2. [BUG-FIN-001] [STATUS: RESOLVED] Trial Balance Contra/Abnormal Account Netting Inversion
- **Issue**: Trial Balance report endpoint `GET /api/financialreports/trial-balance` previously reported an artificial cumulative discrepancy of **₹1,426,017.08** between `totalDebits` (₹291,673,961.02) and `totalCredits` (₹293,099,978.10), despite the underlying PostgreSQL General Ledger being **100.0000% balanced** (Total Posted Debits: ₹563,030,950.63 == Total Posted Credits: ₹563,030,950.63, Delta: ₹0.00).
- **Resolution**: Fixed in `FinancialReportingService.cs`. When `bal.CreditBalance < bal.DebitBalance` for Liability/Revenue/Equity accounts, `-net` is now assigned to `bal.DebitBalance = -net; bal.CreditBalance = 0;`.
- **Commit**: `859beb51`
- **Reconciliation & Verification**:
  - `totalDebits`: **₹292,389,183.06**
  - `totalCredits`: **₹292,389,183.06**
  - Variance: **₹0.0000** (100% Balanced).

---

## 3. [FEAT-FIN-002] [STATUS: IN PROGRESS / PARTIALLY RESOLVED] Accounts Payable — Supplier Payment Void / Reversal Workflow
- **Issue**: Need for an explicit void/reversal workflow for recorded supplier payments and journal entries.
- **Critical Architectural Rules (Documented & Enforced)**:
  - **IMMUTABILITY & AUDIT TRAIL**: A void/reversal MUST NEVER delete the original payment record, delete allocations, or recycle/reset sequence counters (`document_sequences`). 
  - **NON-RECYCLING**: The original `SP-xxxxxx` number is permanently preserved in history.
  - **REVERSING TRANSACTIONS**: Reversals must be executed via an explicit compensating journal entry with its OWN NEW sequence number (e.g. `JE-000056` reversing `JE-000055`), and a compensating ledger entry in `supplier_ledger`.
- **Shipped in this Session**:
  - `ReverseJournalEntryCommand` fortified with `IsReversed` check, linking `ReversedByJournalEntryId` and `ReversalOfJournalEntryId` to block duplicate reversal attempts and prevent reversing a reversal entry.
  - Journal entry draft/post/reverse endpoints restricted to authorized roles (`Owner,Manager,Developer`).
  - Sequence counters preserved without deletion or reuse.
- **Commits**: `859beb51`, `3b6ecf3e`, `701f464a`
- **Remaining Open Scope (Future Phase)**:
  - Frontend UI "Void Payment" button with manager authorization dialog in `SupplierBills.tsx` / `SupplierLedger.tsx`.
  - Dedicated `POST /api/accountspayable/payments/{id}/void` controller endpoint orchestrating bill status reset, supplier ledger reversal, and reversing JE generation.

---

## 4. [BUG-FIN-004] [STATUS: RESOLVED] Customer Receipt Wallet Ledger Corruption & UAT Ledger Correction
- **Issue**: Processing an Accounts Receivable customer receipt (`ProcessCustomerReceiptCommand`) previously added a credit entry to `wallet_ledger` and increased the customer's `running_wallet_balance`, giving the customer unearned shopping credit while also not clearing invoices correctly.
- **Resolution**:
  - In `ARCommandsAndQueries.cs`, removed the improper `walletLedgerRepository.AddAsync(walletEntry)` call and `running_wallet_balance` modification. Receipts now strictly reduce Accounts Receivable debt.
  - Added structured audit logging and authorization.
  - Executed atomic data repair on `posdb_uat` for test receipt `CR-000001` (Uma): reset wallet to ₹0.0000, customer ledger to ₹0.0000 with compensating debit, marked receipt `VOIDED`, posted balancing JE `JE-CORR-CR000001` (Dr `20200` ₹350 / Cr `10100` ₹350), and logged to `audit_logs`.
- **Commit**: `3b6ecf3e`

---

## 5. [BUG-FIN-005] [STATUS: RESOLVED] Finance & Accounting Audit Remediation (AP Aging, Tax Slab, GST Net Turnover, Balance Sheet, P&L COGS)
- **Scope**: Comprehensive audit fixes across finance reporting, multi-tenancy, and accounting endpoints:
  1. **AP Purchase Bills Tax Slabs**: Purchase bill creation now inherits explicit CGST/SGST/IGST rates from `TaxSlabId` when items specify 0% / null (`APCommandsAndQueries.cs`).
  2. **AP Aging Breakdown**: Added `GET /api/accountspayable/aging` endpoint to `AccountsPayableController.cs` returning structured aging buckets (`0-30`, `31-60`, `61-90`, `90+` days) to fulfill frontend contract.
  3. **GST Report Formula**: Corrected turnover reporting in `GetGSTReportQuery.cs` to be strictly net of tax (`GrossSales - TotalTax`).
  4. **Balance Sheet Account Visibility**: Removed arbitrary frontend filter (`a.accountNumber > 1`) in `BalanceSheet.tsx`; Account 1 (`Cash Account`) and all valid asset accounts now render correctly.
  5. **Profit & Loss Structure**: Grouped Cost of Goods Sold accounts (`cogsAccounts`) into a dedicated COGS section distinct from operating expenses in `FinancialReportingService.cs`.
  6. **Multi-Tenancy StoreId Integrity**: Unified active store resolution across 6 financial frontend views.
- **Commit**: `3b6ecf3e`

---

## 6. [BUG-FIN-006] [STATUS: RESOLVED] Legacy Vendor Account Resolution Hijack (`BA-*` / `A-*`) & UAT Reclassification
- **Issue**: `AccountResolutionService.cs` previously resolved accounts by matching `AccountCode.StartsWith(prefix)` ordered by `AccountCode.Length DESC`. Legacy vendor code accounts migrated from Sigma (e.g. `BA-423`, `BA-125`) had longer lengths than standard 5-digit COA accounts (`10100`, `10400`), causing runtime cash sales and receipts to post against vendor ledger accounts.
- **Resolution**:
  - Excluded legacy vendor/customer code patterns (`BA-*`, `A-*`) from standard COA resolution and prioritized configured `fallbackCode`.
  - Audited `posdb_live`: **0** transactions, **0** lines affected (pristine).
  - Reclassified all 5 affected test lines in `posdb_uat` (`BA-423` -> `10100`, `BA-125` -> `10400`). Zero non-migration journal entries reference legacy accounts.
- **Commit**: `d9a45b73`

---

## 7. [CHORE-OPS-001] [STATUS: RESOLVED] Standing Pre-Deployment Backup & Scratch-Restore Protocol
- **Issue**: Binary database dumps previously piped through pseudo-TTY (`docker exec -t`) suffered silent CRLF conversion corruption.
- **Resolution**:
  - Implemented 3-step mandatory protocol: dump directly inside container (`-f`), test-restore into scratch database (`posdb_test_restore`), copy verified dumps to host archive (`/home/jegadish/backups/`).
  - Formalized in `.agents/AGENTS.md` as a zero-exception standing rule for every deployment and container rebuild, including hotfixes.
  - Journal entry authorization refined to `Owner,Manager,Developer`.
- **Commits**: `c483e915`, `701f464a`

---

## 8. [STYLE-FIN-003] [STATUS: OPEN / BACKLOG] Harmonize PO and GRN Numbering with DocumentSequences
- **Issue**: Purchase Orders ([CreatePurchaseOrderCommand.cs:42](file:///d:/JEGADISH/APPLE_SUPERMARKET_POS_PROJECT/AI_POWERED_POS_AND_ERP_SYSTEM/src/Backend/PosErp.Application/Features/Purchasing/Commands/CreatePurchaseOrder/CreatePurchaseOrderCommand.cs#L42)) and Goods Receipt Notes ([CreateGRNCommand.cs:64](file:///d:/JEGADISH/APPLE_SUPERMARKET_POS_PROJECT/AI_POWERED_POS_AND_ERP_SYSTEM/src/Backend/PosErp.Application/Features/Purchasing/Commands/CreateGRN/CreateGRNCommand.cs#L64)) generate reference numbers using randomized GUID string suffixes (e.g. `PO-20260920-ABCD1234`, `GRN-20260920-EBD8`), whereas Finance transactions (`JOURNAL_ENTRY`, `SUPPLIER_PAYMENT`, `CUSTOMER_RECEIPT`, `SALES_RETURN`) use monotonically incrementing counters from `document_sequences`.
- **Classification**: Architectural style inconsistency / cosmetic only; not a functional defect or numbering collision bug.
- **Action**: Migrate PO and GRN number generation to `IDocumentSequenceService` in a future refactoring cycle.




