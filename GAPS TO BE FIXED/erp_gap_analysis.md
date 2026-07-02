# 🔍 Apple Supermarket ERP — Global Standard Gap Analysis

> **Scope**: Full codebase audit — Backend (ASP.NET Core), Frontend (React/TypeScript), Database (PostgreSQL), Business Logic & Accounting.  
> **Standard**: Global retail ERP standards — comparable to SAP Business One, Oracle NetSuite Retail, Tally Prime, Microsoft Dynamics 365 Commerce.

---

## 🚨 Critical Bugs & Logic Errors

### 1. Accounts Receivable (AR) Uses Wrong GL Account
**File**: [CreateInvoiceCommand.cs](file:///d:/JEGADISH/APPLE_SUPERMARKET_POS_PROJECT/AI_POWERED_POS_AND_ERP_SYSTEM/src/Backend/PosErp.Application/Features/Pos/Commands/CreateInvoiceCommand.cs#L518-L518)  
**Severity**: 🔴 CRITICAL — Financial statements are wrong

```csharp
// BUG: Both Wallet Liability AND Accounts Receivable resolve to the same account code "20200"
string walletAccountCode = await ResolveAccountCodeAsync("LIABILITY", "Wallet", "20200", cancellationToken);
string arAccountCode     = await ResolveAccountCodeAsync("LIABILITY", "Wallet", "20200", cancellationToken); // ← WRONG
```
**Impact**: Credit sales debit the **Customer Wallet Liabilities** account instead of a proper **Accounts Receivable** (an Asset). This produces a wrong balance sheet — AR is never recorded, and credit debtors are invisible.  
**Fix**: AR should map to its own ASSET account (e.g., `10400 - Trade Receivables / AR`).

---

### 2. Double Loyalty Points Earned On Redemption Invoice
**Files**: [CreateInvoiceCommand.cs#L447](file:///d:/JEGADISH/APPLE_SUPERMARKET_POS_PROJECT/AI_POWERED_POS_AND_ERP_SYSTEM/src/Backend/PosErp.Application/Features/Pos/Commands/CreateInvoiceCommand.cs#L447) + [L474](file:///d:/JEGADISH/APPLE_SUPERMARKET_POS_PROJECT/AI_POWERED_POS_AND_ERP_SYSTEM/src/Backend/PosErp.Application/Features/Pos/Commands/CreateInvoiceCommand.cs#L474)  
**Severity**: 🔴 CRITICAL — Loyalty fraud / financial loss

When a customer redeems points:
1. At L450: `customer.RunningLoyaltyPoints -= request.PointsRedeemed` ← redemption deducted  
2. At L468: A `LoyaltyLedgerEntry` is added for the redemption  
3. At L474: `CalculateAndAwardPointsForInvoiceAsync(invoice.Id, customer.Id, invoice.NetPayable)` — this then calls `RecordPointsAsync` which creates a **second** ledger entry AND updates `RunningLoyaltyPoints` a second time

**The `NetPayable` passed to CalculateAndAward already has the discount applied**, but the customer's `RunningLoyaltyPoints` was already decremented at L450 — so `RecordPointsAsync` at L96 reads a stale balance and double-counts the redemption. The ledger will have two `BURN` entries for the same transaction.  
**Fix**: Pass `invoice.NetPayable - pointsDiscountValue` (i.e. actual cash paid) to CalculateAndAward, or better, deduct before awarding so the net balance is correct.

---

### 3. Stock Check Is Done After Invoice Is Saved to DB
**File**: [CreateInvoiceCommand.cs#L308-L355](file:///d:/JEGADISH/APPLE_SUPERMARKET_POS_PROJECT/AI_POWERED_POS_AND_ERP_SYSTEM/src/Backend/PosErp.Application/Features/Pos/Commands/CreateInvoiceCommand.cs#L308)  
**Severity**: 🔴 CRITICAL — Data integrity

```csharp
await _context.SaveChangesAsync(cancellationToken); // ← Invoice committed to DB

// THEN stock is checked:
if (rules.PreventNegativeStock) { ... check stock ... }
```
If stock check fails (or a supervisor override prompt is needed), the invoice is already written but stock is not yet deducted. Under concurrent POS traffic, this race condition allows overselling. The stock validation should happen **before** `SaveChangesAsync`.

---

### 4. `invoices/debug` Endpoint Exposed Without Authentication
**File**: [PosController.cs#L634](file:///d:/JEGADISH/APPLE_SUPERMARKET_POS_PROJECT/AI_POWERED_POS_AND_ERP_SYSTEM/src/Backend/PosErp.Api/Controllers/PosController.cs#L634)  
**Severity**: 🔴 CRITICAL — Security

```csharp
[HttpGet("invoices/debug")]
public async Task<IActionResult> DebugInvoices()   // No [Authorize] attribute
```
This endpoint returns all HOLD invoices including customer IDs and amounts — completely unauthenticated. This is a PII (Personal Identifiable Information) data leak and must be removed before any production release.

---

### 5. Daily Max Redemption Limit Is Not Actually Validated
**File**: [CreateInvoiceCommand.cs#L142-L144](file:///d:/JEGADISH/APPLE_SUPERMARKET_POS_PROJECT/AI_POWERED_POS_AND_ERP_SYSTEM/src/Backend/PosErp.Application/Features/Pos/Commands/CreateInvoiceCommand.cs#L142)  
**Severity**: 🟠 HIGH — Loyalty fraud

```csharp
// Check daily max limit (simplified check, real app would sum today's redemptions)
if (request.PointsRedeemed > loyaltyConfig.MaxRedemptionPerDay)
    throw new Exception(...);
```
The comment says it's simplified — it only checks if the **single transaction** exceeds the daily limit, not the **cumulative total redeemed for the day**. A customer can bypass the limit by making 10 small redemptions equaling the same amount.  
**Fix**: Sum all `PointsRedeemed` from today's `loyalty_ledger` for this customer and add the current request.

---

## 🟠 High-Severity Logic Gaps

### 6. COGS (Cost of Goods Sold) Is Never Posted
**Severity**: 🟠 HIGH — Incomplete Profit & Loss Statement

The double-entry for a retail sale should be:
```
Dr Cash/Bank/UPI         ← Net Payable
    Cr Sales Revenue     ← Taxable Value
    Cr Output CGST       ← CGST
    Cr Output SGST       ← SGST

Dr COGS (Inventory Out)  ← Cost Price × Qty  ← MISSING
    Cr Inventory Asset   ← Cost Price × Qty  ← MISSING
```
The system records the revenue-side journal but never records the COGS journal entry. This means:
- Gross Profit cannot be computed from accounting books alone
- The Inventory Asset account (`10300`) in the GL will never decrease when items are sold
- The P&L will show inflated profit (revenue with zero COGS)

---

### 7. Wallet Top-Up Has No Journal Entry
**Severity**: 🟠 HIGH — Missing liability posting

When a customer tops up their wallet, cash is collected but no journal entry is created:
```
Dr Cash               ← Cash received
    Cr Customer Wallet Liabilities (20200)   ← MISSING
```
The `wallet_ledger` table records the ledger entry, but the financial double-entry is never posted. The GL and wallet balance are out of sync.

---

### 8. Sales Return Flow Exists in DB & Backend But Zero Frontend UI
**Severity**: 🟠 HIGH — Operational gap

The database schema has `sales_returns` and `sales_return_items` tables. The backend has `ReturnCommands.cs` with a full `ProcessSalesReturnCommand`. However, searching the entire frontend codebase returns **zero results** for `salesReturn`.  
**Impact**: Cashiers cannot process refunds from the POS terminal. This is a critical operational gap — every retail system must support returns at the POS level.

---

### 9. Invoice Cancellation Flow Is Missing
**Severity**: 🟠 HIGH — Operational gap

The `invoices` table has `status VARCHAR(20)` with comment `-- COMPLETED, CANCELLED, HOLD`. There is no API endpoint (`DELETE /api/pos/invoices/{id}`) or backend command to cancel a completed invoice and reverse its stock and financial journal entries. Without this, errors in billing cannot be corrected.

---

### 10. `purchase_order_items` Missing Audit & Soft-Delete Columns
**File**: [04_PurchasingSchema.sql](file:///d:/JEGADISH/APPLE_SUPERMARKET_POS_PROJECT/AI_POWERED_POS_AND_ERP_SYSTEM/src/Backend/PosErp.Infrastructure/Persistence/Migrations/04_PurchasingSchema.sql)  
**Severity**: 🟠 HIGH — Compliance / audit gap

`purchase_order_headers` and `purchase_bill_headers` have no `updated_at`, `updated_by`, `is_deleted`, `deleted_at` columns — unlike the core tables. This breaks the consistent audit trail and makes it impossible to track who modified a PO or supplier bill.

---

### 11. Concurrency Race Condition on `terminal_sequence`
**File**: [CreateInvoiceCommand.cs#L164-L168](file:///d:/JEGADISH/APPLE_SUPERMARKET_POS_PROJECT/AI_POWERED_POS_AND_ERP_SYSTEM/src/Backend/PosErp.Application/Features/Pos/Commands/CreateInvoiceCommand.cs#L164)  
**Severity**: 🟠 HIGH — Data integrity under load

```csharp
var lastSeq = await _context.Invoices
    .Where(i => i.TerminalId == request.TerminalId && i.BusinessDate == today)
    .Select(i => (int?)i.TerminalSequence).MaxAsync() ?? 0;
var nextSeq = lastSeq + 1;
```
Two simultaneous checkouts on the same terminal within the same millisecond will both read the same `lastSeq` and produce the same `nextSeq`, causing a unique constraint violation. This should use a PostgreSQL `SEQUENCE` or `SELECT ... FOR UPDATE` lock.

---

## 🟡 Medium-Severity Issues

### 12. Loyalty Points Earned Should Use `TotalAmount - PointsDiscount` (Cash Paid), Not `NetPayable`
**File**: [CreateInvoiceCommand.cs#L474](file:///d:/JEGADISH/APPLE_SUPERMARKET_POS_PROJECT/AI_POWERED_POS_AND_ERP_SYSTEM/src/Backend/PosErp.Application/Features/Pos/Commands/CreateInvoiceCommand.cs#L474)  
**Industry Standard**: Points are earned only on actual cash/card payment, not on discounts or points redemption itself (circular earning). The current code passes `invoice.NetPayable` which already includes the points discount, so it **over-awards** by a tiny fraction. The correct value to pass is `invoice.NetPayable - pointsDiscountValue`.

---

### 13. Birthday/Anniversary Offers Use Month Comparison Only
**File**: [CreateInvoiceCommand.cs#L98-L99](file:///d:/JEGADISH/APPLE_SUPERMARKET_POS_PROJECT/AI_POWERED_POS_AND_ERP_SYSTEM/src/Backend/PosErp.Application/Features/Pos/Commands/CreateInvoiceCommand.cs#L98)  
```csharp
bool isBirthday = customer?.Dob.HasValue == true && customer.Dob.Value.Month == DateTime.Today.Month;
```
This gives birthday discounts for the **entire birth month**, not the birth day. Industry standard is to give a configurable window (e.g., ±7 days around birthday). A customer born on Jan 31st gets the birthday discount for all of January.

---

### 14. GRN `purchase_order_item_id` Is `NOT NULL` — Blocks Direct GRN Without PO
**File**: [04_PurchasingSchema.sql#L49](file:///d:/JEGADISH/APPLE_SUPERMARKET_POS_PROJECT/AI_POWERED_POS_AND_ERP_SYSTEM/src/Backend/PosErp.Infrastructure/Persistence/Migrations/04_PurchasingSchema.sql#L49)  
```sql
purchase_order_item_id UUID NOT NULL REFERENCES purchase_order_items(id)
```
Real supermarkets frequently receive goods without a pre-raised PO (urgent restocks, market purchases). The schema enforces PO → GRN flow strictly, blocking direct GRN creation. This should be `NULLABLE`.

---

### 15. Products Table Has Both `mrp` and `selling_price` — No Price History
**File**: [01_CoreSchema.sql#L167-L168](file:///d:/JEGADISH/APPLE_SUPERMARKET_POS_PROJECT/AI_POWERED_POS_AND_ERP_SYSTEM/src/Backend/PosErp.Infrastructure/Persistence/Migrations/01_CoreSchema.sql#L167)  
When you change an MRP or selling price, there is no `product_price_history` table. This means:
- You cannot answer "what was the price on 15-June-2026?"
- Historical invoice reprints may show the current (wrong) price
- Effective-date-based price scheduling is not possible

---

### 16. `stock_adjustments` Has No Journal Entry Link
**File**: [03_InventorySchema.sql#L74-L81](file:///d:/JEGADISH/APPLE_SUPERMARKET_POS_PROJECT/AI_POWERED_POS_AND_ERP_SYSTEM/src/Backend/PosErp.Infrastructure/Persistence/Migrations/03_InventorySchema.sql#L74)  
When stock is adjusted (shrinkage/damage), the adjustment should create:
```
Dr Shrinkage/Loss Expense   ← Cost value of lost stock
    Cr Inventory Asset       ← Decrease inventory
```
The `stock_adjustments` table has no `journal_entry_id` column and no corresponding financial posting. Inventory losses are never reflected in the P&L.

---

### 17. Wallet Spend Has No Negative Guard in `wallet_ledger`
**File**: [CreateInvoiceCommand.cs#L444-L445](file:///d:/JEGADISH/APPLE_SUPERMARKET_POS_PROJECT/AI_POWERED_POS_AND_ERP_SYSTEM/src/Backend/PosErp.Application/Features/Pos/Commands/CreateInvoiceCommand.cs#L444)  
The wallet debit records `-request.WalletAmountUsed` but there is no server-side check that `customer.RunningWalletBalance >= request.WalletAmountUsed` before the deduction. An attacker could send a crafted API request to overdraw a customer's wallet.

---

### 18. `customers` Table Missing `email` Column
**File**: [07_CrmAndOffersSchema.sql](file:///d:/JEGADISH/APPLE_SUPERMARKET_POS_PROJECT/AI_POWERED_POS_AND_ERP_SYSTEM/src/Backend/PosErp.Infrastructure/Persistence/Migrations/07_CrmAndOffersSchema.sql)  
Global retail CRM systems store customer email for e-receipt delivery, marketing campaigns, and loyalty notifications. The `customers` table has `phone`, `dob`, `anniversary`, `marketing_consent` but no `email` field. The `29_AddCustomerCrmFields.sql` migration adds several CRM fields — but email is still absent.

---

### 19. No Duplicate Invoice Number Guard at Application Layer
**File**: [CreateInvoiceCommand.cs](file:///d:/JEGADISH/APPLE_SUPERMARKET_POS_PROJECT/AI_POWERED_POS_AND_ERP_SYSTEM/src/Backend/PosErp.Application/Features/Pos/Commands/CreateInvoiceCommand.cs)  
The `invoice_number` is generated by the frontend (`INV-{terminal_code}-{Date.now().slice(-6)}`). There is no server-side uniqueness check before inserting. The DB has no UNIQUE constraint on `invoice_number` alone (only on `(terminal_id, terminal_sequence, business_date)`). Two offline-synced invoices from different devices could share the same invoice number.

---

### 20. `financial_years` Table Has No Integration with Period Locks
**File**: [25_FinanceModuleSchema.sql#L244](file:///d:/JEGADISH/APPLE_SUPERMARKET_POS_PROJECT/AI_POWERED_POS_AND_ERP_SYSTEM/src/Backend/PosErp.Infrastructure/Persistence/Migrations/25_FinanceModuleSchema.sql#L244)  
`financial_years` and `financial_period_locks` both exist, but there is no automatic lock created when a financial year is closed. Closing `FY-2026-27` should automatically lock all its periods to prevent backdated entries. This connection is absent from the application logic.

---

## 🔵 Missing Global Standard Features

### 21. No Credit Note / Debit Note Module
Retail ERP standard: When a sales return is processed, a **Credit Note** must be issued to the customer (for cash/UPI purchases, a refund voucher; for credit customers, a CN reducing their AR balance). The schema has `sales_returns` but no `credit_notes` or `debit_notes` tables.

### 22. No Goods Return Note (GRN Rejection → Supplier Return) Workflow
When a GRN item is `rejected_quantity > 0`, those items should flow automatically into a **Purchase Return** with a **Debit Note** to the supplier. Currently the `rejected_quantity` is stored but the downstream workflow (supplier debit note, stock exclusion, GL reversal) is not implemented.

### 23. No Minimum Order Quantity (MOQ) / Reorder Point Automation on PO
Standard retail ERP auto-generates Purchase Order suggestions when stock falls below the reorder point. While there is `inventory_intelligence` (AI forecasting), there is no actual **auto-PO generation** trigger linked to the `products` table `reorder_level` field.

### 24. No Customer Credit Limit Enforcement at POS UI Level
Backend validates credit limits for `CREDIT` payment mode, but the **POS frontend has no credit payment mode selector**. The "CREDIT" branch exists in backend code but is unreachable from the UI — credit sales cannot be made at the POS counter at all.

### 25. No Shift Cash Denomination Count (Till Count)
Global standard POS shift close requires the cashier to enter physical denomination counts (₹500 × N, ₹200 × N, etc.) and the system reconciles against expected cash. The current `CloseShiftModal` captures `actualClosingCash` as a single number. No denomination breakdown is collected or stored.

### 26. No Customer Facing Display (CFD) / Second Screen Support
Retail POS global standard: A second customer-facing screen that shows items being scanned, running total, and "Thank You" after payment. There is no CFD component or API hook for this.

### 27. No Negative Stock Alert on the POS Billing Screen
When a product goes below safety stock or is out of stock, the POS cashier currently gets a backend exception only during checkout. There is no pre-scan warning or visual indicator on the product search results that a product is low/out of stock.

### 28. No Supplier Payment Due Date Tracking / Aging Report
`purchase_bill_headers` has `bill_date` but no `due_date`. Accounts Payable aging (0–30 days, 30–60 days, 60–90 days, 90+ days) is a core finance module requirement for cash flow management and supplier relationship management.

### 29. No Multi-Currency Support
The entire system assumes INR (₹). For global standard ERP, purchase bills from international suppliers may be in USD/EUR. There is no `currency_code`, `exchange_rate`, or `base_amount` tracking on any financial table.

### 30. No HSN-Wise GST Summary on POS Receipt (Required by Indian Law)
As per Indian GST law, a Tax Invoice must show a GST summary **by HSN code** (not just by rate). The thermal receipt currently groups by GST rate (`GST 5%`, `GST 12%`). This is non-compliant for businesses with turnover above ₹5 crores, which must print HSN-wise tax summaries.

---

## ⚙️ Code Quality & Security Issues

### 31. Hardcoded Store Details in POS Print Service
**File**: [PosController.cs#L239-L245](file:///d:/JEGADISH/APPLE_SUPERMARKET_POS_PROJECT/AI_POWERED_POS_AND_ERP_SYSTEM/src/Backend/PosErp.Api/Controllers/PosController.cs#L239)  
Store name, address, GSTIN, FSSAI number are all hardcoded in the `PrintReceipt` action. These should be loaded from the `stores` table dynamically, especially for a multi-store setup.

### 32. No Refresh Token Rotation (Security)
JWTs are issued but there is no evidence of refresh token rotation or revocation. If a JWT is stolen, it remains valid until expiry. A refresh token rotation strategy (invalidating old tokens on use) should be implemented.

### 33. `Console.WriteLine` Used for Production Logging
**File**: [PosController.cs#L416](file:///d:/JEGADISH/APPLE_SUPERMARKET_POS_PROJECT/AI_POWERED_POS_AND_ERP_SYSTEM/src/Backend/PosErp.Api/Controllers/PosController.cs#L416)  
Production code should use `ILogger<T>` with structured logging (e.g., Serilog). Raw `Console.WriteLine` statements produce unstructured output that cannot be queried, filtered, or shipped to a log aggregator.

### 34. Tax GST Summary in Receipt Uses Discounted Pre-Tax Line Amount — Incorrect for HSN Reporting
**File**: [PosController.cs#L332](file:///d:/JEGADISH/APPLE_SUPERMARKET_POS_PROJECT/AI_POWERED_POS_AND_ERP_SYSTEM/src/Backend/PosErp.Api/Controllers/PosController.cs#L332)  
```csharp
decimal lineAmt = item.Quantity * item.UnitPrice - item.DiscountAmount;
gstGroups[rate] = (current.taxable + lineAmt, ...)
```
The `taxable` column in the GST summary shows MRP × Qty − Discount (i.e., the **tax-inclusive** post-discount amount), not the actual **taxable value** (pre-tax). Indian GST receipts must show the taxable base *excluding tax*, not including tax.

### 35. No Row-Level Security (RLS) on PostgreSQL
All tables have `-- RLS Comment: ALTER TABLE ... ENABLE ROW LEVEL SECURITY;` — meaning RLS is intentionally disabled. For a multi-store deployment, all stores share the same DB and a compromised API could read other stores' data by crafting requests with a different `store_id`.

### 36. Offline Sync Journal Posting Diverged from Online Checkout (Critical)
**File**: [SyncInvoicesCommand.cs#L110-L135](file:///d:/JEGADISH/APPLE_SUPERMARKET_POS_PROJECT/AI_POWERED_POS_AND_ERP_SYSTEM/src/Backend/PosErp.Application/Features/Pos/Commands/SyncInvoices/SyncInvoicesCommand.cs#L110)
Multiple critical bookkeeping divergence issues exist in the offline sync path:
1. **Invalid Account Codes**: The sync loop hardcodes 4-digit codes `"1000"`, `"1100"`, `"4000"`, `"2200"`, `"2201"` which do not exist in the database (only 5-digit codes like `10100`, `10200`, `40100` are seeded). As a result, `PostJournalEntryAsync` throws an exception, and every offline sync operation is currently 100% broken.
2. **Missing COGS Posting**: There is no P&L cost of goods sold journal entry generated for synced invoices.
3. **Missing Stock Movement**: The sync path does not call `RecordMovementAsync` or deduct stock from product batches, leaving stock ledger levels out of sync.
4. **Missing Customer Wallet & Loyalty Reconciliation**: Synced invoices do not deduct wallet balances or update customer loyalty ledger points.

### 37. Offline Sync Wallet/Loyalty/Split Payment Data Loss (High)
**File**: [SyncInvoicesCommand.cs](file:///d:/JEGADISH/APPLE_SUPERMARKET_POS_PROJECT/AI_POWERED_POS_AND_ERP_SYSTEM/src/Backend/PosErp.Application/Features/Pos/Commands/SyncInvoices/SyncInvoicesCommand.cs)  
The frontend client allows cashiers to select a customer, redeem loyalty points, pay with wallet balances, or execute split payments (Cash + UPI/Card/Wallet) during offline checkouts. These properties are captured in IndexedDB (`fullInvoice` has `customerId`, `walletAmountUsed`, `pointsRedeemed`, `cashAmount`, etc.) and sent in the sync payload array to the backend `/api/pos/sync` endpoint.
However, the backend `OfflineInvoiceDto` C# record does not define these properties. As a result, they are silently discarded during JSON deserialization. This causes permanent data loss for customer identities, customer loyalty points balance updates, and wallet spend records, which will result in balance sheet/ledger discrepancies.

---

## 📋 Summary Table

| # | Category | Issue | Severity |
|---|----------|-------|----------|
| 1 | Accounting | AR uses wrong GL account (Wallet instead of Receivable) | 🔴 Critical |
| 2 | Loyalty | Double loyalty points on redemption invoices | 🔴 Critical |
| 3 | Inventory | Stock check after invoice save (race condition) | 🔴 Critical |
| 4 | Security | Debug endpoint exposed without authentication | 🔴 Critical |
| 5 | Loyalty | Daily redemption limit not actually summed | 🔴 Critical |
| 6 | Accounting | COGS journal entry missing — P&L incomplete | 🟠 High |
| 7 | Accounting | Wallet top-up has no GL posting | 🟠 High |
| 8 | Operations | Sales Return has no Frontend UI | 🟠 High |
| 9 | Operations | Invoice cancellation flow missing | 🟠 High |
| 10 | Audit | PO/GRN tables lack audit columns | 🟠 High |
| 11 | Data Integrity | Terminal sequence race condition under load | 🟠 High |
| 12 | Loyalty | Points earned on discount value, not cash paid | 🟡 Medium |
| 13 | CRM | Birthday discount lasts entire month | 🟡 Medium |
| 14 | Purchasing | GRN requires PO — blocks direct receipt | 🟡 Medium |
| 15 | Catalog | No product price history table | 🟡 Medium |
| 16 | Inventory | Stock adjustment has no GL posting | 🟡 Medium |
| 17 | Security | No server-side wallet balance check before spend | 🟡 Medium |
| 18 | CRM | No email field on customer table | 🟡 Medium |
| 19 | Data Integrity | No duplicate invoice number guard | 🟡 Medium |
| 20 | Finance | Financial year close doesn't auto-lock periods | 🟡 Medium |
| 21 | Missing Feature | No Credit Note / Debit Note module | 🔵 Feature |
| 22 | Missing Feature | No purchase return from rejected GRN workflow | 🔵 Feature |
| 23 | Missing Feature | No auto-PO generation from reorder point | 🔵 Feature |
| 24 | Missing Feature | Credit payment mode not reachable from POS UI | 🔵 Feature |
| 25 | Missing Feature | No shift cash denomination count | 🔵 Feature |
| 26 | Missing Feature | No customer-facing display support | 🔵 Feature |
| 27 | UX | No low-stock warning on POS scan | 🔵 Feature |
| 28 | Finance | No AP due date / aging report | 🔵 Feature |
| 29 | Finance | No multi-currency support | 🔵 Feature |
| 30 | Compliance | HSN-wise GST summary missing on receipt | 🔵 Feature |
| 31 | Code Quality | Store details hardcoded in print controller | ⚙️ Quality |
| 32 | Security | No refresh token rotation | ⚙️ Quality |
| 33 | Code Quality | Console.WriteLine in production | ⚙️ Quality |
| 34 | Compliance | Tax GST summary in receipt uses discounted pre-tax amount | ⚙️ Quality |
| 35 | Security | Row Level Security disabled on all tables | ⚙️ Quality |
| 36 | Data Integrity | Offline sync journal posting diverged from online checkout | 🔴 Critical |
| 37 | Data Integrity | Offline sync wallet/loyalty/split payment data loss | 🟠 High |

---

> **Recommended Priority Order for Fixes**:  
> 1. Fix #4 (security) and #1 (AR account) immediately before any merge to main.  
> 2. Fix #2, #3, #5 (loyalty + stock race) before UAT sign-off.  
> 3. Implement #8 (Sales Return UI) and #6 (COGS posting) as mandatory pre-launch features.  
> 4. Address compliance items #30, #34 to pass GST audit.
