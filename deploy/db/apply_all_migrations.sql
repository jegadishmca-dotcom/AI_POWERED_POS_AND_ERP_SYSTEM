-- ==============================================================================
-- APPLE SUPERMARKET ERP — MASTER IDEMPOTENT MIGRATION SCRIPT
-- Run this once against your live PostgreSQL database to bring it fully up-to-date.
-- All CREATE TABLE statements use IF NOT EXISTS - completely safe to re-run.
-- ==============================================================================

-- ── Phase 2: Product Search Trigger ──────────────────────────────────────────
DO $$ BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'tsvector') THEN NULL; END IF;
END $$;

-- Add search_vector column to products if missing
ALTER TABLE products ADD COLUMN IF NOT EXISTS search_vector tsvector;

-- Update search_vector from existing data
UPDATE products
SET search_vector = to_tsvector('simple', coalesce(name,'') || ' ' || coalesce(product_code,''));

-- Create GIN index for fast full-text search
CREATE INDEX IF NOT EXISTS idx_products_search_vector ON products USING GIN(search_vector);

-- Create trigger function
CREATE OR REPLACE FUNCTION products_search_vector_update() RETURNS trigger AS $$
BEGIN
  NEW.search_vector := to_tsvector('simple',
    coalesce(NEW.name,'') || ' ' ||
    coalesce(NEW.product_code,'') || ' ' ||
    coalesce(NEW.tamil_name,''));
  RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Create trigger (drop first to re-create safely)
DROP TRIGGER IF EXISTS products_search_vector_trigger ON products;
CREATE TRIGGER products_search_vector_trigger
BEFORE INSERT OR UPDATE ON products
FOR EACH ROW EXECUTE FUNCTION products_search_vector_update();

-- ── Phase 2: Inventory Schema ─────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS product_batches (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    store_id UUID,
    product_id UUID NOT NULL REFERENCES products(id),
    batch_number VARCHAR(100) NOT NULL,
    mfg_date DATE,
    expiry_date DATE,
    mrp DECIMAL(18,4) NOT NULL,
    cost_price DECIMAL(18,4) NOT NULL,
    is_active BOOLEAN DEFAULT TRUE,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);
CREATE INDEX IF NOT EXISTS idx_batches_product_expiry ON product_batches(product_id, expiry_date);

CREATE TABLE IF NOT EXISTS stock_ledger (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    store_id UUID,
    warehouse_id UUID,
    terminal_id UUID,
    business_date DATE NOT NULL,
    product_id UUID NOT NULL REFERENCES products(id),
    batch_id UUID REFERENCES product_batches(id),
    movement_type VARCHAR(50) NOT NULL,
    quantity DECIMAL(18,4) NOT NULL,
    unit_cost DECIMAL(18,4) NOT NULL,
    expiry_date DATE,
    reference_document_id UUID NOT NULL,
    reference_number VARCHAR(100) NOT NULL,
    running_balance DECIMAL(18,4) NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    created_by UUID
);
CREATE INDEX IF NOT EXISTS idx_stock_ledger_product ON stock_ledger(product_id);
CREATE INDEX IF NOT EXISTS idx_stock_ledger_store_product ON stock_ledger(store_id, product_id, created_at DESC);
CREATE INDEX IF NOT EXISTS idx_stock_ledger_ref ON stock_ledger(reference_document_id);

CREATE TABLE IF NOT EXISTS stock_adjustments (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    store_id UUID,
    adjustment_number VARCHAR(100) UNIQUE NOT NULL,
    reason VARCHAR(100) NOT NULL,
    status VARCHAR(50) DEFAULT 'PENDING',
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    approved_by UUID
);
CREATE TABLE IF NOT EXISTS stock_adjustment_items (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    stock_adjustment_id UUID NOT NULL REFERENCES stock_adjustments(id) ON DELETE CASCADE,
    product_id UUID NOT NULL REFERENCES products(id),
    batch_id UUID REFERENCES product_batches(id),
    adjusted_quantity DECIMAL(18,4) NOT NULL,
    unit_cost DECIMAL(18,4) NOT NULL
);

