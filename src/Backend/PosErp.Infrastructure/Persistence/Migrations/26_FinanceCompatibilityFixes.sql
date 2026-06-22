-- Add missing cost_center_id to journal_entry_lines
ALTER TABLE journal_entry_lines ADD COLUMN IF NOT EXISTS cost_center_id UUID REFERENCES cost_centers(id) ON DELETE SET NULL;

-- Enforce Foreign Key integrity for store_id and supplier_id columns
DO $$
BEGIN
    -- journal_entries -> stores
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.table_constraints 
        WHERE constraint_name = 'fk_journal_entries_store' AND table_name = 'journal_entries'
    ) THEN
        ALTER TABLE journal_entries ADD CONSTRAINT fk_journal_entries_store FOREIGN KEY (store_id) REFERENCES stores(id) ON DELETE RESTRICT;
    END IF;

    -- journal_entry_lines -> stores
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.table_constraints 
        WHERE constraint_name = 'fk_journal_entry_lines_store' AND table_name = 'journal_entry_lines'
    ) THEN
        ALTER TABLE journal_entry_lines ADD CONSTRAINT fk_journal_entry_lines_store FOREIGN KEY (store_id) REFERENCES stores(id) ON DELETE RESTRICT;
    END IF;

    -- products -> stores
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.table_constraints 
        WHERE constraint_name = 'fk_products_store' AND table_name = 'products'
    ) THEN
        ALTER TABLE products ADD CONSTRAINT fk_products_store FOREIGN KEY (store_id) REFERENCES stores(id) ON DELETE RESTRICT;
    END IF;

    -- product_batches -> stores
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.table_constraints 
        WHERE constraint_name = 'fk_product_batches_store' AND table_name = 'product_batches'
    ) THEN
        ALTER TABLE product_batches ADD CONSTRAINT fk_product_batches_store FOREIGN KEY (store_id) REFERENCES stores(id) ON DELETE RESTRICT;
    END IF;

    -- invoices -> stores
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.table_constraints 
        WHERE constraint_name = 'fk_invoices_store' AND table_name = 'invoices'
    ) THEN
        ALTER TABLE invoices ADD CONSTRAINT fk_invoices_store FOREIGN KEY (store_id) REFERENCES stores(id) ON DELETE RESTRICT;
    END IF;

    -- grn_headers -> suppliers
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.table_constraints 
        WHERE constraint_name = 'fk_grn_headers_supplier' AND table_name = 'grn_headers'
    ) THEN
        ALTER TABLE grn_headers ADD CONSTRAINT fk_grn_headers_supplier FOREIGN KEY (supplier_id) REFERENCES suppliers(id) ON DELETE RESTRICT;
    END IF;

    -- grn_headers -> stores
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.table_constraints 
        WHERE constraint_name = 'fk_grn_headers_store' AND table_name = 'grn_headers'
    ) THEN
        ALTER TABLE grn_headers ADD CONSTRAINT fk_grn_headers_store FOREIGN KEY (store_id) REFERENCES stores(id) ON DELETE RESTRICT;
    END IF;
END $$;

-- Add journal source tracking columns
ALTER TABLE journal_entries ADD COLUMN IF NOT EXISTS source_module VARCHAR(50);
ALTER TABLE journal_entries ADD COLUMN IF NOT EXISTS source_document_type VARCHAR(50);
ALTER TABLE journal_entries ADD COLUMN IF NOT EXISTS source_document_id UUID;
ALTER TABLE journal_entries ADD COLUMN IF NOT EXISTS status VARCHAR(50) DEFAULT 'DRAFT' NOT NULL;

-- Create Daily Finance Summary Table for performance optimization
CREATE TABLE IF NOT EXISTS daily_finance_summary (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    store_id UUID NOT NULL REFERENCES stores(id) ON DELETE RESTRICT,
    business_date DATE NOT NULL,
    total_sales DECIMAL(18,4) DEFAULT 0 NOT NULL,
    total_purchases DECIMAL(18,4) DEFAULT 0 NOT NULL,
    total_payments DECIMAL(18,4) DEFAULT 0 NOT NULL,
    total_receipts DECIMAL(18,4) DEFAULT 0 NOT NULL,
    total_expenses DECIMAL(18,4) DEFAULT 0 NOT NULL,
    net_cash_flow DECIMAL(18,4) DEFAULT 0 NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL,
    UNIQUE (store_id, business_date)
);

-- Create Approval Request Steps Table for multi-level approval workflows
CREATE TABLE IF NOT EXISTS approval_request_steps (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    approval_request_id UUID NOT NULL REFERENCES approval_requests(id) ON DELETE CASCADE,
    level INT NOT NULL,
    role_name VARCHAR(50) NOT NULL,
    status VARCHAR(50) DEFAULT 'PENDING' NOT NULL, -- PENDING, APPROVED, REJECTED
    actioned_by UUID REFERENCES users(id),
    actioned_at TIMESTAMP WITH TIME ZONE,
    comments TEXT,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL,
    UNIQUE (approval_request_id, level)
);
