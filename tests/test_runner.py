#!/usr/bin/env python3
# =============================================================================
# tests/test_runner.py — MASTER TEST RUNNER
#
# Runs all 4 layers in sequence and produces a final bug report.
#
# Usage:
#   python tests/test_runner.py            — Run all layers
#   python tests/test_runner.py --layer 1  — Run only Layer 1 (Schema)
#   python tests/test_runner.py --layer 2  — Run only Layer 2 (API Smoke)
#   python tests/test_runner.py --layer 3  — Run only Layer 3 (Workflows)
#   python tests/test_runner.py --layer 4  — Run only Layer 4 (Accounting)
#   python tests/test_runner.py --html     — Also generate HTML report
# =============================================================================

import sys, os, time, argparse, datetime, requests

# Allow imports from tests/
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from config import C, header, section, TEST_RUN_ID, TEST_PREFIX, DB_CONFIG, API_BASE_URL, ADMIN_USER

def banner():
    print(f"""
{C.BOLD}{C.HEADER}
==================================================================
    APPLE SUPERMARKET ERP -- AUTOMATED TEST RUNNER
    4-Layer System-Wide Bug Detection Suite
=================================================================={C.RESET}
  {C.INFO}Run Date:{C.RESET}  {datetime.datetime.now().strftime('%Y-%m-%d %H:%M:%S')}
  {C.INFO}Test Run ID:{C.RESET} {TEST_RUN_ID}  (stamped into every invoice created this run)
  {C.INFO}Invoice Prefix:{C.RESET} {TEST_PREFIX}-*
  {C.INFO}Target DB:{C.RESET}  {DB_CONFIG['database']} @ {DB_CONFIG['host']}
  {C.INFO}API:{C.RESET}       {API_BASE_URL}
  {C.INFO}Layers:{C.RESET}    Schema Scanner | API Smoke | Workflows | Accounting
""")


def preflight_uat_guard() -> bool:
    """Abort with a loud warning if the backend is currently in LIVE mode.
    The automated test suite must only run against the UAT environment."""
    try:
        r = requests.get(
            f"{API_BASE_URL}/api/environment/mode",
            timeout=8,
        )
        if r.status_code == 200:
            mode = r.json().get("activeMode", "").upper()
            if mode == "LIVE":
                print(f"\n{C.FAIL}{C.BOLD}" + "=" * 70)
                print("  ⛔  PRE-FLIGHT GUARD: SERVER IS IN LIVE MODE")
                print("")
                print("  The automated test suite writes test invoices to the database.")
                print("  Running against LIVE will contaminate production data.")
                print("")
                print("  ➜  Switch the server to UAT mode via the admin UI first,")
                print("     then re-run the test suite.")
                print("=" * 70 + C.RESET + "\n")
                return False
            elif mode == "UAT":
                print(f"  {C.PASS}[PRE-FLIGHT]{C.RESET} Server is in UAT mode — safe to proceed.")
                return True
    except Exception as e:
        print(f"  {C.WARN}[PRE-FLIGHT]{C.RESET} Could not verify server mode ({e}). Proceeding with caution.")
    return True  # Unknown mode — don't hard-block, but warn already shown.


