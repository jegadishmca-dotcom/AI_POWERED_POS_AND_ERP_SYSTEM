-- Migration 50: Add case-insensitive functional index on barcodes(lower(barcode))
-- Speeds up case-insensitive barcode lookups by avoiding sequential table scans.

CREATE INDEX IF NOT EXISTS ix_barcodes_barcode_lower 
ON barcodes (LOWER(barcode));
