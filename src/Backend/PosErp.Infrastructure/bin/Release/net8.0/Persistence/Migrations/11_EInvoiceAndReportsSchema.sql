-- ==============================================================================
-- PHASE 4: E-INVOICING AND REPORTS
-- ==============================================================================
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='invoices' AND column_name='irn') THEN
        ALTER TABLE invoices ADD COLUMN irn VARCHAR(64) UNIQUE;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='invoices' AND column_name='ack_no') THEN
        ALTER TABLE invoices ADD COLUMN ack_no VARCHAR(50);
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='invoices' AND column_name='ack_date') THEN
        ALTER TABLE invoices ADD COLUMN ack_date TIMESTAMP WITH TIME ZONE;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='invoices' AND column_name='qr_code_url') THEN
        ALTER TABLE invoices ADD COLUMN qr_code_url TEXT;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='invoices' AND column_name='eway_bill_no') THEN
        ALTER TABLE invoices ADD COLUMN eway_bill_no VARCHAR(50);
    END IF;
END $$;