def run_all(layers_to_run: list, generate_html: bool, skip_guard: bool = False):
    banner()
    if not skip_guard and not preflight_uat_guard():
        return 1
    start_time = time.time()
    all_results = {}

    if 1 in layers_to_run:
        t0 = time.time()
        import layer1_schema_scanner
        all_results["Layer 1: Schema Scanner"] = layer1_schema_scanner.run()
        all_results["Layer 1: Schema Scanner"]["duration"] = time.time() - t0

    if 2 in layers_to_run:
        t0 = time.time()
        import layer2_api_smoke
        all_results["Layer 2: API Smoke Tests"] = layer2_api_smoke.run()
        all_results["Layer 2: API Smoke Tests"]["duration"] = time.time() - t0

    if 3 in layers_to_run:
        t0 = time.time()
        import layer3_workflows
        all_results["Layer 3: Business Workflows"] = layer3_workflows.run()
        all_results["Layer 3: Business Workflows"]["duration"] = time.time() - t0

    if 4 in layers_to_run:
        t0 = time.time()
        import layer4_accounting
        all_results["Layer 4: Accounting Integrity"] = layer4_accounting.run()
        all_results["Layer 4: Accounting Integrity"]["duration"] = time.time() - t0

    total_time = time.time() - start_time

    # ── Final Report ──────────────────────────────────────────────────────────
    print(f"\n\n{C.BOLD}{C.HEADER}{'═'*70}")
    print("   FINAL TEST REPORT — APPLE SUPERMARKET ERP")
    print(f"{'═'*70}{C.RESET}")
    print(f"  Total Runtime: {total_time:.1f}s\n")

    total_passed = 0
    total_failed = 0
    all_issues = []

    for layer_name, result in all_results.items():
        passed = result.get("passed", 0)
        failed = result.get("failed", 0)
        warnings = result.get("warnings", 0)
        duration = result.get("duration", 0)
        issues = result.get("issues", [])

        total_passed += passed
        total_failed += failed
        all_issues.extend([{"layer": layer_name, **i} if isinstance(i, dict) else {"layer": layer_name, "error": str(i)} for i in issues])

        status_icon = f"{C.PASS}✓ CLEAN{C.RESET}" if failed == 0 else f"{C.FAIL}✗ {failed} ISSUE(S){C.RESET}"
        warn_str = f" | {C.WARN}{warnings} warnings{C.RESET}" if warnings > 0 else ""
        print(f"  {C.BOLD}{layer_name}{C.RESET}")
        print(f"    Status:  {status_icon}{warn_str}")
        print(f"    Passed:  {passed}  |  Failed: {failed}  |  Time: {duration:.1f}s")
        if issues:
            for i in issues[:3]:  # Show first 3 issues per layer
                issue_text = i if isinstance(i, str) else i.get("error", str(i))
                print(f"    {C.FAIL}✗{C.RESET} {str(issue_text)[:80]}")
            if len(issues) > 3:
                print(f"    {C.WARN}  ... and {len(issues)-3} more{C.RESET}")
        print()

    # Overall status
    print(f"{'─'*70}")
    overall_pass = total_failed == 0
    if overall_pass:
        print(f"  {C.PASS}{C.BOLD}OVERALL RESULT: ALL TESTS PASSED — System is healthy! ✓{C.RESET}")
    else:
        print(f"  {C.FAIL}{C.BOLD}OVERALL RESULT: {total_failed} ISSUE(S) FOUND — Action required!{C.RESET}")

    print(f"  Total Passed:  {C.PASS}{total_passed}{C.RESET}")
    print(f"  Total Failed:  {C.FAIL}{total_failed}{C.RESET}")
    print(f"{'═'*70}\n")

    if all_issues and total_failed > 0:
        print(f"{C.BOLD}{C.FAIL}ACTION ITEMS — Fix these issues:{C.RESET}")
        for i, issue in enumerate(all_issues, 1):
            layer = issue.get("layer", "?")
            err = issue.get("error", issue.get("missing", str(issue)))
            cls = issue.get("class", "")
            wf = issue.get("workflow", "")
            context = cls or wf
            print(f"  {i}. [{layer}]{f' ({context})' if context else ''}: {str(err)[:100]}")

    # ── Test Record Cleanup Hint ───────────────────────────────────────────────
    print(f"\n{C.INFO}{'─'*70}")
    print(f"  Test Run ID: {TEST_RUN_ID}")
    print(f"  All invoices created this run are prefixed: {TEST_PREFIX}-")
    print(f"  They are stored in: {DB_CONFIG['database']} @ {DB_CONFIG['host']}")
    print(f"  To inspect them:\n")
    print(f"    SELECT invoice_number, status, total_amount, created_at")
    print(f"    FROM invoices")
    print(f"    WHERE invoice_number LIKE '{TEST_PREFIX}-%'")
    print(f"    ORDER BY created_at;")
    print(f"")
    print(f"  (posdb_uat is refreshed from posdb_live on demand — no manual cleanup needed){C.RESET}")

    # ── Optional HTML Report ───────────────────────────────────────────────────
    if generate_html:
        generate_html_report(all_results, total_passed, total_failed, total_time, all_issues)

    return 0 if overall_pass else 1


