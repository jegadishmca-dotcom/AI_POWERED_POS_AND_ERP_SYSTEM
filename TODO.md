# ERP & POS System Tracked Backlog / Technical Debt

## 1. Warehouse & Bin Management Database Persistence
- **Component**: [`WarehouseLocationsList.tsx`](file:///d:/JEGADISH/APPLE_SUPERMARKET_POS_PROJECT/AI_POWERED_POS_AND_ERP_SYSTEM/src/Frontend/src/features/inventory/components/WarehouseLocationsList.tsx)
- **Persistence Scope**: `localStorage` (DOES persist across browser page reloads on the local machine).
- **Backend Gap**: Backend database API controllers (`POST /api/warehouses`, `POST /api/warehouses/{id}/bins`) do NOT exist in C# backend service, so data is not saved to PostgreSQL database or synchronized across devices.
- **Required Action**: Create backend `WarehouseController.cs` and EF Core migration in a future pass.

---

## 2. [BUG-FIN-001] [PRIORITY: HIGH] Trial Balance Contra/Abnormal Account Netting Inversion
- **Issue**: Trial Balance report endpoint `GET /api/financialreports/trial-balance` reports an artificial cumulative discrepancy of **₹1,426,017.08** between `totalDebits` (₹291,673,961.02) and `totalCredits` (₹293,099,978.10), despite the underlying PostgreSQL General Ledger being **100.0000% balanced** (Total Posted Debits: ₹563,030,950.63 == Total Posted Credits: ₹563,030,950.63, Delta: ₹0.00).
- **Component**: [`FinancialReportingService.cs`](file:///d:/JEGADISH/APPLE_SUPERMARKET_POS_PROJECT/AI_POWERED_POS_AND_ERP_SYSTEM/src/Backend/PosErp.Application/Features/Finance/Services/FinancialReportingService.cs#L419-L433)
- **Root Cause**:
  In lines 428–432:
  ```csharp
  else // LIABILITY, EQUITY, REVENUE
  {
      decimal net = bal.CreditBalance - bal.DebitBalance;
      if (net >= 0) { bal.CreditBalance = net; bal.DebitBalance = 0; }
      else { bal.DebitBalance = 0; bal.CreditBalance = -net; } // BUG: Assigns negative balance to Credit side!
  }
  ```
  When a Liability or Revenue account has a net Debit balance (contra-revenue like Sales Return, or tax asset like Input GST), `net < 0`. The code calculates `-net` (positive) and assigns it to `CreditBalance` instead of `DebitBalance`. This moves the debit balance over to the credit column, producing a double swing (`2 * balance`).
- **Affected Accounts (Exact Breakdown)**:
  1. `[3] SalesReturn (REVENUE)`: Net Debit **₹710,298.74** flipped to Credit column ➔ Discrepancy swing: **+₹1,420,597.48**
  2. `[22030] Input CGST (LIABILITY)`: Net Debit **₹72.00** flipped to Credit column ➔ Discrepancy swing: **+₹144.00**
  3. `[22040] Input SGST (LIABILITY)`: Net Debit **₹72.00** flipped to Credit column ➔ Discrepancy swing: **+₹144.00**
  4. `[91] SGST Input (LIABILITY)`: Net Debit **₹1,282.90** flipped to Credit column ➔ Discrepancy swing: **+₹2,565.80**
  5. `[93] CGST Input (LIABILITY)`: Net Debit **₹1,282.90** flipped to Credit column ➔ Discrepancy swing: **+₹2,565.80**
  - **Sum of Swings**: `1,420,597.48 + 144.00 + 144.00 + 2,565.80 + 2,565.80 = ₹1,426,017.08` (Exact 100.00% match).
- **Before / After Verification**:
  - **Current Reported**: Debits: `₹291,673,961.02` | Credits: `₹293,099,978.10` | Discrepancy: `₹1,426,017.08`
  - **After Corrected Netting**: Debits: `₹292,386,969.56` | Credits: `₹292,386,969.56` | Discrepancy: **₹0.00**
- **Permanent Remediation**:
  Replace lines 419–433 in `FinancialReportingService.cs` with standard accounting balance assignment:
  ```csharp
  foreach (var bal in balances)
  {
      decimal net = bal.DebitBalance - bal.CreditBalance;
      if (net >= 0)
      {
          bal.DebitBalance = net;
          bal.CreditBalance = 0;
      }
      else
      {
          bal.DebitBalance = 0;
          bal.CreditBalance = -net;
      }
  }
  ```
- **UAT Communication**: Must be disclosed to accounts expert prior to testing: *"Known reporting calculation bug logged in issue BUG-FIN-001; underlying GL is fully balanced. Backend reporting patch scheduled separately."*

---

## 3. [FEAT-FIN-002] [PRIORITY: MEDIUM] Accounts Payable — Supplier Payment Void / Reversal Workflow
- **Issue**: The application currently provides `ProcessSupplierPaymentCommand` to record vendor payments, but has no inverse command or API endpoint (`POST /api/accountspayable/payments/{id}/void`) to reverse/cancel an incorrectly recorded payment.
- **Scope Needed**:
  1. Backend `VoidSupplierPaymentCommand`:
     - Sets `SupplierPayment.Status = "VOIDED"`.
     - Reverts allocated `PurchaseBill.Status` from `PAID` back to `PENDING_PAYMENT` / `PARTIALLY_PAID`.
     - Removes or voids `SupplierPaymentAllocations`.
     - Appends reversal entry in `SupplierLedger` (Credit reversal debit).
     - Generates reversing double-entry Journal Entry (Dr Cash/Bank, Cr Accounts Payable).
  2. Frontend UI:
     - Add "Void Payment" button with manager authorization in `SupplierBills.tsx` / `SupplierLedger.tsx`.

