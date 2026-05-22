-- ==============================================================================
-- PHASE 4: ACCOUNTING & GST SCHEMA
-- ==============================================================================

CREATE TABLE accounts (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    account_code VARCHAR(20) UNIQUE NOT NULL,
    name VARCHAR(200) NOT NULL,
    account_type VARCHAR(50) NOT NULL,
    parent_account_id UUID REFERENCES accounts(id),
    is_active BOOLEAN DEFAULT TRUE,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE journal_entries (
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

CREATE TABLE journal_entry_lines (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    journal_entry_id UUID NOT NULL REFERENCES journal_entries(id) ON DELETE CASCADE,
    account_id UUID NOT NULL REFERENCES accounts(id),
    description TEXT,
    debit_amount DECIMAL(18,4) DEFAULT 0,
    credit_amount DECIMAL(18,4) DEFAULT 0
);

CREATE TABLE tax_transactions (
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

-- Basic Seed for COA
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
