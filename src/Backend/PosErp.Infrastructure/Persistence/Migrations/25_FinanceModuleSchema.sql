-- ==============================================================================
-- PHASE 4.1: STORE MASTER & STORE-AWARE INTEGRATION
-- ==============================================================================

CREATE TABLE IF NOT EXISTS stores (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    store_code VARCHAR(50) UNIQUE NOT NULL,
    store_name VARCHAR(200) NOT NULL,
    address TEXT,
    gstin VARCHAR(15), -- 15-character GSTIN in India
    contact_number VARCHAR(20),
    email VARCHAR(100),
    manager_id UUID REFERENCES users(id) ON DELETE SET NULL,
    is_active BOOLEAN DEFAULT TRUE NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL,
    created_by UUID,
    updated_at TIMESTAMP WITH TIME ZONE,
    updated_by UUID,
    is_deleted BOOLEAN DEFAULT FALSE NOT NULL,
    deleted_at TIMESTAMP WITH TIME ZONE
);

-- Seed default store to support existing records
INSERT INTO stores (id, store_code, store_name, address, gstin, is_active)
VALUES ('00000000-0000-0000-0000-000000000000', 'STORE-01', 'Apple Supermarket Head Office', 'Main Road, Kumbakonam', '33AAAAA1111A1Z1', TRUE)
ON CONFLICT (store_code) DO NOTHING;

-- Update product_batches to track available quantities and GRN reference
ALTER TABLE product_batches ADD COLUMN IF NOT EXISTS available_quantity DECIMAL(18,4) DEFAULT 0 NOT NULL;
ALTER TABLE product_batches ADD COLUMN IF NOT EXISTS grn_reference VARCHAR(100);

-- ==============================================================================
-- PHASE 4.2: SUB-LEDGERS, PAYMENTS/RECEIPTS & ALLOCATIONS
-- ==============================================================================

CREATE TABLE IF NOT EXISTS supplier_ledger (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    store_id UUID NOT NULL REFERENCES stores(id) ON DELETE RESTRICT,
    supplier_id UUID NOT NULL REFERENCES suppliers(id) ON DELETE RESTRICT,
    entry_date DATE NOT NULL,
    transaction_type VARCHAR(50) NOT NULL, -- BILL, PAYMENT, DEBIT_NOTE, CREDIT_NOTE
    reference_number VARCHAR(100) NOT NULL,
    debit_amount DECIMAL(18,4) DEFAULT 0 NOT NULL,
    credit_amount DECIMAL(18,4) DEFAULT 0 NOT NULL,
    running_balance DECIMAL(18,4) DEFAULT 0 NOT NULL,
    description TEXT,
    journal_entry_id UUID REFERENCES journal_entries(id) ON DELETE SET NULL,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL
);

CREATE TABLE IF NOT EXISTS customer_ledger (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    store_id UUID NOT NULL REFERENCES stores(id) ON DELETE RESTRICT,
    customer_id UUID NOT NULL REFERENCES customers(id) ON DELETE RESTRICT,
    entry_date DATE NOT NULL,
    transaction_type VARCHAR(50) NOT NULL, -- INVOICE, RECEIPT, CREDIT_NOTE, DEBIT_NOTE
    reference_number VARCHAR(100) NOT NULL,
    debit_amount DECIMAL(18,4) DEFAULT 0 NOT NULL,
    credit_amount DECIMAL(18,4) DEFAULT 0 NOT NULL,
    running_balance DECIMAL(18,4) DEFAULT 0 NOT NULL,
    description TEXT,
    journal_entry_id UUID REFERENCES journal_entries(id) ON DELETE SET NULL,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL
);

CREATE TABLE IF NOT EXISTS supplier_payments (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    store_id UUID NOT NULL REFERENCES stores(id) ON DELETE RESTRICT,
    supplier_id UUID NOT NULL REFERENCES suppliers(id) ON DELETE RESTRICT,
    payment_date DATE NOT NULL,
    payment_number VARCHAR(100) UNIQUE NOT NULL,
    payment_mode VARCHAR(50) NOT NULL,
    reference_number VARCHAR(100),
    amount DECIMAL(18,4) NOT NULL,
    journal_entry_id UUID REFERENCES journal_entries(id) ON DELETE SET NULL,
    status VARCHAR(50) DEFAULT 'PENDING_APPROVAL' NOT NULL, -- PENDING_APPROVAL, APPROVED, POSTED, VOID
    notes TEXT,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL
);

