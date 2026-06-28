# =============================================================================
# tests/layer1_schema_scanner.py
#
# PURPOSE: Compare every C# entity property against actual PostgreSQL columns.
# CATCHES: Missing columns that cause 500 errors on INSERT/UPDATE (e.g. balance_after_transaction).
#
# HOW IT WORKS:
#   1. Reads every .cs entity file under PosErp.Domain/Entities/
#   2. Extracts class names + public property names using regex
#   3. Converts property names to snake_case (EF Core convention)
#   4. Looks up the actual table name (honoring DbContext overrides)
#   5. Queries PostgreSQL information_schema.columns
#   6. Reports any property that doesn't have a matching DB column
# =============================================================================

import os, re, sys
import psycopg2

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from config import DB_CONFIG, BACKEND_ENTITIES_PATH, DBCONTEXT_PATH, ok, fail, warn, info, header, section, C

# ── Properties ALWAYS ignored — EF navigation, collection, computed aliases ──
# Navigation properties hold references to other entities (not DB columns)
# EF Core uses the FK Id column (e.g. CustomerId) not the nav prop itself
IGNORED_PROPERTY_NAMES = {
    # Common navigation property names
    "Items", "Lines", "Tiers", "Customers", "Allocations", "Payments",
    "Batches", "Products", "Invoices", "Orders", "Returns", "Steps",
    "Versions", "UsageLogs", "Histories", "Transactions", "Entries",
    "DomainEvents", "Children", "Bins", "Variants", "PriceLists",
    # Computed alias properties (getter/setter delegates to another prop)
    # These have .Ignore() in DbContext — won't crash, just scanner noise
    "TotalDiscount", "TaxTotal", "FinalTotal", "Total",
    "MinStockLevel", "ReorderPoint", "SearchVector", "QrCodeUrl",
    # EF Core xmin concurrency tokens - mapped to PostgreSQL xmin system column
    # NOT a real DB column, it is a PostgreSQL system column, never in information_schema
    "Version",
}

# Property types that indicate EF navigation properties (entity references)
# These become FK columns (e.g. CustomerId) not direct columns
NAVIGATION_TYPE_KEYWORDS = [
    "Store", "Product", "Customer", "Supplier", "User", "Invoice",
    "JournalEntry", "Account", "GRNHeader", "PurchaseReturn",
    "PurchaseOrderHeader", "PurchaseBillHeader", "FixedAsset",
    "BankAccount", "SupplierPayment", "CustomerReceipt", "ProductBatch",
    "StockTakeHeader", "StockAdjustment", "Transfer", "Warehouse",
    "ApprovalRequest", "SalesReturn", "AiAlert", "TaxSlab", "Tier",
    "ActionedByUser", "ResolvedByUser",
    "Batch", "GlAccount", "CostCenter",
    "AccumulatedDeprAccount", "AssetAccount", "DepreciationExpenseAccount",
]

# Known COLUMN NAME OVERRIDES from ApplicationDbContext.HasColumnName() calls.
# Format: (ClassName, PropertyName) -> actual_db_column_name
# If the scanner finds ClassName.PropertyName missing, it checks here first.
COLUMN_NAME_OVERRIDES = {
    # Barcode.BarcodeValue -> HasColumnName("barcode")
    ("Barcode", "BarcodeValue"): "barcode",
    # EWayBillMetadata uses eway_bill_number not e_way_bill_number
    ("EWayBillMetadata", "EWayBillNumber"): "eway_bill_number",
    # These user-reference properties follow pattern XXXX_by not XXXX_by_id
    ("FinancialYear", "ClosedById"): "closed_by",
    ("FinancialPeriodLock", "LockedById"): "locked_by",
    ("PettyCashLedgerEntry", "ApprovedById"): "approved_by",
    ("InterStoreTransfer", "CreatedById"): "created_by",
    ("ApprovalRequest", "RequestedById"): "requested_by",
    ("ApprovalRequest", "ActionedById"): "actioned_by",
    ("ApprovalRequestStep", "ActionedById"): "actioned_by",
    # Customer tier
    ("Customer", "CustomerTierId"): "customer_tier_id",
}

