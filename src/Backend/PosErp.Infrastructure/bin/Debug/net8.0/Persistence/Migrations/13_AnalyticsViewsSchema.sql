-- ==============================================================================
-- PHASE 5: ANALYTICS MATERIALIZED VIEWS
-- ==============================================================================

-- Drop existing views to recreate with correct column names
DROP MATERIALIZED VIEW IF EXISTS mv_daily_sales_summary CASCADE;
DROP MATERIALIZED VIEW IF EXISTS mv_product_sales_stats CASCADE;

-- Daily Sales Summary (Fast line charts)
CREATE MATERIALIZED VIEW IF NOT EXISTS mv_daily_sales_summary AS
SELECT 
    terminal_id,
    business_date,
    COUNT(id) as total_invoices,
    SUM(sub_total) as gross_sales,
    SUM(discount_amount) as total_discounts,
    SUM(tax_amount) as total_tax,
    SUM(total_amount) as net_sales
FROM invoices
WHERE status = 'COMPLETED'
GROUP BY terminal_id, business_date;

CREATE UNIQUE INDEX IF NOT EXISTS idx_mv_daily_sales_summary ON mv_daily_sales_summary(terminal_id, business_date);

-- Top Products Stats (Fast tables)
CREATE MATERIALIZED VIEW IF NOT EXISTS mv_product_sales_stats AS
SELECT 
    i.business_date,
    ii.product_id,
    SUM(ii.quantity) as total_quantity_sold,
    SUM(ii.total_amount) as total_revenue
FROM invoices i
JOIN invoice_items ii ON i.id = ii.invoice_id AND i.business_date = ii.business_date
WHERE i.status = 'COMPLETED'
GROUP BY i.business_date, ii.product_id;

CREATE UNIQUE INDEX IF NOT EXISTS idx_mv_product_sales_stats ON mv_product_sales_stats(business_date, product_id);

-- Hangfire will call: REFRESH MATERIALIZED VIEW CONCURRENTLY mv_daily_sales_summary;