-- ── Phase 2: Purchasing Schema ────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS purchase_order_headers (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    store_id UUID,
    supplier_id UUID NOT NULL,
    po_number VARCHAR(100) UNIQUE NOT NULL,
    po_date DATE NOT NULL,
    expected_delivery_date DATE NOT NULL,
    total_amount DECIMAL(18,4) NOT NULL,
    status VARCHAR(50) DEFAULT 'DRAFT',
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    created_by UUID
);
CREATE TABLE IF NOT EXISTS purchase_order_items (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    purchase_order_header_id UUID NOT NULL REFERENCES purchase_order_headers(id) ON DELETE CASCADE,
    product_id UUID NOT NULL REFERENCES products(id),
    ordered_quantity DECIMAL(18,4) NOT NULL,
    received_quantity DECIMAL(18,4) NOT NULL DEFAULT 0,
    unit_cost DECIMAL(18,4) NOT NULL,
    total_cost DECIMAL(18,4) NOT NULL
);
CREATE TABLE IF NOT EXISTS grn_headers (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    store_id UUID,
    purchase_order_header_id UUID REFERENCES purchase_order_headers(id),
    supplier_id UUID NOT NULL,
    grn_number VARCHAR(100) UNIQUE NOT NULL,
    supplier_invoice_number VARCHAR(100),
    received_date DATE NOT NULL,
    total_amount DECIMAL(18,4) NOT NULL,
    status VARCHAR(50) DEFAULT 'DRAFT',
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    created_by UUID
);
CREATE TABLE IF NOT EXISTS grn_items (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    grn_header_id UUID NOT NULL REFERENCES grn_headers(id) ON DELETE CASCADE,
    purchase_order_item_id UUID NOT NULL REFERENCES purchase_order_items(id),
    product_id UUID NOT NULL REFERENCES products(id),
    batch_id UUID REFERENCES product_batches(id),
    batch_number VARCHAR(100),
    expiry_date DATE,
    mfg_date DATE,
    received_quantity DECIMAL(18,4) NOT NULL,
    accepted_quantity DECIMAL(18,4) NOT NULL,
    rejected_quantity DECIMAL(18,4) NOT NULL,
    unit_cost DECIMAL(18,4) NOT NULL,
    total_cost DECIMAL(18,4) NOT NULL
);
CREATE TABLE IF NOT EXISTS purchase_bill_headers (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    store_id UUID,
    supplier_id UUID NOT NULL,
    grn_header_id UUID REFERENCES grn_headers(id),
    bill_number VARCHAR(100) UNIQUE NOT NULL,
    bill_date DATE NOT NULL,
    sub_total DECIMAL(18,4) NOT NULL,
    tax_amount DECIMAL(18,4) NOT NULL,
    total_amount DECIMAL(18,4) NOT NULL,
    status VARCHAR(50) DEFAULT 'PENDING_PAYMENT',
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    created_by UUID
);
CREATE TABLE IF NOT EXISTS purchase_bill_items (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    purchase_bill_header_id UUID NOT NULL REFERENCES purchase_bill_headers(id) ON DELETE CASCADE,
    product_id UUID NOT NULL REFERENCES products(id),
    quantity DECIMAL(18,4) NOT NULL,
    unit_cost DECIMAL(18,4) NOT NULL,
    tax_amount DECIMAL(18,4) NOT NULL,
    total_amount DECIMAL(18,4) NOT NULL
);

-- ── Phase 2: Materialized Stock View ─────────────────────────────────────────
CREATE MATERIALIZED VIEW IF NOT EXISTS mv_current_stock AS
SELECT DISTINCT ON (store_id, product_id)
    id as latest_ledger_id,
    store_id,
    product_id,
    batch_id,
    running_balance as current_stock,
    unit_cost as last_unit_cost,
    created_at as last_movement_date
FROM stock_ledger
ORDER BY store_id, product_id, created_at DESC;
CREATE UNIQUE INDEX IF NOT EXISTS idx_mv_current_stock_unique ON mv_current_stock (store_id, product_id);

-- ── Phase 2: Warehouse & Stock Take ──────────────────────────────────────────
CREATE TABLE IF NOT EXISTS suppliers (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    name VARCHAR(200) NOT NULL,
    gstin VARCHAR(15),
    phone VARCHAR(20),
    payment_terms VARCHAR(50) DEFAULT 'NET30',
    is_active BOOLEAN DEFAULT TRUE,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);
