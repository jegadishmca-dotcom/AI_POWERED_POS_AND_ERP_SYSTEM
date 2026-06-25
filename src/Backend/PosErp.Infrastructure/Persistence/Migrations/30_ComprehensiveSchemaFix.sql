-- 30_ComprehensiveSchemaFix.sql
-- Fixes missing columns found between EF Core models and Postgres Schema

-- Missing CRM Fields
ALTER TABLE customers ADD COLUMN IF NOT EXISTS last_points_earned_date TIMESTAMP WITH TIME ZONE;
ALTER TABLE customers ADD COLUMN IF NOT EXISTS last_redemption_date TIMESTAMP WITH TIME ZONE;

-- Missing Audit Fields
ALTER TABLE audit_logs ADD COLUMN IF NOT EXISTS tenant_id UUID NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
ALTER TABLE audit_logs ADD COLUMN IF NOT EXISTS user_agent TEXT;

-- Missing Store Fields
ALTER TABLE stores ADD COLUMN IF NOT EXISTS tenant_id UUID NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';

-- Missing Offer Fields
ALTER TABLE offers ADD COLUMN IF NOT EXISTS activated_by UUID;
ALTER TABLE offers ADD COLUMN IF NOT EXISTS deactivated_by UUID;
ALTER TABLE offers ADD COLUMN IF NOT EXISTS created_by UUID;
ALTER TABLE offers ADD COLUMN IF NOT EXISTS updated_by UUID;
ALTER TABLE offers ADD COLUMN IF NOT EXISTS updated_at TIMESTAMP WITH TIME ZONE;
ALTER TABLE offers ADD COLUMN IF NOT EXISTS store_id UUID;

-- Missing Loyalty Config
CREATE TABLE IF NOT EXISTS loyalty_program_configs (
    id UUID PRIMARY KEY,
    is_active_config BOOLEAN NOT NULL DEFAULT TRUE,
    earn_ratio_spend_amount NUMERIC NOT NULL DEFAULT 100,
    earn_ratio_points NUMERIC NOT NULL DEFAULT 1,
    redeem_ratio_points NUMERIC NOT NULL DEFAULT 100,
    redeem_ratio_discount_amount NUMERIC NOT NULL DEFAULT 10,
    max_redemption_percentage_per_invoice NUMERIC NOT NULL DEFAULT 20,
    max_redemption_per_day NUMERIC NOT NULL DEFAULT 1000,
    max_manual_adjustment_per_day NUMERIC NOT NULL DEFAULT 500,
    max_bonus_allocation_per_customer NUMERIC NOT NULL DEFAULT 2000,
    enable_auto_tier_evaluation BOOLEAN NOT NULL DEFAULT TRUE,
    enable_point_expiry BOOLEAN NOT NULL DEFAULT TRUE,
    expiry_months INTEGER NOT NULL DEFAULT 12,
    birthday_bonus_points NUMERIC NOT NULL DEFAULT 50,
    anniversary_bonus_points NUMERIC NOT NULL DEFAULT 100,
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_by UUID
);

DO $$ 
BEGIN
    IF EXISTS (
        SELECT 1 
        FROM information_schema.columns 
        WHERE table_name='ai_alerts' AND column_name='severity'
    ) THEN
        ALTER TABLE ai_alerts RENAME COLUMN severity TO alert_severity;
    END IF;
END $$;