import pandas as pd
import os

output_dir = r"D:\JEGADISH\APPLE_SUPERMARKET_POS_PROJECT\AI_POWERED_POS_AND_ERP_SYSTEM\Documentation\RC1"

# --- API Catalog ---
api_data = [
    {"Controller": "AuthController", "Endpoint": "POST /api/auth/login", "Purpose": "Authenticate user", "Authorization": "AllowAnonymous", "Dependencies": "JwtTokenGenerator, UserManager"},
    {"Controller": "PosController", "Endpoint": "POST /api/pos/checkout", "Purpose": "Process sale invoice", "Authorization": "Cashier, Manager", "Dependencies": "IApplicationDbContext, IStockLedgerService"},
    {"Controller": "LoyaltyController", "Endpoint": "POST /api/loyalty/redeem", "Purpose": "Redeem customer points", "Authorization": "Cashier", "Dependencies": "ILoyaltyService"},
    {"Controller": "OffersController", "Endpoint": "GET /api/offers/active", "Purpose": "Fetch active POS promotions", "Authorization": "Cashier, Manager", "Dependencies": "IOfferEngine"},
    {"Controller": "InventoryIntelligenceController", "Endpoint": "GET /api/inventory/intelligence/health", "Purpose": "Fetch inventory scores", "Authorization": "Manager, Owner", "Dependencies": "IAiAnalyticsService"},
    {"Controller": "ProcurementController", "Endpoint": "POST /api/procurement/generate-draft", "Purpose": "Generate Draft PO from recommendations", "Authorization": "Manager", "Dependencies": "IPurchaseRecommendationEngine"},
    {"Controller": "ExecutiveDashboardController", "Endpoint": "GET /api/executive/kpi/revenue", "Purpose": "Fetch executive revenue KPIs", "Authorization": "Owner", "Dependencies": "IFinancialReportingService"}
]

api_df = pd.DataFrame(api_data)
api_df.to_excel(os.path.join(output_dir, "API_Catalog.xlsx"), index=False)

# --- Background Jobs Catalog ---
job_data = [
    {"Job Name": "ExpirePointsJob", "Schedule": "Daily (02:00 AM)", "Purpose": "Expire unused loyalty points", "Dependencies": "ILoyaltyService", "Output Tables": "LoyaltyLedgerEntry", "Failure Handling": "Idempotent. Re-runs next day."},
    {"Job Name": "BirthdayBonusJob", "Schedule": "Daily (01:00 AM)", "Purpose": "Award points for birthdays", "Dependencies": "ICustomerTierService", "Output Tables": "LoyaltyLedgerEntry", "Failure Handling": "Snapshot Check. Safe to retry."},
    {"Job Name": "AnniversaryBonusJob", "Schedule": "Daily (01:15 AM)", "Purpose": "Award points for anniversaries", "Dependencies": "ICustomerTierService", "Output Tables": "LoyaltyLedgerEntry", "Failure Handling": "Snapshot Check. Safe to retry."},
    {"Job Name": "TierEvaluationJob", "Schedule": "Monthly (1st, 03:00 AM)", "Purpose": "Downgrade expired tiers", "Dependencies": "ICustomerTierService", "Output Tables": "CustomerTier", "Failure Handling": "Logged. Requires manual re-run if failed."},
    {"Job Name": "InsightGenerationJob", "Schedule": "Daily (01:00 AM)", "Purpose": "Generate AI business insights", "Dependencies": "IInsightEngine", "Output Tables": "AiBusinessInsight", "Failure Handling": "Snapshot Check. Auto-heals next run."},
    {"Job Name": "ForecastGenerationJob", "Schedule": "Weekly (Sun 02:00 AM)", "Purpose": "Update AI demand forecasts", "Dependencies": "IForecastEngine", "Output Tables": "AiDemandForecast", "Failure Handling": "Retries 3x via Hangfire."},
    {"Job Name": "AlertGenerationJob", "Schedule": "Hourly", "Purpose": "Generate threshold alerts (Expiry, Dead Stock)", "Dependencies": "IAiAnalyticsService", "Output Tables": "AiAlert", "Failure Handling": "Idempotent. Skips if already open."},
    {"Job Name": "ExecutiveSnapshotJob", "Schedule": "Daily (23:55 PM)", "Purpose": "Snapshot KPIs for Owner Dashboard", "Dependencies": "IFinancialReportingService", "Output Tables": "ExecutiveKpiSnapshot", "Failure Handling": "Can be manually triggered for past date."}
]

job_df = pd.DataFrame(job_data)
job_df.to_excel(os.path.join(output_dir, "Background_Jobs_Catalog.xlsx"), index=False)

