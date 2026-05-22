-- ==============================================================================
-- PRODUCT FULL-TEXT SEARCH TRIGGER & FUNCTION
-- Updates search_vector automatically on INSERT/UPDATE of product or its barcodes
-- ==============================================================================

-- 1. Add search_vector column if it doesn't exist
ALTER TABLE products ADD COLUMN IF NOT EXISTS search_vector tsvector;

-- 2. Create the function
CREATE OR REPLACE FUNCTION products_search_vector_trigger() RETURNS trigger AS $$
DECLARE
  barcodes_str TEXT;
BEGIN
  -- Aggregate all associated barcodes for the product
  SELECT string_agg(barcode, ' ') INTO barcodes_str
  FROM barcodes
  WHERE product_id = NEW.id AND is_deleted = false;

  -- Build the TSVECTOR with weights
  NEW.search_vector :=
    setweight(to_tsvector('english', coalesce(NEW.name, '')), 'A') ||
    setweight(to_tsvector('english', coalesce(NEW.product_code, '')), 'A') ||
    setweight(to_tsvector('english', coalesce(NEW.tamil_name, '')), 'B') ||
    setweight(to_tsvector('english', coalesce(NEW.hsn_code, '')), 'C') ||
    setweight(to_tsvector('english', coalesce(barcodes_str, '')), 'A');
    
  RETURN NEW;
END
$$ LANGUAGE plpgsql;

-- 2. Attach trigger to Products
DROP TRIGGER IF EXISTS trg_products_search_vector ON products;
CREATE TRIGGER trg_products_search_vector
BEFORE INSERT OR UPDATE ON products
FOR EACH ROW EXECUTE FUNCTION products_search_vector_trigger();

-- 3. Create a secondary trigger on Barcodes to update the Product's search_vector when a barcode changes
CREATE OR REPLACE FUNCTION update_product_search_vector_from_barcode() RETURNS trigger AS $$
BEGIN
  IF TG_OP = 'DELETE' THEN
    UPDATE products SET updated_at = NOW() WHERE id = OLD.product_id;
    RETURN OLD;
  ELSE
    UPDATE products SET updated_at = NOW() WHERE id = NEW.product_id;
    RETURN NEW;
  END IF;
END
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_barcodes_update_product ON barcodes;
CREATE TRIGGER trg_barcodes_update_product
AFTER INSERT OR UPDATE OR DELETE ON barcodes
FOR EACH ROW EXECUTE FUNCTION update_product_search_vector_from_barcode();

-- 4. Create Indexes
CREATE INDEX IF NOT EXISTS idx_products_search ON products USING GIN(search_vector);
CREATE INDEX IF NOT EXISTS idx_barcodes_barcode ON barcodes(barcode) WHERE is_deleted = FALSE;
CREATE INDEX IF NOT EXISTS idx_products_code ON products(product_code) WHERE is_deleted = FALSE;
CREATE INDEX IF NOT EXISTS idx_products_hsn ON products(hsn_code) WHERE is_deleted = FALSE;