# ── Known table name overrides from ApplicationDbContext ─────────────────────
# Format: C# class name → actual PostgreSQL table name
TABLE_OVERRIDES = {
    "LoyaltyLedgerEntry":         "loyalty_ledger",
    "StockLedgerEntry":           "stock_ledger",
    "WalletLedgerEntry":          "wallet_ledger",
    "GRNHeader":                  "grn_headers",
    "GRNItem":                    "grn_items",
    "StoreBusinessDate":          "store_business_dates",
    "StockAdjustmentItem":        "stock_adjustment_items",
    "StockTakeItem":              "stock_take_items",
    "GstHsnMasterIndia":          "gst_hsn_master_india",
    "AssetDepreciationHistory":   "asset_depreciation_history",
    "EInvoiceMetadata":           "einvoice_metadata",
    "EWayBillMetadata":           "ewaybill_metadata",
    "AuditLog":                   "audit_logs",
    "RefreshToken":               "refresh_tokens",
    "PettyCashLedgerEntry":       "petty_cash_ledger",
    "SupplierLedgerEntry":        "supplier_ledger",
    "CustomerLedgerEntry":        "customer_ledger",
    "OfferUsageLog":              "offer_usage_logs",
    "OfferVersion":               "offer_versions",
    "InventoryValuationHistory":  "inventory_valuation_history",
    "InterStoreTransfer":         "inter_store_transfers",
    "InterStoreTransferItem":     "inter_store_transfer_items",
    "PurchaseReturn":             "purchase_returns",
    "PurchaseReturnItem":         "purchase_return_items",
    "SalesReturn":                "sales_returns",
    "SalesReturnItem":            "sales_return_items",
    "AiKpiResult":                "ai_kpi_results",
    "AiKpiHistory":               "ai_kpi_history",
    "AiCashFlowForecast":         "ai_cash_flow_forecasts",
    "AiFinancialAnomaly":         "ai_financial_anomalies",
    "AiInventoryShrinkageAnalytic": "ai_inventory_shrinkage_analytics",
    "AiExpiryRiskPrediction":     "ai_expiry_risk_predictions",
    "AiSupplierPaymentRecommendation": "ai_supplier_payment_recommendations",
    "AiBusinessInsight":          "ai_business_insights",
    "AiDemandForecast":           "ai_demand_forecasts",
    "AiCustomerIntelligence":     "ai_customer_intelligences",
    "AiStorePerformance":         "ai_store_performances",
    "ExecutiveKpiSnapshot":       "executive_kpi_snapshots",
    "ForecastAccuracySnapshot":   "forecast_accuracy_snapshots",
    "AiAlert":                    "ai_alerts",
    "DailyFinanceSummary":        "daily_finance_summaries",
    "SupplierScorecard":          "supplier_scorecards",
    "SupplierRebate":             "supplier_rebates",
    "ApprovalRequest":            "approval_requests",
    "ApprovalRequestStep":        "approval_request_steps",
    "ApprovalLimit":              "approval_limits",
    "DocumentSequence":           "document_sequences",
    "FixedAsset":                 "fixed_assets",
    "CostCenter":                 "cost_centers",
    "FinancialYear":              "financial_years",
    "FinancialPeriodLock":        "financial_period_locks",
    "BankAccount":                "bank_accounts",
    "BankTransaction":            "bank_transactions",
    "JournalEntry":               "journal_entries",
    "JournalEntryLine":           "journal_entry_lines",
    "TaxTransaction":             "tax_transactions",
    "PosSession":                 "pos_sessions",
    "PurchaseOrderHeader":        "purchase_orders",
    "PurchaseOrderItem":          "purchase_order_items",
    "PurchaseBillHeader":         "purchase_bills",
    "PurchaseBillItem":           "purchase_bill_items",
    "CustomerReceipt":            "customer_receipts",
    "CustomerReceiptAllocation":  "customer_receipt_allocations",
    "SupplierPayment":            "supplier_payments",
    "SupplierPaymentAllocation":  "supplier_payment_allocations",
    "StockTransferRequest":       "stock_transfer_requests",
    "StockTakeHeader":            "stock_take_headers",
    "StockAdjustmentHeader":      "stock_adjustment_headers",
    "ProductBatch":               "product_batches",
}