CREATE TABLE IF NOT EXISTS warehouses (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    store_id UUID,
    name VARCHAR(100) NOT NULL,
    code VARCHAR(50) UNIQUE NOT NULL,
    is_active BOOLEAN DEFAULT TRUE
);
CREATE TABLE IF NOT EXISTS bins (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    warehouse_id UUID NOT NULL REFERENCES warehouses(id) ON DELETE CASCADE,
    code VARCHAR(50) NOT NULL,
    description VARCHAR(200),
    is_active BOOLEAN DEFAULT TRUE,
    UNIQUE(warehouse_id, code)
);
CREATE TABLE IF NOT EXISTS stock_take_headers (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    store_id UUID,
    take_number VARCHAR(100) UNIQUE NOT NULL,
    scheduled_date DATE NOT NULL,
    status VARCHAR(50) DEFAULT 'DRAFT',
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    approved_by UUID
);
CREATE TABLE IF NOT EXISTS stock_take_items (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    stock_take_header_id UUID NOT NULL REFERENCES stock_take_headers(id) ON DELETE CASCADE,
    product_id UUID NOT NULL REFERENCES products(id),
    batch_id UUID REFERENCES product_batches(id),
    system_quantity DECIMAL(18,4) NOT NULL,
    physical_quantity DECIMAL(18,4) NOT NULL
);

-- ── Phase 3: CRM, Loyalty, Wallet & Offers ───────────────────────────────────
CREATE TABLE IF NOT EXISTS customer_tiers (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    name VARCHAR(50) NOT NULL UNIQUE,
    level INT NOT NULL,
    minimum_spend DECIMAL(18,4) NOT NULL DEFAULT 0,
    points_earn_multiplier DECIMAL(18,4) NOT NULL DEFAULT 1.0
);

CREATE TABLE IF NOT EXISTS customers (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    phone VARCHAR(20) UNIQUE NOT NULL,
    name VARCHAR(200) NOT NULL,
    tamil_name VARCHAR(200),
    dob DATE,
    anniversary DATE,
    marketing_consent BOOLEAN DEFAULT FALSE,
    analytics_consent BOOLEAN DEFAULT FALSE,
    consent_recorded_at TIMESTAMP WITH TIME ZONE,
    customer_tier_id UUID REFERENCES customer_tiers(id),
    membership_card_number VARCHAR(100) UNIQUE,
    running_wallet_balance DECIMAL(18,4) DEFAULT 0,
    running_loyalty_points DECIMAL(18,4) DEFAULT 0,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);
CREATE INDEX IF NOT EXISTS idx_customers_phone ON customers(phone);

-- Phase 3: Add customer_id reference to invoices partitioned table
ALTER TABLE invoices ADD COLUMN IF NOT EXISTS customer_id UUID REFERENCES customers(id);

CREATE TABLE IF NOT EXISTS wallet_ledger (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    customer_id UUID NOT NULL REFERENCES customers(id) ON DELETE CASCADE,
    store_id UUID,
    transaction_type VARCHAR(50) NOT NULL,
    amount DECIMAL(18,4) NOT NULL,
    reference_document VARCHAR(100) NOT NULL,
    running_balance DECIMAL(18,4) NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    created_by UUID
);
CREATE INDEX IF NOT EXISTS idx_wallet_ledger_customer ON wallet_ledger(customer_id, created_at DESC);

CREATE TABLE IF NOT EXISTS loyalty_ledger (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    customer_id UUID NOT NULL REFERENCES customers(id) ON DELETE CASCADE,
    store_id UUID,
    transaction_type VARCHAR(50) NOT NULL,
    points DECIMAL(18,4) NOT NULL,
    reference_document VARCHAR(100) NOT NULL,
    expiry_date DATE,
    running_points DECIMAL(18,4) NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    created_by UUID
);
CREATE INDEX IF NOT EXISTS idx_loyalty_ledger_customer ON loyalty_ledger(customer_id, created_at DESC);

CREATE TABLE IF NOT EXISTS offers (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    name VARCHAR(200) NOT NULL,
    description TEXT,
    offer_type VARCHAR(50) NOT NULL,
    rules_json JSONB NOT NULL DEFAULT '{}',
    priority INT DEFAULT 0,
    is_stackable BOOLEAN DEFAULT FALSE,
    start_date TIMESTAMP WITH TIME ZONE NOT NULL,
    end_date TIMESTAMP WITH TIME ZONE NOT NULL,
    is_active BOOLEAN DEFAULT TRUE,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);