def generate_html_report(all_results, total_passed, total_failed, total_time, all_issues):
    """Generate a colour-coded HTML report."""
    report_path = os.path.join(os.path.dirname(os.path.abspath(__file__)), "test_report.html")
    now = datetime.datetime.now().strftime('%Y-%m-%d %H:%M:%S')
    overall_color = "#22c55e" if total_failed == 0 else "#ef4444"
    overall_text = "ALL PASSED" if total_failed == 0 else f"{total_failed} ISSUE(S) FOUND"

    rows = ""
    for layer_name, result in all_results.items():
        p = result.get("passed", 0)
        f = result.get("failed", 0)
        w = result.get("warnings", 0)
        d = result.get("duration", 0)
        color = "#22c55e" if f == 0 else "#ef4444"
        status = "PASS" if f == 0 else "FAIL"
        issues_html = ""
        for issue in result.get("issues", []):
            issue_text = issue if isinstance(issue, str) else issue.get("error", str(issue))
            issues_html += f'<li style="color:#ef4444">{str(issue_text)[:120]}</li>'
        rows += f"""
        <tr>
            <td><b>{layer_name}</b></td>
            <td style="color:{color};font-weight:bold">{status}</td>
            <td style="color:#22c55e">{p}</td>
            <td style="color:{'#ef4444' if f>0 else '#6b7280'}">{f}</td>
            <td style="color:{'#f59e0b' if w>0 else '#6b7280'}">{w}</td>
            <td>{d:.1f}s</td>
            <td><ul style="margin:0;padding-left:16px">{issues_html or '<li style="color:#22c55e">None</li>'}</ul></td>
        </tr>"""

    issue_rows = ""
    for i, issue in enumerate(all_issues, 1):
        err = issue.get("error", str(issue))
        layer = issue.get("layer", "")
        issue_rows += f"<tr><td>{i}</td><td>{layer}</td><td style='color:#ef4444'>{str(err)[:150]}</td></tr>"

    html = f"""<!DOCTYPE html>
<html><head><meta charset="UTF-8"><title>ERP Test Report — {now}</title>
<style>
  body {{font-family:Inter,sans-serif;background:#0f172a;color:#e2e8f0;padding:2rem;}}
  h1 {{color:#f8fafc;font-size:1.8rem;}} h2{{color:#94a3b8;font-size:1.1rem;margin-top:2rem;}}
  .badge {{display:inline-block;padding:6px 18px;border-radius:9999px;font-weight:700;font-size:1.1rem;color:#fff;background:{overall_color};}}
  table {{width:100%;border-collapse:collapse;margin-top:1rem;}}
  th {{background:#1e293b;color:#94a3b8;padding:10px 14px;text-align:left;font-size:.85rem;letter-spacing:.05em;}}
  td {{padding:10px 14px;border-bottom:1px solid #1e293b;font-size:.9rem;vertical-align:top;}}
  tr:hover {{background:#1e293b;}}
  .stat {{display:inline-block;background:#1e293b;border-radius:12px;padding:12px 24px;margin:8px;text-align:center;}}
  .stat-val {{font-size:2rem;font-weight:800;}} .stat-lbl{{color:#64748b;font-size:.8rem;}}
</style></head>
<body>
<h1>Apple Supermarket ERP — Automated Test Report</h1>
<p style="color:#64748b">Generated: {now} | Runtime: {total_time:.1f}s</p>
<p><span class="badge">{overall_text}</span></p>
<div>
  <div class="stat"><div class="stat-val" style="color:#22c55e">{total_passed}</div><div class="stat-lbl">PASSED</div></div>
  <div class="stat"><div class="stat-val" style="color:#ef4444">{total_failed}</div><div class="stat-lbl">FAILED</div></div>
</div>
<h2>Layer Results</h2>
<table><thead><tr><th>Layer</th><th>Status</th><th>Passed</th><th>Failed</th><th>Warnings</th><th>Time</th><th>Issues</th></tr></thead>
<tbody>{rows}</tbody></table>
{"<h2>All Issues (Action Required)</h2><table><thead><tr><th>#</th><th>Layer</th><th>Issue</th></tr></thead><tbody>" + issue_rows + "</tbody></table>" if all_issues else ""}
</body></html>"""

    with open(report_path, "w", encoding="utf-8") as f:
        f.write(html)
    print(f"\n  {C.INFO}HTML Report saved:{C.RESET} {report_path}")
    print(f"  Open in browser: file:///{report_path.replace(chr(92), '/')}")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Apple Supermarket ERP Test Runner")
    parser.add_argument("--layer",      type=int, choices=[1,2,3,4], help="Run only a specific layer (1-4)")
    parser.add_argument("--html",       action="store_true", help="Generate HTML report")
    parser.add_argument("--skip-guard", action="store_true", help="Skip the UAT pre-flight mode check (dangerous — use only in CI against a dedicated UAT instance)")
    args = parser.parse_args()

    layers = [args.layer] if args.layer else [1, 2, 3, 4]
    sys.exit(run_all(layers, args.html, skip_guard=args.skip_guard))