-- Payment allocations mapping to bills (handles partial/full settlement)
CREATE TABLE IF NOT EXISTS supplier_payment_allocations (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    payment_id UUID NOT NULL REFERENCES supplier_payments(id) ON DELETE CASCADE,
    purchase_bill_id UUID NOT NULL REFERENCES purchase_bill_headers(id) ON DELETE RESTRICT,
    allocated_amount DECIMAL(18,4) NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL
);

CREATE TABLE IF NOT EXISTS customer_receipts (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    store_id UUID NOT NULL REFERENCES stores(id) ON DELETE RESTRICT,
    customer_id UUID NOT NULL REFERENCES customers(id) ON DELETE RESTRICT,
    receipt_date DATE NOT NULL,
    receipt_number VARCHAR(100) UNIQUE NOT NULL,
    payment_mode VARCHAR(50) NOT NULL,
    reference_number VARCHAR(100),
    amount DECIMAL(18,4) NOT NULL,
    journal_entry_id UUID REFERENCES journal_entries(id) ON DELETE SET NULL,
    status VARCHAR(50) DEFAULT 'POSTED' NOT NULL,
    notes TEXT,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL
);

-- Receipt allocations mapping to invoices (handles partial/full settlement)
CREATE TABLE IF NOT EXISTS customer_receipt_allocations (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    receipt_id UUID NOT NULL REFERENCES customer_receipts(id) ON DELETE CASCADE,
    invoice_id UUID NOT NULL,
    invoice_business_date DATE NOT NULL,
    allocated_amount DECIMAL(18,4) NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL,
    FOREIGN KEY (invoice_id, invoice_business_date) REFERENCES invoices(id, business_date) ON DELETE RESTRICT
);

-- ==============================================================================
-- PHASE 4.3: BANK, PETTY CASH (SHIFT ACCOUNTING) & CENTRALIZED SEQUENCES
-- ==============================================================================

CREATE TABLE IF NOT EXISTS bank_accounts (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    store_id UUID NOT NULL REFERENCES stores(id) ON DELETE RESTRICT,
    account_name VARCHAR(100) NOT NULL,
    bank_name VARCHAR(100) NOT NULL,
    account_number VARCHAR(50) UNIQUE NOT NULL,
    ifs_code VARCHAR(20) NOT NULL,
    branch VARCHAR(100),
    gl_account_id UUID NOT NULL REFERENCES accounts(id) ON DELETE RESTRICT,
    current_balance DECIMAL(18,4) DEFAULT 0 NOT NULL,
    is_active BOOLEAN DEFAULT TRUE NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL
);

CREATE TABLE IF NOT EXISTS bank_transactions (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    bank_account_id UUID NOT NULL REFERENCES bank_accounts(id) ON DELETE RESTRICT,
    transaction_date DATE NOT NULL,
    type VARCHAR(50) NOT NULL, -- DEPOSIT, WITHDRAWAL, BANK_FEE, INTEREST
    amount DECIMAL(18,4) NOT NULL,
    reference_number VARCHAR(100),
    description TEXT,
    is_reconciled BOOLEAN DEFAULT FALSE NOT NULL,
    reconciled_date DATE,
    journal_entry_id UUID REFERENCES journal_entries(id) ON DELETE SET NULL,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL
);

CREATE TABLE IF NOT EXISTS petty_cash_ledger (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    store_id UUID NOT NULL REFERENCES stores(id) ON DELETE RESTRICT,
    transaction_date DATE NOT NULL,
    voucher_number VARCHAR(100) UNIQUE NOT NULL,
    description TEXT NOT NULL,
    category VARCHAR(100) NOT NULL,
    debit_amount DECIMAL(18,4) DEFAULT 0 NOT NULL,
    credit_amount DECIMAL(18,4) DEFAULT 0 NOT NULL,
    running_balance DECIMAL(18,4) DEFAULT 0 NOT NULL,
    requested_by VARCHAR(200),
    approved_by UUID REFERENCES users(id) ON DELETE RESTRICT,
    journal_entry_id UUID REFERENCES journal_entries(id) ON DELETE SET NULL,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL
);

-- In-place modification of pos_sessions to post shift discrepancies and link store
ALTER TABLE pos_sessions ADD COLUMN IF NOT EXISTS store_id UUID REFERENCES stores(id) ON DELETE RESTRICT;
ALTER TABLE pos_sessions ADD COLUMN IF NOT EXISTS journal_entry_id UUID REFERENCES journal_entries(id) ON DELETE SET NULL;

