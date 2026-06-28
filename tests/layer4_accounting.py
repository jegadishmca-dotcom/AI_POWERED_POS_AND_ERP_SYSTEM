# =============================================================================
# tests/layer4_accounting.py
#
# PURPOSE: Verify the integrity of every journal entry and accounting rule.
# CATCHES: Imbalanced journals, missing GST accounts, wrong account types,
#          orphaned journal lines, trial balance discrepancies.
# =============================================================================

import sys, os
import psycopg2
from decimal import Decimal

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from config import DB_CONFIG, ok, fail, warn, info, header, section, C


def run() -> dict:
    header("LAYER 4: ACCOUNTING INTEGRITY CHECKS")

    conn = psycopg2.connect(**DB_CONFIG)
    cur = conn.cursor()

    results = {
        "checks": 0,
        "passed": 0,
        "failed": 0,
        "warnings": 0,
        "issues": []
    }

    def chk_ok(msg):   results["checks"] += 1; results["passed"] += 1; ok(msg)
    def chk_fail(msg): results["checks"] += 1; results["failed"] += 1; fail(msg); results["issues"].append(msg)
    def chk_warn(msg): results["checks"] += 1; results["warnings"] += 1; warn(msg)

    # ── CHECK 1: All Journal Entries are Balanced (Dr = Cr) ───────────────────
    section("CHECK 1: Journal Entry Balance (Dr = Cr for every entry)")
    cur.execute("""
        SELECT je.id, je.entry_number, je.description,
               COALESCE(SUM(jl.debit_amount), 0)  AS total_dr,
               COALESCE(SUM(jl.credit_amount), 0) AS total_cr,
               COUNT(jl.id) AS line_count
        FROM journal_entries je
        LEFT JOIN journal_entry_lines jl ON jl.journal_entry_id = je.id
        GROUP BY je.id, je.entry_number, je.description
        ORDER BY je.created_at DESC;
    """)
    je_rows = cur.fetchall()
    imbalanced = 0
    empty_je = 0
    for row in je_rows:
        je_id, je_num, desc, dr, cr, lines = row
        dr, cr = float(dr), float(cr)
        if lines == 0:
            empty_je += 1
            chk_fail(f"JE {je_num}: NO LINES — journal entry has zero line items!")
        elif abs(dr - cr) > 0.01:
            imbalanced += 1
            chk_fail(f"JE {je_num}: IMBALANCED — Dr={dr:.2f} Cr={cr:.2f} (diff={abs(dr-cr):.4f}) | {(desc or '')[:50]}")
        else:
            chk_ok(f"JE {je_num}: Balanced — Dr=Cr={dr:.2f} | Lines={lines}")

    if imbalanced == 0 and empty_je == 0:
        info(f"All {len(je_rows)} journal entries are balanced.")
    else:
        results["issues"].append(f"{imbalanced} imbalanced + {empty_je} empty journal entries found")

    # ── CHECK 2: Required GST Accounts Exist ──────────────────────────────────
    section("CHECK 2: Required Chart of Accounts (GST + Core Accounts)")
    required_accounts = {
        "1000": "Cash on Hand",
        "1100": "Bank Account",
        "4000": "Sales Revenue",
        "5000": "Cost of Goods Sold",
        "2000": "Accounts Payable",
        "22010": "Output CGST",
        "22020": "Output SGST",
        "22030": "Input CGST",
        "22040": "Input SGST",
    }
    cur.execute("SELECT account_code, name, is_active FROM accounts WHERE account_code = ANY(%s);",
                (list(required_accounts.keys()),))
    found = {r[0]: (r[1], r[2]) for r in cur.fetchall()}
    for code, expected_name in required_accounts.items():
        if code in found:
            name, active = found[code]
            if active:
                chk_ok(f"Account {code} '{name}' — EXISTS and ACTIVE")
            else:
                chk_fail(f"Account {code} '{name}' — EXISTS but INACTIVE!")
        else:
            chk_fail(f"Account {code} '{expected_name}' — MISSING from Chart of Accounts!")

    # ── CHECK 3: No Orphaned Journal Lines ────────────────────────────────────
    section("CHECK 3: Orphaned Journal Lines")
    cur.execute("""
        SELECT COUNT(*) FROM journal_entry_lines jl
        WHERE NOT EXISTS (SELECT 1 FROM journal_entries je WHERE je.id = jl.journal_entry_id);
    """)
    orphaned_lines = cur.fetchone()[0]
    if orphaned_lines == 0:
        chk_ok("No orphaned journal lines found")
    else:
        chk_fail(f"{orphaned_lines} orphaned journal lines (no parent journal_entry)!")

    # ── CHECK 4: No Journal Lines with NULL Account ────────────────────────────
    section("CHECK 4: Journal Lines with NULL or Invalid Account")
    cur.execute("""
        SELECT COUNT(*) FROM journal_entry_lines jl
        WHERE jl.account_id IS NULL;
    """)
    null_accounts = cur.fetchone()[0]
    if null_accounts == 0:
        chk_ok("No journal lines with NULL account_id")
    else:
        chk_fail(f"{null_accounts} journal lines with NULL account_id!")

    cur.execute("""
        SELECT COUNT(*) FROM journal_entry_lines jl
        WHERE NOT EXISTS (SELECT 1 FROM accounts a WHERE a.id = jl.account_id);
    """)
    invalid_accounts = cur.fetchone()[0]
    if invalid_accounts == 0:
        chk_ok("All journal line accounts exist in Chart of Accounts")
    else:
        chk_fail(f"{invalid_accounts} journal lines reference non-existent accounts!")

    # ── CHECK 5: Account Type Validation ──────────────────────────────────────
    section("CHECK 5: Account Type Consistency")
    cur.execute("""
        SELECT account_type, COUNT(*) FROM accounts WHERE is_active = true GROUP BY account_type;
    """)
    type_counts = {r[0]: r[1] for r in cur.fetchall()}
    expected_types = ["ASSET", "LIABILITY", "EQUITY", "REVENUE", "EXPENSE"]
    for t in expected_types:
        if t in type_counts:
            chk_ok(f"Account type {t}: {type_counts[t]} accounts")
        else:
            chk_warn(f"Account type {t}: NO accounts of this type!")

    # ── CHECK 6: GST Calculation Spot Check ───────────────────────────────────
    section("CHECK 6: GST Calculation Verification (spot check last 5 taxable invoices)")
    cur.execute("""
        SELECT je.entry_number, je.description,
            SUM(CASE WHEN a.account_code IN ('22010') THEN jl.credit_amount ELSE 0 END) AS cgst,
            SUM(CASE WHEN a.account_code IN ('22020') THEN jl.credit_amount ELSE 0 END) AS sgst,
            SUM(CASE WHEN a.account_type = 'REVENUE' THEN jl.credit_amount ELSE 0 END)  AS revenue,
            SUM(CASE WHEN a.account_type = 'ASSET' THEN jl.debit_amount ELSE 0 END)     AS cash_dr
        FROM journal_entries je
        JOIN journal_entry_lines jl ON jl.journal_entry_id = je.id
        JOIN accounts a ON a.id = jl.account_id
        WHERE je.description ILIKE '%POS Invoice%'
        GROUP BY je.id, je.entry_number, je.description
        HAVING SUM(CASE WHEN a.account_code IN ('22010') THEN jl.credit_amount ELSE 0 END) > 0
        ORDER BY je.created_at DESC
        LIMIT 5;
    """)
    gst_rows = cur.fetchall()
    if not gst_rows:
        chk_warn("No GST-applicable invoices found for spot check")
    else:
        for row in gst_rows:
            je_num, desc, cgst, sgst, revenue, cash = [row[0], row[1]] + [float(x) for x in row[2:]]
            # Verify: CGST ≈ SGST (equal split)
            if abs(cgst - sgst) < 0.02:
                chk_ok(f"{je_num}: CGST={cgst:.2f} SGST={sgst:.2f} (equal split ✓) | Revenue={revenue:.2f} | Cash={cash:.2f}")
            else:
                chk_fail(f"{je_num}: CGST={cgst:.2f} ≠ SGST={sgst:.2f} (unequal split!)")
            # Verify: revenue + cgst + sgst ≈ cash collected
            expected_cash = revenue + cgst + sgst
            if abs(expected_cash - cash) < 0.02:
                chk_ok(f"  → Revenue({revenue:.2f}) + CGST({cgst:.2f}) + SGST({sgst:.2f}) = {expected_cash:.2f} ≈ Cash({cash:.2f}) ✓")
            else:
                chk_fail(f"  → Cash mismatch: Expected={expected_cash:.2f} Actual={cash:.2f}")

    # ── CHECK 7: Trial Balance (Assets = Liabilities + Equity) ────────────────
    section("CHECK 7: Trial Balance Sanity")
    cur.execute("""
        SELECT
            SUM(CASE WHEN a.account_type = 'ASSET'     THEN jl.debit_amount - jl.credit_amount ELSE 0 END) AS asset_balance,
            SUM(CASE WHEN a.account_type = 'LIABILITY'  THEN jl.credit_amount - jl.debit_amount ELSE 0 END) AS liability_balance,
            SUM(CASE WHEN a.account_type = 'EQUITY'    THEN jl.credit_amount - jl.debit_amount ELSE 0 END) AS equity_balance,
            SUM(CASE WHEN a.account_type = 'REVENUE'   THEN jl.credit_amount - jl.debit_amount ELSE 0 END) AS revenue_balance,
            SUM(CASE WHEN a.account_type = 'EXPENSE'   THEN jl.debit_amount - jl.credit_amount ELSE 0 END) AS expense_balance
        FROM journal_entry_lines jl
        JOIN accounts a ON a.id = jl.account_id;
    """)
    tb = cur.fetchone()
    if tb and any(v is not None for v in tb):
        assets, liab, equity, revenue, expense = [float(v or 0) for v in tb]
        net_income = revenue - expense
        total_left = assets
        total_right = liab + equity + net_income
        print(f"  Assets:    ₹{assets:>12.2f}")
        print(f"  Liab:      ₹{liab:>12.2f}")
        print(f"  Equity:    ₹{equity:>12.2f}")
        print(f"  Revenue:   ₹{revenue:>12.2f}")
        print(f"  Expense:   ₹{expense:>12.2f}")
        print(f"  Net Income:₹{net_income:>12.2f}")
        print(f"  ─────────────────────────────────")
        print(f"  LHS (Assets):               ₹{total_left:>12.2f}")
        print(f"  RHS (Liab+Equity+NetIncome):₹{total_right:>12.2f}")
        if abs(total_left - total_right) < 1.0:
            chk_ok(f"Trial Balance: LHS ≈ RHS (diff < ₹1 — acceptable rounding)")
        else:
            chk_warn(f"Trial Balance: LHS({total_left:.2f}) ≠ RHS({total_right:.2f}) — diff={abs(total_left-total_right):.2f}")
    else:
        chk_warn("Trial balance: No journal data found")

    # ── CHECK 8: Loyalty Config Exists ────────────────────────────────────────
    section("CHECK 8: Loyalty Program Configuration")
    cur.execute("SELECT earn_ratio_spend_amount, earn_ratio_points, is_active_config FROM loyalty_program_configs LIMIT 1;")
    lpc = cur.fetchone()
    if lpc:
        spend, pts, active = float(lpc[0]), float(lpc[1]), lpc[2]
        if active:
            chk_ok(f"Loyalty config: Spend ₹{spend} → Earn {pts} point(s) | Status=ACTIVE")
        else:
            chk_fail(f"Loyalty config exists but is_active_config=False!")
    else:
        chk_fail("Loyalty program config is MISSING — points will not be awarded!")

    conn.close()

    # ── SUMMARY ───────────────────────────────────────────────────────────────
    section("LAYER 4 SUMMARY")
    print(f"  Checks Run   : {results['checks']}")
    print(f"  {C.PASS}PASSED{C.RESET}       : {results['passed']}")
    print(f"  {C.FAIL}FAILED{C.RESET}       : {results['failed']}")
    print(f"  {C.WARN}WARNINGS{C.RESET}     : {results['warnings']}")

    if results["issues"]:
        print(f"\n  {C.FAIL}{C.BOLD}Accounting Issues (must fix before go-live):{C.RESET}")
        for issue in results["issues"]:
            print(f"    ✗ {issue[:100]}")

    return results


if __name__ == "__main__":
    run()
