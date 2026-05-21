-- ==============================================================================
-- PHASE 2: STOCK POSITION VIEWS
-- ==============================================================================

-- 1. Current Stock Materialized View
-- We use a MATERIALIZED VIEW for near-instant reporting on massive catalogs.
-- A Hangfire job should be scheduled to run REFRESH MATERIALIZED VIEW CONCURRENTLY mv_current_stock every 5-10 minutes.

CREATE MATERIALIZED VIEW mv_current_stock AS
SELECT DISTINCT ON (store_id, product_id)
    id as latest_ledger_id,
    store_id,
    product_id,
    batch_id,
    running_balance as current_stock,
    unit_cost as last_unit_cost,
    created_at as last_movement_date
FROM stock_ledger
ORDER BY store_id, product_id, created_at DESC;

CREATE UNIQUE INDEX idx_mv_current_stock_unique ON mv_current_stock (store_id, product_id);