-- Centralized sequences tracker
CREATE TABLE IF NOT EXISTS document_sequences (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    store_id UUID NOT NULL REFERENCES stores(id) ON DELETE RESTRICT,
    document_type VARCHAR(50) NOT NULL,
    prefix VARCHAR(20) NOT NULL,
    current_number INT NOT NULL DEFAULT 0,
    padding INT NOT NULL DEFAULT 6,
    suffix VARCHAR(20),
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL,
    UNIQUE (store_id, document_type)
);

-- ==============================================================================
-- PHASE 4.4: FIXED ASSETS, BUDGETS, YEARS, VALUATION LEDGER & TRANSFERS
-- ==============================================================================

CREATE TABLE IF NOT EXISTS fixed_assets (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    store_id UUID NOT NULL REFERENCES stores(id) ON DELETE RESTRICT,
    asset_code VARCHAR(50) UNIQUE NOT NULL,
    name VARCHAR(200) NOT NULL,
    description TEXT,
    purchase_date DATE NOT NULL,
    purchase_cost DECIMAL(18,4) NOT NULL,
    salvage_value DECIMAL(18,4) DEFAULT 0 NOT NULL,
    useful_life_years INT NOT NULL,
    depreciation_method VARCHAR(50) NOT NULL, -- STRAIGHT_LINE, WRITTEN_DOWN_VALUE
    depreciation_rate DECIMAL(5,2) DEFAULT 0 NOT NULL,
    asset_account_id UUID NOT NULL REFERENCES accounts(id) ON DELETE RESTRICT,
    accumulated_depr_account_id UUID NOT NULL REFERENCES accounts(id) ON DELETE RESTRICT,
    depreciation_expense_account_id UUID NOT NULL REFERENCES accounts(id) ON DELETE RESTRICT,
    current_book_value DECIMAL(18,4) NOT NULL,
    status VARCHAR(50) DEFAULT 'ACTIVE' NOT NULL, -- ACTIVE, DISPOSED, WRITTEN_OFF
    disposal_date DATE,
    disposal_value DECIMAL(18,4) DEFAULT 0 NOT NULL,
    disposal_gain_loss DECIMAL(18,4) DEFAULT 0 NOT NULL,
    journal_entry_id UUID REFERENCES journal_entries(id) ON DELETE SET NULL,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL
);

CREATE TABLE IF NOT EXISTS asset_depreciation_history (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    asset_id UUID NOT NULL REFERENCES fixed_assets(id) ON DELETE CASCADE,
    depreciation_date DATE NOT NULL,
    amount DECIMAL(18,4) NOT NULL,
    book_value_before DECIMAL(18,4) NOT NULL,
    book_value_after DECIMAL(18,4) NOT NULL,
    journal_entry_id UUID REFERENCES journal_entries(id) ON DELETE SET NULL,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL
);

CREATE TABLE IF NOT EXISTS cost_centers (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    store_id UUID NOT NULL REFERENCES stores(id) ON DELETE RESTRICT,
    code VARCHAR(50) UNIQUE NOT NULL,
    name VARCHAR(100) NOT NULL,
    description TEXT,
    is_active BOOLEAN DEFAULT TRUE NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL
);

CREATE TABLE IF NOT EXISTS budgets (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    store_id UUID NOT NULL REFERENCES stores(id) ON DELETE RESTRICT,
    cost_center_id UUID NOT NULL REFERENCES cost_centers(id) ON DELETE RESTRICT,
    gl_account_id UUID NOT NULL REFERENCES accounts(id) ON DELETE RESTRICT,
    financial_year VARCHAR(10) NOT NULL,
    period VARCHAR(20) NOT NULL, -- MONTHLY, QUARTERLY, ANNUAL
    period_start_date DATE NOT NULL,
    period_end_date DATE NOT NULL,
    budgeted_amount DECIMAL(18,4) NOT NULL,
    actual_amount DECIMAL(18,4) DEFAULT 0 NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL
);

CREATE TABLE IF NOT EXISTS financial_years (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name VARCHAR(50) UNIQUE NOT NULL, -- e.g. FY-2026-27
    start_date DATE NOT NULL,
    end_date DATE NOT NULL,
    status VARCHAR(20) DEFAULT 'ACTIVE' NOT NULL, -- ACTIVE, CLOSED
    closed_at TIMESTAMP WITH TIME ZONE,
    closed_by UUID REFERENCES users(id),
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL
);