# Classes to SKIP entirely (abstract, interface, helper, no DB table)
SKIP_CLASSES = {
    "ITenantEntity", "BaseEntity", "IAggregateRoot", "IEntity",
}


def to_snake_case(name: str) -> str:
    """Convert PascalCase/camelCase C# property to snake_case for PostgreSQL."""
    # Handle acronyms like GRN, HSN, ID
    s1 = re.sub(r'([A-Z]+)([A-Z][a-z])', r'\1_\2', name)
    s2 = re.sub(r'([a-z0-9])([A-Z])', r'\1_\2', s1)
    return s2.lower()


def class_to_table(class_name: str) -> str:
    """Convert C# class name to PostgreSQL table name."""
    if class_name in TABLE_OVERRIDES:
        return TABLE_OVERRIDES[class_name]
    # Default: pluralize snake_case
    snake = to_snake_case(class_name)
    if snake.endswith("y"):
        return snake[:-1] + "ies"
    elif snake.endswith("s"):
        return snake + "es"
    else:
        return snake + "s"


def extract_classes_from_file(filepath: str) -> dict:
    """
    Parse a C# file and return {ClassName: [property_names]} for all public classes.
    Skips:
    - Navigation properties (property type matches a known entity class name)
    - Collection properties (ICollection<>, IList<>, List<>, IEnumerable<>)
    - Computed aliases and Ignored properties
    """
    classes = {}

    with open(filepath, "r", encoding="utf-8", errors="ignore") as f:
        content = f.read()

    # Find class declarations
    class_pattern = re.compile(
        r'public\s+(?:partial\s+)?class\s+(\w+)\s*(?:<[^>]*>)?(?:\s*:\s*[^\{]+)?\s*\{'
    )
    # Public properties: public [modifiers] Type Name { get; [set;] }
    # We also capture the TYPE to detect navigation properties
    prop_pattern = re.compile(
        r'public\s+'
        r'(?:(?:virtual|override|new|static|required|abstract)\s+)*'
        r'([A-Za-z_][\w<>?,?\[\] ]+?)\s+'   # capture type
        r'([A-Z][A-Za-z0-9_]+)'               # capture property name (PascalCase)
        r'\s*\{[^}]*get[^}]*\}'
    )

    for match in class_pattern.finditer(content):
        class_name = match.group(1)
        if class_name in SKIP_CLASSES:
            continue
        start = match.end()
        depth = 1
        pos = start
        while pos < len(content) and depth > 0:
            if content[pos] == '{':
                depth += 1
            elif content[pos] == '}':
                depth -= 1
            pos += 1
        class_body = content[start:pos]

        props = []
        for pm in prop_pattern.finditer(class_body):
            prop_type = pm.group(1).strip()
            prop_name = pm.group(2).strip()

            # Skip ignored names
            if prop_name in IGNORED_PROPERTY_NAMES:
                continue
            if prop_name.startswith("_"):
                continue

            # Skip collection navigation properties (ICollection<>, List<>, IEnumerable<>)
            if any(col in prop_type for col in ["ICollection", "IList", "List<", "IEnumerable", "HashSet"]):
                continue

            # Skip navigation properties: type name matches a known entity or starts with known prefix
            is_nav = False
            for nav_keyword in NAVIGATION_TYPE_KEYWORDS:
                if prop_type.strip().rstrip("?") == nav_keyword:
                    is_nav = True
                    break
            if is_nav:
                continue

            props.append(prop_name)

        if props:
            classes[class_name] = list(dict.fromkeys(props))  # deduplicate preserving order

    return classes