# --- Database Entity Catalog ---
db_data = [
    {"Table Name": "Tenant", "Module": "Core", "Purpose": "Top-level isolation boundary", "Tenant Scoped": "No", "Audit Required": "Yes"},
    {"Table Name": "Store", "Module": "Core", "Purpose": "Physical store location", "Tenant Scoped": "Yes", "Audit Required": "Yes"},
    {"Table Name": "User", "Module": "Core", "Purpose": "Identity & Access", "Tenant Scoped": "Yes", "Audit Required": "Yes"},
    {"Table Name": "Role", "Module": "Core", "Purpose": "RBAC Roles", "Tenant Scoped": "Yes", "Audit Required": "Yes"},
    {"Table Name": "Invoice", "Module": "POS", "Purpose": "Sales Header", "Tenant Scoped": "Yes", "Audit Required": "Yes"},
    {"Table Name": "InvoiceItem", "Module": "POS", "Purpose": "Sales Detail", "Tenant Scoped": "Yes", "Audit Required": "No"},
    {"Table Name": "Terminal", "Module": "POS", "Purpose": "Hardware definition", "Tenant Scoped": "Yes", "Audit Required": "Yes"},
    {"Table Name": "Customer", "Module": "CRM", "Purpose": "Shopper Identity", "Tenant Scoped": "Yes", "Audit Required": "Yes"},
    {"Table Name": "CustomerTier", "Module": "CRM", "Purpose": "Loyalty Status Level", "Tenant Scoped": "Yes", "Audit Required": "Yes"},
    {"Table Name": "LoyaltyLedgerEntry", "Module": "CRM", "Purpose": "Immutable point history", "Tenant Scoped": "Yes", "Audit Required": "Yes"},
    {"Table Name": "Offer", "Module": "Promotions", "Purpose": "Discount Rules", "Tenant Scoped": "Yes", "Audit Required": "Yes"},
    {"Table Name": "OfferVersion", "Module": "Promotions", "Purpose": "Audit trail for offers", "Tenant Scoped": "Yes", "Audit Required": "Yes"},
    {"Table Name": "OfferUsageLog", "Module": "Promotions", "Purpose": "Tracks offer redemption", "Tenant Scoped": "Yes", "Audit Required": "No"},
    {"Table Name": "Product", "Module": "Inventory", "Purpose": "Global SKU master", "Tenant Scoped": "Yes", "Audit Required": "Yes"},
    {"Table Name": "InventoryLocation", "Module": "Inventory", "Purpose": "Warehouses & Shelves", "Tenant Scoped": "Yes", "Audit Required": "Yes"},
    {"Table Name": "ProductStoreInventoryPolicy", "Module": "Inventory", "Purpose": "Reorder & EOQ Rules", "Tenant Scoped": "Yes", "Audit Required": "Yes"},
    {"Table Name": "InventoryForecast", "Module": "Inventory", "Purpose": "AI demand outputs", "Tenant Scoped": "Yes", "Audit Required": "No"},
    {"Table Name": "InventoryAgingSnapshot", "Module": "Inventory", "Purpose": "FIFO Expiry tracking", "Tenant Scoped": "Yes", "Audit Required": "No"},
    {"Table Name": "PurchaseOrder", "Module": "Procurement", "Purpose": "PO Header", "Tenant Scoped": "Yes", "Audit Required": "Yes"},
    {"Table Name": "PurchaseOrderItem", "Module": "Procurement", "Purpose": "PO Lines", "Tenant Scoped": "Yes", "Audit Required": "No"},
    {"Table Name": "Supplier", "Module": "Procurement", "Purpose": "Vendor Master", "Tenant Scoped": "Yes", "Audit Required": "Yes"},
    {"Table Name": "SupplierScorecard", "Module": "Procurement", "Purpose": "Fill rate analytics", "Tenant Scoped": "Yes", "Audit Required": "No"},
    {"Table Name": "AiBusinessInsight", "Module": "AI Intelligence", "Purpose": "Actionable analytics", "Tenant Scoped": "Yes", "Audit Required": "Yes"},
    {"Table Name": "AiDemandForecast", "Module": "AI Intelligence", "Purpose": "Predictive modeling", "Tenant Scoped": "Yes", "Audit Required": "No"},
    {"Table Name": "AiCustomerIntelligence", "Module": "AI Intelligence", "Purpose": "Churn risk scoring", "Tenant Scoped": "Yes", "Audit Required": "No"},
    {"Table Name": "ExecutiveKpiSnapshot", "Module": "AI Intelligence", "Purpose": "Dashboard metrics", "Tenant Scoped": "Yes", "Audit Required": "No"},
    {"Table Name": "ForecastAccuracySnapshot", "Module": "AI Intelligence", "Purpose": "Model drift tracking", "Tenant Scoped": "Yes", "Audit Required": "No"},
    {"Table Name": "AiAlert", "Module": "AI Intelligence", "Purpose": "Operational anomaly triggers", "Tenant Scoped": "Yes", "Audit Required": "Yes"},
    {"Table Name": "AuditLog", "Module": "Security", "Purpose": "Immutable system history", "Tenant Scoped": "Yes", "Audit Required": "No"}
]

db_df = pd.DataFrame(db_data)
db_df.to_excel(os.path.join(output_dir, "Database_Entity_Catalog.xlsx"), index=False)

print("Catalogs generated successfully.")
