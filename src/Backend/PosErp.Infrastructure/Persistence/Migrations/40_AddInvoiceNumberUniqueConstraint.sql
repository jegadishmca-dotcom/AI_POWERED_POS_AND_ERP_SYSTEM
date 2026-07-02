-- Migration 40: Add UNIQUE constraint on (invoice_number, business_date)
-- to prevent duplicate invoice numbers, compatible with PostgreSQL partitioning.
ALTER TABLE invoices ADD CONSTRAINT uq_invoices_number_date UNIQUE (invoice_number, business_date);
