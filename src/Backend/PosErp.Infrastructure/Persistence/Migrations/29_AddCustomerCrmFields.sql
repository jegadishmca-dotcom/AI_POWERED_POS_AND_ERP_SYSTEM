-- Add missing CRM fields to customers table
ALTER TABLE customers ADD COLUMN IF NOT EXISTS email VARCHAR(255);
ALTER TABLE customers ADD COLUMN IF NOT EXISTS address TEXT;

-- Loyalty and Segmentation
ALTER TABLE customers ADD COLUMN IF NOT EXISTS lifetime_points_earned DECIMAL(18,4) DEFAULT 0 NOT NULL;
ALTER TABLE customers ADD COLUMN IF NOT EXISTS lifetime_spend DECIMAL(18,4) DEFAULT 0 NOT NULL;
ALTER TABLE customers ADD COLUMN IF NOT EXISTS last_purchase_date TIMESTAMP WITH TIME ZONE;

ALTER TABLE customers ADD COLUMN IF NOT EXISTS preferred_category VARCHAR(100);
ALTER TABLE customers ADD COLUMN IF NOT EXISTS average_basket_value DECIMAL(18,4) DEFAULT 0 NOT NULL;
ALTER TABLE customers ADD COLUMN IF NOT EXISTS visit_frequency INT DEFAULT 0 NOT NULL;
ALTER TABLE customers ADD COLUMN IF NOT EXISTS customer_segment VARCHAR(50) DEFAULT 'New Customer' NOT NULL;

-- Stage 2 Additions
ALTER TABLE customers ADD COLUMN IF NOT EXISTS enrollment_date TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL;
ALTER TABLE customers ADD COLUMN IF NOT EXISTS membership_status VARCHAR(50) DEFAULT 'Active' NOT NULL;

-- Add missing fields to customer_tiers
ALTER TABLE customer_tiers ADD COLUMN IF NOT EXISTS minimum_points DECIMAL(18,4) DEFAULT 0 NOT NULL;
ALTER TABLE customer_tiers ADD COLUMN IF NOT EXISTS tier_upgrade_rule VARCHAR(50) DEFAULT 'Spend' NOT NULL;
ALTER TABLE customer_tiers ADD COLUMN IF NOT EXISTS tier_downgrade_rule VARCHAR(50) DEFAULT 'Inactivity' NOT NULL;
ALTER TABLE customer_tiers ADD COLUMN IF NOT EXISTS benefits_json TEXT DEFAULT '{}' NOT NULL;
