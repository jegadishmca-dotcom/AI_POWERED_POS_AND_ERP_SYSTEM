-- 1. Add credit_limit to customers table
ALTER TABLE customers ADD COLUMN IF NOT EXISTS credit_limit DECIMAL(18,4) DEFAULT 0 NOT NULL;

-- 2. Add due_date to purchase_bill_headers table
ALTER TABLE purchase_bill_headers ADD COLUMN IF NOT EXISTS due_date DATE;

-- 3. Add due_date to invoices table
ALTER TABLE invoices ADD COLUMN IF NOT EXISTS due_date DATE;

-- 4. Supplier Rebate & Promotional Funding Placeholder Table
CREATE TABLE IF NOT EXISTS supplier_rebates (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    store_id UUID NOT NULL REFERENCES stores(id) ON DELETE RESTRICT,
    supplier_id UUID NOT NULL REFERENCES suppliers(id) ON DELETE RESTRICT,
    rebate_program_name VARCHAR(200) NOT NULL,
    percentage DECIMAL(5,2),
    fixed_amount DECIMAL(18,4) DEFAULT 0,
    earned_amount DECIMAL(18,4) DEFAULT 0 NOT NULL,
    status VARCHAR(50) DEFAULT 'ACTIVE' NOT NULL, -- ACTIVE, CLAIMED, EXPIRED
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL
);
