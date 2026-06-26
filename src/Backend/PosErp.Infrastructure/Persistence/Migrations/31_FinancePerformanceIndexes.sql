-- ==============================================================================
-- PHASE 4.6: PERFORMANCE INDEXES FOR FINANCE & GENERAL LEDGER
-- ==============================================================================

-- General Ledger & Journal Entries
CREATE INDEX IF NOT EXISTS idx_journal_entries_store_status 
    ON journal_entries(store_id, status);

CREATE INDEX IF NOT EXISTS idx_journal_entry_lines_je 
    ON journal_entry_lines(journal_entry_id);

CREATE INDEX IF NOT EXISTS idx_journal_entry_lines_account 
    ON journal_entry_lines(account_id);

-- Supplier Ledger & Bills (AP)
CREATE INDEX IF NOT EXISTS idx_supplier_ledger_store_supplier 
    ON supplier_ledger(store_id, supplier_id);

CREATE INDEX IF NOT EXISTS idx_purchase_bills_store_supplier 
    ON purchase_bill_headers(store_id, supplier_id);

CREATE INDEX IF NOT EXISTS idx_supplier_payments_store_supplier 
    ON supplier_payments(store_id, supplier_id);

CREATE INDEX IF NOT EXISTS idx_supplier_payment_allocations_pay_bill 
    ON supplier_payment_allocations(payment_id, purchase_bill_id);

-- Customer Ledger & Receipts (AR)
CREATE INDEX IF NOT EXISTS idx_customer_ledger_store_customer 
    ON customer_ledger(store_id, customer_id);

CREATE INDEX IF NOT EXISTS idx_customer_receipts_store_customer 
    ON customer_receipts(store_id, customer_id);

CREATE INDEX IF NOT EXISTS idx_customer_receipt_allocations_rcpt_inv 
    ON customer_receipt_allocations(receipt_id, invoice_id);

-- Returns
CREATE INDEX IF NOT EXISTS idx_purchase_returns_store_supplier 
    ON purchase_returns(store_id, supplier_id);

CREATE INDEX IF NOT EXISTS idx_sales_returns_store_invoice 
    ON sales_returns(store_id, invoice_id);