CREATE INDEX IF NOT EXISTS idx_offers_active ON offers(start_date, end_date) WHERE is_active = TRUE;

-- Phase 3: Offer Enhancements (add columns only if not already added)
ALTER TABLE offers ADD COLUMN IF NOT EXISTS promo_code VARCHAR(50) UNIQUE;
ALTER TABLE offers ADD COLUMN IF NOT EXISTS is_exclusive BOOLEAN DEFAULT FALSE;
ALTER TABLE offers ADD COLUMN IF NOT EXISTS max_usage_per_invoice INT;
CREATE INDEX IF NOT EXISTS idx_offers_promocode ON offers(promo_code);

-- Phase 3: Seed Default Customer Tiers
INSERT INTO customer_tiers (id, name, level, minimum_spend, points_earn_multiplier) VALUES
('00000000-0000-0000-0000-000000000011', 'Silver', 1, 0, 1.0),
('00000000-0000-0000-0000-000000000012', 'Gold', 2, 5000, 1.2),
('00000000-0000-0000-0000-000000000013', 'Platinum', 3, 15000, 1.5)
ON CONFLICT (name) DO NOTHING;

-- Phase 3: Seed Default Offers
INSERT INTO offers (name, description, offer_type, promo_code, priority, is_stackable, is_exclusive, start_date, end_date, rules_json)
SELECT '10% Off Grocery Bill', 'Get 10% off on all grocery items for bills above 1000', 'PERCENTAGE',
    NULL, 1, TRUE, FALSE, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP + INTERVAL '365 days',
    '{"Conditions": {"MinCartValue": 1000}, "Reward": {"DiscountType": "Percentage", "Value": 10, "ApplyTo": "BILL", "MaxDiscountAmount": 500}}'
WHERE NOT EXISTS (SELECT 1 FROM offers WHERE name = '10% Off Grocery Bill');

INSERT INTO offers (name, description, offer_type, promo_code, priority, is_stackable, is_exclusive, start_date, end_date, rules_json)
SELECT 'FESTIVAL500 - Flat 500 Off', 'Exclusive flat 500 off on total bill. Cannot be combined with other offers.', 'FLAT',
    'FESTIVAL500', 10, FALSE, TRUE, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP + INTERVAL '365 days',
    '{"Conditions": {"MinCartValue": 2000}, "Reward": {"DiscountType": "FlatAmount", "Value": 500, "ApplyTo": "BILL"}}'
WHERE NOT EXISTS (SELECT 1 FROM offers WHERE promo_code = 'FESTIVAL500');

-- ── Phase 4: Accounting & GST ─────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS accounts (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    account_code VARCHAR(20) UNIQUE NOT NULL,
    name VARCHAR(200) NOT NULL,
    account_type VARCHAR(50) NOT NULL,
    parent_account_id UUID REFERENCES accounts(id),
    is_active BOOLEAN DEFAULT TRUE,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);
