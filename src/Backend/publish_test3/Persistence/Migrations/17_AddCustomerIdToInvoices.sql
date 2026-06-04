-- =====================================================================
-- Enterprise Supermarket POS & ERP System - Add CustomerId to Invoices
-- =====================================================================

ALTER TABLE invoices ADD COLUMN IF NOT EXISTS customer_id UUID;
ALTER TABLE invoices ADD COLUMN IF NOT EXISTS cash_amount DECIMAL(18,4) NOT NULL DEFAULT 0;
ALTER TABLE invoices ADD COLUMN IF NOT EXISTS upi_amount DECIMAL(18,4) NOT NULL DEFAULT 0;
ALTER TABLE invoices ADD COLUMN IF NOT EXISTS card_amount DECIMAL(18,4) NOT NULL DEFAULT 0;
ALTER TABLE invoices ADD COLUMN IF NOT EXISTS wallet_amount DECIMAL(18,4) NOT NULL DEFAULT 0;
