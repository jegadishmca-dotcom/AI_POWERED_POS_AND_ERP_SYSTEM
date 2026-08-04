# Unresolved Business Questions

The following rules and edge cases were identified during the Knowledge Base extraction phase but lack explicit definition in the current codebase or documentation. They require a definitive business decision before automated rules can be implemented in the QA Platform Rule Engine.

## Point of Sale (POS)
1. **Offline Sync**: The `OfflineInvoiceDto` is currently commented out in `CreateInvoiceCommand`. Are offline POS transactions officially supported? If so, what is the sync collision resolution strategy?
2. **Negative Quantities**: Are negative quantities allowed in a standard POS Sale for direct, inline returns, or must all returns go through the dedicated `Sales Return` flow?

## Inventory
3. **Stock Valuation Method**: When a new GRN is received at a different `UnitCost`, how is inventory valuation calculated? Does the system use Moving Average Cost (MAC), FIFO, or Standard Costing?
4. **Stock Adjustments**: Does any stock adjustment require an approval workflow, or only those exceeding a certain monetary threshold?
5. **Over-receiving GRN**: Can a receiver confirm a GRN where `ReceivedQuantity > OrderedQuantity`? Is it hard-blocked, or allowed with a warning/override?

## CRM & Loyalty
6. **Customer Merging**: What is the process for merging duplicate customer profiles? Does the wallet balance and loyalty ledger merge automatically?
7. **Tier Downgrades**: If `EnableAutoTierEvaluation` is true, does a customer immediately lose tier benefits if their 12-month rolling spend drops, or is it evaluated only annually?
8. **Phone Number Changes**: Can a customer change their registered phone number? Does this require OTP verification?
9. **Loyalty Return Reversals**: If a customer returns a product, the loyalty points earned on that product are reversed. What happens if the customer has already spent those points, causing their `RunningLoyaltyPoints` to drop below zero? Is a negative balance allowed?

## Finance & Accounting
10. **Invoice Voiding / Cancellation**: If an invoice is cancelled on the same business day, does the system void the original `JournalEntry` (striking it out), or does it post a new Reversal Journal Entry?
11. **Rounding Differences**: If a multi-item invoice has a 0.01 fractional tax difference between the line-item sum and the invoice total, which account absorbs the difference? Is there a dedicated `Rounding Expense/Income` account?

## Offers & Promotions
12. **Discount Allocation**: When a Cart-level percentage discount is applied, is the discount amount stored purely at the header level, or is it distributed proportionally across the `InvoiceItems`? (Proportional distribution is usually required for accurate GST calculation).

## Security
13. **Concurrent Sessions**: Can the same Cashier log into multiple POS Terminals simultaneously, or does a new login invalidate the previous terminal session?