CREATE TABLE IF NOT EXISTS financial_period_locks (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    store_id UUID NOT NULL REFERENCES stores(id) ON DELETE RESTRICT,
    period_name VARCHAR(100) NOT NULL, -- e.g. June 2026
    start_date DATE NOT NULL,
    end_date DATE NOT NULL,
    is_locked BOOLEAN DEFAULT FALSE NOT NULL,
    locked_by UUID REFERENCES users(id),
    locked_at TIMESTAMP WITH TIME ZONE,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL,
    UNIQUE (store_id, period_name)
);

-- Audit-proof inventory valuation tracking
CREATE TABLE IF NOT EXISTS inventory_valuation_history (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    store_id UUID NOT NULL REFERENCES stores(id) ON DELETE RESTRICT,
    business_date DATE NOT NULL,
    product_id UUID NOT NULL REFERENCES products(id) ON DELETE RESTRICT,
    batch_id UUID NOT NULL REFERENCES product_batches(id) ON DELETE RESTRICT,
    quantity DECIMAL(18,4) NOT NULL,
    unit_cost DECIMAL(18,4) NOT NULL,
    total_valuation DECIMAL(18,4) NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL,
    UNIQUE (store_id, business_date, batch_id)
);

-- Inter-store inventory movement transfers
CREATE TABLE IF NOT EXISTS inter_store_transfers (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    transfer_number VARCHAR(100) UNIQUE NOT NULL,
    from_store_id UUID NOT NULL REFERENCES stores(id) ON DELETE RESTRICT,
    to_store_id UUID NOT NULL REFERENCES stores(id) ON DELETE RESTRICT,
    transfer_date DATE NOT NULL,
    status VARCHAR(50) DEFAULT 'DRAFT' NOT NULL, -- DRAFT, SHIPPED, RECEIVED
    journal_entry_id UUID REFERENCES journal_entries(id) ON DELETE SET NULL,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL,
    created_by UUID
);

CREATE TABLE IF NOT EXISTS inter_store_transfer_items (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    transfer_id UUID NOT NULL REFERENCES inter_store_transfers(id) ON DELETE CASCADE,
    product_id UUID NOT NULL REFERENCES products(id) ON DELETE RESTRICT,
    batch_id UUID NOT NULL REFERENCES product_batches(id) ON DELETE RESTRICT,
    quantity DECIMAL(18,4) NOT NULL,
    unit_cost DECIMAL(18,4) NOT NULL
);

-- ==============================================================================
-- PHASE 4.5: RETURNS, GST PORTAL PLUGINS & WORKFLOW APPROVALS
-- ==============================================================================

-- Purchase Returns table
CREATE TABLE IF NOT EXISTS purchase_returns (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    store_id UUID NOT NULL REFERENCES stores(id) ON DELETE RESTRICT,
    supplier_id UUID NOT NULL REFERENCES suppliers(id) ON DELETE RESTRICT,
    grn_header_id UUID REFERENCES grn_headers(id) ON DELETE SET NULL,
    return_number VARCHAR(100) UNIQUE NOT NULL,
    return_date DATE NOT NULL,
    sub_total DECIMAL(18,4) NOT NULL,
    tax_amount DECIMAL(18,4) NOT NULL,
    total_amount DECIMAL(18,4) NOT NULL,
    status VARCHAR(50) DEFAULT 'DRAFT' NOT NULL, -- DRAFT, APPROVED, POSTED
    journal_entry_id UUID REFERENCES journal_entries(id) ON DELETE SET NULL,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL,
    created_by UUID
);

CREATE TABLE IF NOT EXISTS purchase_return_items (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    purchase_return_id UUID NOT NULL REFERENCES purchase_returns(id) ON DELETE CASCADE,
    product_id UUID NOT NULL REFERENCES products(id) ON DELETE RESTRICT,
    batch_id UUID NOT NULL REFERENCES product_batches(id) ON DELETE RESTRICT,
    quantity DECIMAL(18,4) NOT NULL,
    unit_cost DECIMAL(18,4) NOT NULL,
    tax_amount DECIMAL(18,4) NOT NULL,
    total_amount DECIMAL(18,4) NOT NULL
);

