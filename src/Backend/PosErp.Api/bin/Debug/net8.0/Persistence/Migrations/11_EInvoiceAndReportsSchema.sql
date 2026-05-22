-- ==============================================================================
-- PHASE 4: E-INVOICING AND REPORTS
-- ==============================================================================
ALTER TABLE invoices 
ADD COLUMN irn VARCHAR(64) UNIQUE,
ADD COLUMN ack_no VARCHAR(50),
ADD COLUMN ack_date TIMESTAMP WITH TIME ZONE,
ADD COLUMN qr_code_url TEXT,
ADD COLUMN eway_bill_no VARCHAR(50);