CREATE TABLE IF NOT EXISTS journal_entries (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    store_id UUID,
    entry_number VARCHAR(100) UNIQUE NOT NULL,
    entry_date DATE NOT NULL,
    description TEXT,
    reference_document VARCHAR(100),
    is_posted BOOLEAN DEFAULT FALSE,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    created_by UUID
);
CREATE TABLE IF NOT EXISTS journal_entry_lines (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    journal_entry_id UUID NOT NULL REFERENCES journal_entries(id) ON DELETE CASCADE,
    account_id UUID NOT NULL REFERENCES accounts(id),
    description TEXT,
    debit_amount DECIMAL(18,4) DEFAULT 0,
    credit_amount DECIMAL(18,4) DEFAULT 0
);
CREATE TABLE IF NOT EXISTS tax_transactions (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    store_id UUID,
    transaction_type VARCHAR(50) NOT NULL,
    document_number VARCHAR(100) NOT NULL,
    transaction_date DATE NOT NULL,
    taxable_amount DECIMAL(18,4) NOT NULL,
    cgst_amount DECIMAL(18,4) DEFAULT 0,
    sgst_amount DECIMAL(18,4) DEFAULT 0,
    igst_amount DECIMAL(18,4) DEFAULT 0,
    cess_amount DECIMAL(18,4) DEFAULT 0,
    gstin VARCHAR(15),
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- Phase 4: Seed Chart of Accounts
INSERT INTO accounts (account_code, name, account_type) VALUES
('1000', 'Cash on Hand', 'ASSET'),
('1100', 'Bank Account', 'ASSET'),
('2000', 'Accounts Payable', 'LIABILITY'),
('2100', 'Customer Wallet Deposits', 'LIABILITY'),
('2200', 'CGST Payable', 'LIABILITY'),
('2201', 'SGST Payable', 'LIABILITY'),
('4000', 'Sales Revenue', 'REVENUE'),
('5000', 'Cost of Goods Sold', 'EXPENSE')
ON CONFLICT (account_code) DO NOTHING;

-- Phase 4: E-Invoicing columns on invoices table
ALTER TABLE invoices ADD COLUMN IF NOT EXISTS irn VARCHAR(64) UNIQUE;
ALTER TABLE invoices ADD COLUMN IF NOT EXISTS ack_no VARCHAR(50);
ALTER TABLE invoices ADD COLUMN IF NOT EXISTS ack_date TIMESTAMP WITH TIME ZONE;
ALTER TABLE invoices ADD COLUMN IF NOT EXISTS qr_code_url TEXT;
ALTER TABLE invoices ADD COLUMN IF NOT EXISTS eway_bill_no VARCHAR(50);

-- ── Phase 5: Analytics Materialized Views ────────────────────────────────────
CREATE MATERIALIZED VIEW IF NOT EXISTS mv_daily_sales_summary AS
SELECT
    terminal_id,
    business_date,
    COUNT(id) as total_invoices,
    SUM(sub_total) as gross_sales,
    SUM(total_discount) as total_discounts,
    SUM(tax_total) as total_tax,
    SUM(total_amount) as net_sales
FROM invoices
WHERE status = 'COMPLETED'
GROUP BY terminal_id, business_date;
CREATE UNIQUE INDEX IF NOT EXISTS idx_mv_daily_sales_summary ON mv_daily_sales_summary(terminal_id, business_date);

CREATE MATERIALIZED VIEW IF NOT EXISTS mv_product_sales_stats AS
SELECT
    i.business_date,
    ii.product_id,
    SUM(ii.quantity) as total_quantity_sold,
    SUM(ii.final_total) as total_revenue
FROM invoices i
JOIN invoice_items ii ON i.id = ii.invoice_id
WHERE i.status = 'COMPLETED'
GROUP BY i.business_date, ii.product_id;
CREATE UNIQUE INDEX IF NOT EXISTS idx_mv_product_sales_stats ON mv_product_sales_stats(business_date, product_id);

-- ── Phase 6: Audit Logs ───────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS audit_logs (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    user_id UUID,
    action VARCHAR(100) NOT NULL,
    entity_name VARCHAR(100),
    entity_id VARCHAR(100),
    old_values JSONB,
    new_values JSONB,
    ip_address VARCHAR(45),
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);
CREATE INDEX IF NOT EXISTS idx_audit_logs_action ON audit_logs(action);
CREATE INDEX IF NOT EXISTS idx_audit_logs_created_at ON audit_logs(created_at);

-- ── Migration History Tracking ────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS migration_history (
    migration_name VARCHAR(255) PRIMARY KEY,
    applied_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

INSERT INTO migration_history (migration_name) VALUES
('02_ProductSearchTrigger.sql'),
('03_InventorySchema.sql'),
('04_PurchasingSchema.sql'),
('05_StockViewsSchema.sql'),
('06_FinalInventorySchema.sql'),
('07_CrmAndOffersSchema.sql'),
('08_OfferEnhancementsSchema.sql'),
('09_SeedOffers.sql'),
('10_AccountingSchema.sql'),
('11_EInvoiceAndReportsSchema.sql'),
('12_SeedChartOfAccounts.sql'),
('13_AnalyticsViewsSchema.sql'),
('14_AuditLogsSchema.sql')
ON CONFLICT (migration_name) DO NOTHING;

-- Done!
SELECT 'All migrations applied successfully!' as result;