def get_all_entity_files() -> list:
    """Recursively find all .cs files in the Entities folder."""
    files = []
    for root, dirs, filenames in os.walk(BACKEND_ENTITIES_PATH):
        for fname in filenames:
            if fname.endswith(".cs"):
                files.append(os.path.join(root, fname))
    return files


def get_db_columns(cursor, table_name: str) -> set:
    """Get all column names for a given table from information_schema."""
    cursor.execute(
        "SELECT column_name FROM information_schema.columns WHERE table_name = %s",
        (table_name,)
    )
    return {row[0] for row in cursor.fetchall()}


def table_exists(cursor, table_name: str) -> bool:
    cursor.execute(
        "SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = %s)",
        (table_name,)
    )
    return cursor.fetchone()[0]


def run() -> dict:
    """Run the schema scanner. Returns summary dict."""
    header("LAYER 1: DATABASE SCHEMA INTEGRITY SCANNER")
    info("Connecting to database...")

    conn = psycopg2.connect(**DB_CONFIG)
    cur = conn.cursor()

    entity_files = get_all_entity_files()
    info(f"Found {len(entity_files)} C# entity files to scan")

    results = {
        "total_classes": 0,
        "passed": 0,
        "failed": 0,
        "skipped": 0,   # table doesn't exist yet (staging only)
        "issues": []
    }

    for filepath in sorted(entity_files):
        classes = extract_classes_from_file(filepath)
        filename = os.path.basename(filepath)

        for class_name, properties in classes.items():
            if not properties:
                continue
            results["total_classes"] += 1
            table_name = class_to_table(class_name)

            if not table_exists(cur, table_name):
                results["skipped"] += 1
                warn(f"{class_name:40} → table '{table_name}' not found in DB (skip)")
                continue

            db_cols = get_db_columns(cur, table_name)
            missing = []

            for prop in properties:
                col = to_snake_case(prop)
                # Skip navigation property hints and common false positives
                if col in ("domain_events",):
                    continue
                # Check if there is a known column name override (HasColumnName mapping)
                override_col = COLUMN_NAME_OVERRIDES.get((class_name, prop))
                if override_col:
                    col = override_col  # use the actual DB column name
                if col not in db_cols:
                    missing.append(f"{prop} → {col}")


            if missing:
                results["failed"] += 1
                fail(f"{class_name:40} → table '{table_name}' MISSING {len(missing)} column(s):")
                for m in missing:
                    print(f"         {C.FAIL}✗{C.RESET}  {m}")
                results["issues"].append({
                    "class": class_name,
                    "table": table_name,
                    "missing": missing
                })
            else:
                results["passed"] += 1
                ok(f"{class_name:40} → '{table_name}' ({len(properties)} props, all matched)")

    conn.close()

    section("LAYER 1 SUMMARY")
    total = results["total_classes"]
    p = results["passed"]
    f = results["failed"]
    s = results["skipped"]
    print(f"  Classes Scanned : {total}")
    print(f"  {C.PASS}PASSED{C.RESET}          : {p}")
    print(f"  {C.FAIL}FAILED{C.RESET}          : {f}")
    print(f"  {C.WARN}SKIPPED{C.RESET}         : {s} (table not in DB)")

    if results["issues"]:
        print(f"\n  {C.FAIL}{C.BOLD}ACTION REQUIRED — Schema mismatches will cause 500 errors:{C.RESET}")
        for issue in results["issues"]:
            print(f"    ✗ {issue['class']} ({issue['table']}): {', '.join(issue['missing'][:3])}{'...' if len(issue['missing'])>3 else ''}")

    return results


if __name__ == "__main__":
    run()
