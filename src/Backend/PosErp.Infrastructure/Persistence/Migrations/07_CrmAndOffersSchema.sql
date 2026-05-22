-- ==============================================================================
-- PHASE 3: CRM, LOYALTY, WALLET & OFFERS
-- ==============================================================================

CREATE TABLE IF NOT EXISTS customer_tiers (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name VARCHAR(50) NOT NULL UNIQUE,
    level INT NOT NULL,
    minimum_spend DECIMAL(18,4) NOT NULL DEFAULT 0,
    points_earn_multiplier DECIMAL(18,4) NOT NULL DEFAULT 1.0
);

CREATE TABLE IF NOT EXISTS customers (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
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

CREATE TABLE IF NOT EXISTS wallet_ledger (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    customer_id UUID NOT NULL REFERENCES customers(id) ON DELETE CASCADE,
    store_id UUID,
    transaction_type VARCHAR(50) NOT NULL, -- TOPUP, SPEND, REFUND
    amount DECIMAL(18,4) NOT NULL,
    reference_document VARCHAR(100) NOT NULL,
    running_balance DECIMAL(18,4) NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    created_by UUID
);

CREATE INDEX IF NOT EXISTS idx_wallet_ledger_customer ON wallet_ledger(customer_id, created_at DESC);

CREATE TABLE IF NOT EXISTS loyalty_ledger (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    customer_id UUID NOT NULL REFERENCES customers(id) ON DELETE CASCADE,
    store_id UUID,
    transaction_type VARCHAR(50) NOT NULL, -- EARN, BURN, EXPIRED
    points DECIMAL(18,4) NOT NULL,
    reference_document VARCHAR(100) NOT NULL,
    expiry_date DATE,
    running_points DECIMAL(18,4) NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    created_by UUID
);

CREATE INDEX IF NOT EXISTS idx_loyalty_ledger_customer ON loyalty_ledger(customer_id, created_at DESC);

CREATE TABLE IF NOT EXISTS offers (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
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
