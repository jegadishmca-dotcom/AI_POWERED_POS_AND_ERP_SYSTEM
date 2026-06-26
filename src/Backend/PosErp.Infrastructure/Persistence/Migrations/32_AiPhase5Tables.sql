-- ============================================================
-- Migration 32: AI Phase 5 Tables
-- Creates all Phase 5 AI entity tables that are defined in EF Core
-- but were missing from the raw SQL migration runner.
-- ============================================================

-- 1. AI Business Insights
CREATE TABLE IF NOT EXISTS ai_business_insights (
    id UUID PRIMARY KEY,
    insight_category TEXT NOT NULL,
    business_area TEXT NOT NULL,
    title TEXT NOT NULL,
    description TEXT NOT NULL,
    impact_score INTEGER NOT NULL DEFAULT 0,
    confidence_score INTEGER NOT NULL DEFAULT 0,
    estimated_financial_impact NUMERIC NOT NULL DEFAULT 0,
    recommended_action TEXT NOT NULL,
    generation_reasoning TEXT NOT NULL,
    reference_type TEXT,
    reference_id UUID,
    status TEXT NOT NULL DEFAULT 'OPEN',
    assigned_to UUID,
    resolved_date TIMESTAMP WITH TIME ZONE,
    resolution_notes TEXT,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);
CREATE INDEX IF NOT EXISTS idx_ai_business_insights_category ON ai_business_insights(insight_category);
CREATE INDEX IF NOT EXISTS idx_ai_business_insights_status ON ai_business_insights(status);

-- 2. AI Customer Intelligences (churn risk, LTV, segmentation)
CREATE TABLE IF NOT EXISTS ai_customer_intelligences (
    id UUID PRIMARY KEY,
    customer_id UUID NOT NULL,
    segment_type TEXT NOT NULL,
    churn_risk_pct NUMERIC NOT NULL DEFAULT 0,
    ltv_prediction NUMERIC NOT NULL DEFAULT 0,
    lifetime_value_category TEXT NOT NULL,
    predicted_next_purchase_date TIMESTAMP WITH TIME ZONE,
    churn_category TEXT NOT NULL,
    recommended_action TEXT NOT NULL,
    last_calculated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);
CREATE INDEX IF NOT EXISTS idx_ai_customer_intel_customer ON ai_customer_intelligences(customer_id);
CREATE INDEX IF NOT EXISTS idx_ai_customer_intel_churn ON ai_customer_intelligences(churn_risk_pct DESC);
CREATE INDEX IF NOT EXISTS idx_ai_customer_intel_calc_at ON ai_customer_intelligences(last_calculated_at);

-- 3. AI Demand Forecasts
CREATE TABLE IF NOT EXISTS ai_demand_forecasts (
    id UUID PRIMARY KEY,
    forecast_type TEXT NOT NULL,
    reference_id UUID,
    forecast_date TIMESTAMP WITH TIME ZONE NOT NULL,
    forecast_horizon_days INTEGER NOT NULL DEFAULT 7,
    forecast_method TEXT NOT NULL,
    forecast_quantity NUMERIC NOT NULL DEFAULT 0,
    actual_quantity NUMERIC,
    forecast_error NUMERIC,
    confidence_level NUMERIC NOT NULL DEFAULT 0,
    model_version TEXT NOT NULL DEFAULT '1.0',
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);
CREATE INDEX IF NOT EXISTS idx_ai_demand_forecasts_type_date ON ai_demand_forecasts(forecast_type, forecast_date);
CREATE INDEX IF NOT EXISTS idx_ai_demand_forecasts_ref ON ai_demand_forecasts(reference_id);

-- 4. AI Store Performances
CREATE TABLE IF NOT EXISTS ai_store_performances (
    id UUID PRIMARY KEY,
    store_id UUID NOT NULL,
    metric_name TEXT NOT NULL,
    metric_value NUMERIC NOT NULL DEFAULT 0,
    benchmark_value NUMERIC NOT NULL DEFAULT 0,
    variance NUMERIC NOT NULL DEFAULT 0,
    rank INTEGER NOT NULL DEFAULT 0,
    benchmark_group TEXT NOT NULL,
    percentile NUMERIC NOT NULL DEFAULT 0,
    calculated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);
CREATE INDEX IF NOT EXISTS idx_ai_store_perf_store ON ai_store_performances(store_id);
CREATE INDEX IF NOT EXISTS idx_ai_store_perf_metric ON ai_store_performances(metric_name);

-- 5. Executive KPI Snapshots (daily snapshot of key business metrics)
CREATE TABLE IF NOT EXISTS executive_kpi_snapshots (
    id UUID PRIMARY KEY,
    snapshot_date TIMESTAMP WITH TIME ZONE NOT NULL,
    daily_sales NUMERIC NOT NULL DEFAULT 0,
    daily_profit NUMERIC NOT NULL DEFAULT 0,
    gross_margin_pct NUMERIC NOT NULL DEFAULT 0,
    total_inventory_value NUMERIC NOT NULL DEFAULT 0,
    dead_stock_value NUMERIC NOT NULL DEFAULT 0,
    active_loyalty_members INTEGER NOT NULL DEFAULT 0,
    active_customers INTEGER NOT NULL DEFAULT 0,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);
CREATE INDEX IF NOT EXISTS idx_exec_kpi_snapshot_date ON executive_kpi_snapshots(snapshot_date);

-- 6. Forecast Accuracy Snapshots (daily model evaluation results)
CREATE TABLE IF NOT EXISTS forecast_accuracy_snapshots (
    id UUID PRIMARY KEY,
    snapshot_date TIMESTAMP WITH TIME ZONE NOT NULL,
    model_version TEXT NOT NULL DEFAULT '1.0',
    mean_absolute_percentage_error NUMERIC NOT NULL DEFAULT 0,
    mean_absolute_error NUMERIC NOT NULL DEFAULT 0,
    root_mean_square_error NUMERIC NOT NULL DEFAULT 0,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);
CREATE INDEX IF NOT EXISTS idx_forecast_accuracy_date ON forecast_accuracy_snapshots(snapshot_date);

-- 7. Patch ai_alerts table: ensure alert_severity column exists
--    (EF migration renamed severity -> alert_severity; handle both column states)
ALTER TABLE ai_alerts ADD COLUMN IF NOT EXISTS alert_severity VARCHAR(50) NOT NULL DEFAULT 'INFO';