-- Sales Returns table
CREATE TABLE IF NOT EXISTS sales_returns (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    store_id UUID NOT NULL REFERENCES stores(id) ON DELETE RESTRICT,
    invoice_id UUID NOT NULL,
    business_date DATE NOT NULL,
    return_number VARCHAR(100) UNIQUE NOT NULL,
    return_date DATE NOT NULL,
    sub_total DECIMAL(18,4) NOT NULL,
    tax_amount DECIMAL(18,4) NOT NULL,
    total_amount DECIMAL(18,4) NOT NULL,
    refund_amount DECIMAL(18,4) NOT NULL,
    refund_mode VARCHAR(50) NOT NULL, -- CASH, UPI, CREDIT_NOTE
    status VARCHAR(50) DEFAULT 'COMPLETED' NOT NULL,
    journal_entry_id UUID REFERENCES journal_entries(id) ON DELETE SET NULL,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL,
    created_by UUID
);

CREATE TABLE IF NOT EXISTS sales_return_items (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    sales_return_id UUID NOT NULL REFERENCES sales_returns(id) ON DELETE CASCADE,
    product_id UUID NOT NULL REFERENCES products(id) ON DELETE RESTRICT,
    batch_id UUID NOT NULL REFERENCES product_batches(id) ON DELETE RESTRICT,
    quantity DECIMAL(18,4) NOT NULL,
    unit_price DECIMAL(18,4) NOT NULL,
    tax_amount DECIMAL(18,4) NOT NULL,
    total_amount DECIMAL(18,4) NOT NULL
);

-- E-Invoice Sync metadata
CREATE TABLE IF NOT EXISTS einvoice_metadata (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    invoice_id UUID NOT NULL,
    business_date DATE NOT NULL,
    irn VARCHAR(64) UNIQUE,
    ack_number VARCHAR(30),
    ack_date TIMESTAMP WITH TIME ZONE,
    qr_code_content TEXT,
    status VARCHAR(50) DEFAULT 'PENDING' NOT NULL, -- PENDING, GENERATED, CANCELLED, FAILED
    error_message TEXT,
    sync_attempts INT DEFAULT 0 NOT NULL,
    last_sync_at TIMESTAMP WITH TIME ZONE,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL,
    FOREIGN KEY (invoice_id, business_date) REFERENCES invoices(id, business_date) ON DELETE RESTRICT
);

-- E-Way Bill metadata
CREATE TABLE IF NOT EXISTS ewaybill_metadata (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    reference_type VARCHAR(50) NOT NULL, -- INVOICE, INTER_STORE_TRANSFER
    reference_id UUID NOT NULL,
    eway_bill_number VARCHAR(20) UNIQUE,
    issue_date TIMESTAMP WITH TIME ZONE,
    valid_until TIMESTAMP WITH TIME ZONE,
    vehicle_number VARCHAR(20),
    distance_km INT,
    status VARCHAR(50) DEFAULT 'ACTIVE' NOT NULL, -- ACTIVE, CANCELLED
    error_message TEXT,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL
);

-- Workflow Approvals Configuration & Requests
CREATE TABLE IF NOT EXISTS approval_limits (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    store_id UUID NOT NULL REFERENCES stores(id) ON DELETE RESTRICT,
    request_type VARCHAR(100) NOT NULL, -- SUPPLIER_PAYMENT, JOURNAL_ADJUSTMENT, ASSET_PURCHASE
    manager_limit DECIMAL(18,4) NOT NULL,
    owner_limit DECIMAL(18,4) NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL,
    UNIQUE (store_id, request_type)
);

-- Seed default approval thresholds
INSERT INTO approval_limits (store_id, request_type, manager_limit, owner_limit) VALUES 
('00000000-0000-0000-0000-000000000000', 'SUPPLIER_PAYMENT', 25000.00, 100000.00)
ON CONFLICT DO NOTHING;

CREATE TABLE IF NOT EXISTS approval_requests (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    store_id UUID NOT NULL REFERENCES stores(id) ON DELETE RESTRICT,
    request_type VARCHAR(100) NOT NULL,
    target_id UUID NOT NULL,
    amount DECIMAL(18,4) NOT NULL,
    requested_by UUID NOT NULL REFERENCES users(id),
    status VARCHAR(50) DEFAULT 'PENDING' NOT NULL, -- PENDING, APPROVED, REJECTED
    actioned_by UUID REFERENCES users(id),
    actioned_at TIMESTAMP WITH TIME ZONE,
    comments TEXT,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL
);

-- Alter journal_entries & lines to contain store references
ALTER TABLE journal_entries ADD COLUMN IF NOT EXISTS store_id UUID REFERENCES stores(id) ON DELETE RESTRICT;
ALTER TABLE journal_entry_lines ADD COLUMN IF NOT EXISTS store_id UUID REFERENCES stores(id) ON DELETE RESTRICT;
