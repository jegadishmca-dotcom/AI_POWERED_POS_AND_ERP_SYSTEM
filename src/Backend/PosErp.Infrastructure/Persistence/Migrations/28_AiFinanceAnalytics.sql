-- 1. Add square_footage to stores table
ALTER TABLE stores ADD COLUMN IF NOT EXISTS square_footage DECIMAL(18,2) DEFAULT 2000.00 NOT NULL;

-- 2. KPI results cache table
CREATE TABLE IF NOT EXISTS ai_kpi_results (
    id UUID PRIMARY KEY,
    store_id UUID REFERENCES stores(id) ON DELETE CASCADE, -- NULL meansHQ/Consolidated
    kpi_type VARCHAR(100) NOT NULL, -- FINANCIAL, INVENTORY, STORE
    kpi_name VARCHAR(100) NOT NULL,
    kpi_value DECIMAL(18,4) NOT NULL,
    calculated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_ai_kpi_results_store_kpi ON ai_kpi_results(store_id, kpi_type, kpi_name);

-- 3. KPI history table
CREATE TABLE IF NOT EXISTS ai_kpi_history (
    id UUID PRIMARY KEY,
    store_id UUID REFERENCES stores(id) ON DELETE CASCADE,
    kpi_type VARCHAR(100) NOT NULL,
    kpi_name VARCHAR(100) NOT NULL,
    kpi_value DECIMAL(18,4) NOT NULL,
    recorded_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_ai_kpi_history_store_date ON ai_kpi_history(store_id, recorded_at);

-- 4. Cash Flow Forecast V1 cache table
CREATE TABLE IF NOT EXISTS ai_cash_flow_forecasts (
    id UUID PRIMARY KEY,
    store_id UUID REFERENCES stores(id) ON DELETE CASCADE,
    forecast_date DATE NOT NULL,
    projected_inflow DECIMAL(18,2) DEFAULT 0 NOT NULL,
    projected_outflow DECIMAL(18,2) DEFAULT 0 NOT NULL,
    projected_balance DECIMAL(18,2) DEFAULT 0 NOT NULL,
    confidence_level VARCHAR(50) NOT NULL, -- HIGH, MEDIUM, LOW
    calculated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_ai_cash_flow_forecasts_store_date ON ai_cash_flow_forecasts(store_id, forecast_date);

-- 5. Supplier Payment Recommendation table
CREATE TABLE IF NOT EXISTS ai_supplier_payment_recommendations (
    id UUID PRIMARY KEY,
    supplier_id UUID REFERENCES suppliers(id) ON DELETE CASCADE NOT NULL,
    supplier_name VARCHAR(255) NOT NULL,
    purchase_bill_id UUID REFERENCES purchase_bill_headers(id) ON DELETE CASCADE NOT NULL,
    bill_number VARCHAR(255) NOT NULL,
    due_date DATE NOT NULL,
    amount_due DECIMAL(18,2) NOT NULL,
    discount_available DECIMAL(18,2) DEFAULT 0 NOT NULL,
    discount_expiry_date DATE,
    priority_score INT NOT NULL,
    recommendation_reason VARCHAR(500) NOT NULL,
    feedback_status VARCHAR(50) DEFAULT 'PENDING' NOT NULL, -- PENDING, ACCEPTED, REJECTED
    feedback_notes VARCHAR(500),
    actioned_at TIMESTAMP WITH TIME ZONE,
    actioned_by UUID REFERENCES users(id) ON DELETE SET NULL,
    calculated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL
);

-- 6. Financial Anomaly detection table
CREATE TABLE IF NOT EXISTS ai_financial_anomalies (
    id UUID PRIMARY KEY,
    anomaly_type VARCHAR(100) NOT NULL, -- DUPLICATE_PAYMENT, UNUSUAL_JOURNAL, CASHIER_SHORTAGE
    severity VARCHAR(50) NOT NULL, -- CRITICAL, WARNING, INFO
    description VARCHAR(1000) NOT NULL,
    detected_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL,
    reference_id UUID, -- journal_entry_id or cashier session id
    is_resolved BOOLEAN DEFAULT FALSE NOT NULL,
    resolved_at TIMESTAMP WITH TIME ZONE,
    resolved_by UUID REFERENCES users(id) ON DELETE SET NULL
);

-- 7. Inventory Shrinkage Analytics table
CREATE TABLE IF NOT EXISTS ai_inventory_shrinkage_analytics (
    id UUID PRIMARY KEY,
    store_id UUID REFERENCES stores(id) ON DELETE CASCADE,
    product_id UUID REFERENCES products(id) ON DELETE CASCADE NOT NULL,
    product_name VARCHAR(255) NOT NULL,
    shrinkage_quantity DECIMAL(18,2) NOT NULL,
    shrinkage_cost DECIMAL(18,2) NOT NULL,
    shrinkage_rate_pct DECIMAL(18,4) NOT NULL,
    risk_level VARCHAR(50) NOT NULL, -- HIGH, MEDIUM, LOW
    calculated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL
);

-- 8. Expiry Risk Prediction table
CREATE TABLE IF NOT EXISTS ai_expiry_risk_predictions (
    id UUID PRIMARY KEY,
    store_id UUID REFERENCES stores(id) ON DELETE CASCADE,
    product_id UUID REFERENCES products(id) ON DELETE CASCADE NOT NULL,
    product_name VARCHAR(255) NOT NULL,
    batch_id UUID REFERENCES product_batches(id) ON DELETE CASCADE NOT NULL,
    batch_number VARCHAR(100) NOT NULL,
    expiry_date DATE NOT NULL,
    remaining_quantity DECIMAL(18,2) NOT NULL,
    cost_price DECIMAL(18,2) NOT NULL,
    potential_loss DECIMAL(18,2) NOT NULL,
    average_daily_sales_qty DECIMAL(18,4) NOT NULL,
    projected_sold_qty DECIMAL(18,2) NOT NULL,
    expiry_risk_pct DECIMAL(18,4) NOT NULL,
    risk_category VARCHAR(50) NOT NULL, -- CRITICAL, HIGH, MEDIUM, LOW
    calculated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL
);

-- 9. AI Alerts table
CREATE TABLE IF NOT EXISTS ai_alerts (
    id UUID PRIMARY KEY,
    store_id UUID REFERENCES stores(id) ON DELETE CASCADE,
    alert_type VARCHAR(100) NOT NULL, -- SHRINKAGE, EXPIRY, ANOMALY, BUDGET_OVERRUN
    severity VARCHAR(50) NOT NULL, -- CRITICAL, WARNING, INFO
    title VARCHAR(255) NOT NULL,
    message VARCHAR(1000) NOT NULL,
    is_read BOOLEAN DEFAULT FALSE NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL,
    resolved_at TIMESTAMP WITH TIME ZONE,
    resolved_by UUID REFERENCES users(id) ON DELETE SET NULL
);
CREATE INDEX IF NOT EXISTS idx_ai_alerts_store_status ON ai_alerts(store_id, is_read);
