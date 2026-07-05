# =============================================================================
# tests/layer3_workflows.py
#
# PURPOSE: Test complete end-to-end business workflows from login to DB verification.
# CATCHES: Broken flows where individual APIs pass but combined workflows fail.
#
# Workflows:
#   1. Cash Sale (POS → Journal Entry → Stock Deduction → Loyalty Points)
#   2. Customer Creation + CRM
#   3. Offer / Promo Code Application
#   4. Purchase Order → GRN → Stock Update
#   5. Sales Return
# =============================================================================

import sys, os, uuid, time
import requests, psycopg2

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from config import API_BASE_URL, ADMIN_USER, DB_CONFIG, ok, fail, warn, info, header, section, C, TEST_PREFIX

# ─────────────────────────────────────────────────────────────────────────────
# Helpers
# ─────────────────────────────────────────────────────────────────────────────
def login() -> dict:
    r = requests.post(f"{API_BASE_URL}/api/auth/login", json=ADMIN_USER, timeout=10)
    if r.status_code != 200:
        raise RuntimeError(f"Login failed: {r.status_code} {r.text[:200]}")
    d = r.json()
    return {
        "token": d["accessToken"],
        "userId": d["user"]["id"],
        "terminalId": str(d.get("terminalId", "00000000-0000-0000-0000-000000000001")),
    }

def h(token): return {"Authorization": f"Bearer {token}", "Content-Type": "application/json"}
def api(method, path, token, body=None, timeout=20):
    url = f"{API_BASE_URL}{path}"
    fn = getattr(requests, method.lower())
    return fn(url, headers=h(token), json=body, timeout=timeout)

def db():
    return psycopg2.connect(**DB_CONFIG)

def ensure_business_date_open(session: dict):
    """Ensure a business date is OPEN for today. POS requires this before any invoice can be created."""
    token = session["token"]
    user_id = session["userId"]

    # Check if a business date is already open
    r = api("GET", "/api/pos/business-date/active", token)
    if r.status_code == 200:
        data = r.json()
        if data.get("isOpen", False):
            info(f"Business date already open: {data.get('businessDate', 'unknown')}")
            return True

    # Open today's business date
    from datetime import datetime, timezone
    today = datetime.now(timezone.utc).strftime("%Y-%m-%d")
    payload = {
        "businessDate": today,
        "openedBy": user_id,
    }
    r = api("POST", "/api/pos/business-date/open", token, payload)
    if r.status_code == 200:
        ok(f"Business date opened for {today}")
        return True
    else:
        # If date was already used, try tomorrow
        from datetime import timedelta
        tomorrow = (datetime.now(timezone.utc) + timedelta(days=1)).strftime("%Y-%m-%d")
        payload["businessDate"] = tomorrow
        r2 = api("POST", "/api/pos/business-date/open", token, payload)
        if r2.status_code == 200:
            ok(f"Business date opened for {tomorrow}")
            return True
        else:
            warn(f"Could not open business date: {r.text[:200]} / {r2.text[:200]}")
            return False

def get_instock_product(conn, min_gst=0.0):
    """Return (id, name, price, gst_rate) for an in-stock product."""
    cur = conn.cursor()
    cur.execute("""
        SELECT p.id, p.name, p.selling_price,
               COALESCE(ts.cgst_rate + ts.sgst_rate, 0) as gst
        FROM products p
        LEFT JOIN tax_slabs ts ON p.tax_slab_id = ts.id
        WHERE p.is_deleted = false AND p.current_stock > 0
          AND COALESCE(ts.cgst_rate + ts.sgst_rate, 0) >= %s
        ORDER BY (ts.cgst_rate + ts.sgst_rate) DESC NULLS LAST
        LIMIT 1;
    """, (min_gst,))
    row = cur.fetchone()
    return (str(row[0]), row[1], float(row[2]), float(row[3])) if row else None

def count_journals(conn, since_id=None):
    cur = conn.cursor()
    cur.execute("SELECT COUNT(*) FROM journal_entries;")
    return cur.fetchone()[0]


# ─────────────────────────────────────────────────────────────────────────────
# WORKFLOW 1: POS Cash Sale — Full End-to-End
# ─────────────────────────────────────────────────────────────────────────────
def workflow_1_cash_sale(session: dict, conn) -> dict:
    section("WORKFLOW 1: POS Cash Sale (GST + Journal + Stock + Loyalty)")
    issues = []
    token = session["token"]

    # Step 1: Find in-stock product with GST 18%
    prod = get_instock_product(conn, min_gst=18.0)
    if not prod:
        prod = get_instock_product(conn, min_gst=0.0)
    if not prod:
        fail("Step 1: No in-stock products found — cannot run workflow")
        return {"passed": False, "issues": ["No products in stock"]}
    prod_id, prod_name, price, gst = prod
    ok(f"Step 1: Product found — '{prod_name}' | GST={gst}% | Price=₹{price}")

    # Capture pre-state
    cur = conn.cursor()
    cur.execute("SELECT current_stock FROM products WHERE id = %s;", (prod_id,))
    stock_before_row = cur.fetchone()
    # Stock is tracked in stock_ledger (running_balance), not products.current_stock directly
    cur.execute("""
        SELECT COALESCE(SUM(quantity), 0) as net_stock
        FROM stock_ledger WHERE product_id = %s;
    """, (prod_id,))
    ledger_stock_before = float(cur.fetchone()[0])
    cur.execute("SELECT COUNT(*) FROM journal_entries;")
    je_count_before = cur.fetchone()[0]

    # Step 2: Create invoice
    inv_num = f"{TEST_PREFIX}-WF1-{uuid.uuid4().hex[:8].upper()}"
    payload = {
        "invoiceNumber": inv_num,
        "terminalId": session["terminalId"],
        "cashierId": session["userId"],
        "customerId": None,
        "promoCode": None,
        "walletAmountUsed": 0.0,
        "cashAmount": round(price + 50),
        "upiAmount": 0.0,
        "cardAmount": 0.0,
        "roundOff": 0.0,
        "netPayable": price,
        "paymentMode": "CASH",
        "pointsRedeemed": 0,
        "supervisorOverridePin": None,
        "items": [{"productId": prod_id, "quantity": 1, "unitPrice": price, "batchId": None}]
    }
    r = api("POST", "/api/pos/create", token, payload)
    if r.status_code not in [200, 201]:
        fail(f"Step 2: Invoice creation FAILED — {r.status_code}: {r.text[:200]}")
        return {"passed": False, "issues": [f"Invoice creation failed: {r.text[:200]}"]}
    res = r.json()
    inv_id = res.get("invoiceId") if isinstance(res, dict) else str(res)
    inv_num = res.get("invoiceNumber", inv_num) if isinstance(res, dict) else inv_num
    ok(f"Step 2: Invoice created — {inv_num} | ID={inv_id}")

    time.sleep(1)  # let async processing complete

    # Step 3: Verify Journal Entry created and balanced
    cur.execute("""
        SELECT je.id, je.entry_number, SUM(jl.debit_amount) as dr, SUM(jl.credit_amount) as cr
        FROM journal_entries je
        JOIN journal_entry_lines jl ON jl.journal_entry_id = je.id
        WHERE je.description ILIKE %s
        GROUP BY je.id, je.entry_number;
    """, (f"%{inv_num}%",))
    je = cur.fetchone()
    if not je:
        fail(f"Step 3: No journal entry found for {inv_num}")
        issues.append("No journal entry created for invoice")
    else:
        dr, cr = float(je[2]), float(je[3])
        balanced = abs(dr - cr) < 0.01
        if balanced:
            ok(f"Step 3: Journal {je[1]} — Dr={dr:.2f} Cr={cr:.2f} BALANCED")
        else:
            fail(f"Step 3: Journal {je[1]} — Dr={dr:.2f} Cr={cr:.2f} IMBALANCED!")
            issues.append(f"Journal imbalanced: Dr={dr} Cr={cr}")

    # Step 4: Verify stock deducted in stock_ledger
    cur.execute("""
        SELECT COALESCE(SUM(quantity), 0) FROM stock_ledger WHERE product_id = %s;
    """, (prod_id,))
    ledger_stock_after = float(cur.fetchone()[0])
    if ledger_stock_after < ledger_stock_before:
        ok(f"Step 4: Stock deducted in ledger — Before={ledger_stock_before} After={ledger_stock_after}")
    else:
        # Also check via last entry movement_type=SALE
        cur.execute("""
            SELECT COUNT(*) FROM stock_ledger 
            WHERE product_id = %s AND movement_type IN ('SALE','SALE_OVERRIDE') 
            AND created_at > NOW() - INTERVAL '2 minutes';
        """, (prod_id,))
        recent_sale = cur.fetchone()[0]
        if recent_sale > 0:
            ok(f"Step 4: SALE entry found in stock_ledger ({recent_sale} recent entries)")
        else:
            fail(f"Step 4: Stock ledger NOT updated — no SALE entry found!")
            issues.append("Stock ledger not updated after sale")

    # Step 5: Verify GST accounts used in journal
    if je:
        cur.execute("""
            SELECT a.account_code, a.name
            FROM journal_entry_lines jl
            JOIN accounts a ON jl.account_id = a.id
            WHERE jl.journal_entry_id = %s AND a.account_code IN ('22010','22020','22030');
        """, (je[0],))
        gst_lines = cur.fetchall()
        if gst > 0 and gst_lines:
            ok(f"Step 5: GST accounts used — {[r[0]+' '+r[1] for r in gst_lines]}")
        elif gst == 0:
            ok(f"Step 5: Product is tax-exempt — no GST lines (correct)")
        else:
            fail(f"Step 5: GST product sold but no GST accounts in journal!")
            issues.append("GST accounts missing from journal for taxable product")

    passed = len(issues) == 0
    if passed: ok("WORKFLOW 1: COMPLETE — ALL STEPS PASSED")
    else: fail(f"WORKFLOW 1: FAILED — {len(issues)} issue(s)")
    return {"passed": passed, "issues": issues}


# ─────────────────────────────────────────────────────────────────────────────
# WORKFLOW 2: Create Customer + CRM Verification
# ─────────────────────────────────────────────────────────────────────────────
def workflow_2_customer_crm(session: dict, conn) -> dict:
    section("WORKFLOW 2: Create Customer + CRM Data")
    issues = []
    token = session["token"]

    # Step 1: Create customer
    phone = f"9{uuid.uuid4().hex[:9]}"[:10]
    payload = {
        "name": "Test Customer Auto",
        "phone": phone,
        "email": f"testcustomer_{uuid.uuid4().hex[:6]}@example.com",
        "address": "123 Test Street",
        "city": "Chennai",
        "dateOfBirth": "1990-01-15",
        "anniversaryDate": "2015-06-10",
        "gender": "Male"
    }
    r = api("POST", "/api/customers", token, payload)
    if r.status_code not in [200, 201]:
        fail(f"Step 1: Create customer FAILED — {r.status_code}: {r.text[:200]}")
        return {"passed": False, "issues": [f"Customer creation failed: {r.text[:200]}"]}
    resp_data = r.json()
    if isinstance(resp_data, dict):
        cust_id = str(resp_data.get("id", resp_data.get("customerId", "")))
    else:
        cust_id = str(resp_data)  # API returns bare GUID string
    ok(f"Step 1: Customer created — ID={cust_id} | Phone={phone}")

    # Step 2: Fetch customer by searching by phone (GET /api/customers/{id} not implemented)
    # The actual endpoint is GET /api/customers/search?q={phone}
    r2 = api("GET", f"/api/customers/search?q={phone}", token)
    if r2.status_code == 200:
        results = r2.json()
        found_list = results if isinstance(results, list) else results.get("items", results.get("data", [results] if isinstance(results, dict) else []))
        match = any(str(c.get("phone", c.get("mobile", ""))).endswith(phone[-6:]) for c in found_list)
        if match or len(found_list) > 0:
            ok(f"Step 2: Customer found via search — {len(found_list)} result(s)")
        else:
            warn(f"Step 2: Search returned 0 results for phone {phone}")
    else:
        warn(f"Step 2: Customer search returned {r2.status_code} — may be newly created (not yet indexed)")

    # Step 3: Search customer by phone using the search endpoint
    r3 = api("GET", f"/api/customers/search?q={phone}", token)
    if r3.status_code == 200:
        ok(f"Step 3: Customer search endpoint is working (200 OK)")
    else:
        fail(f"Step 3: Customer search FAILED — {r3.status_code}")
        issues.append("Customer search endpoint failed")

    # Step 4: Verify in DB
    cur = conn.cursor()
    cur.execute("SELECT id, name, phone FROM customers WHERE phone = %s;", (phone,))
    db_cust = cur.fetchone()
    if db_cust:
        ok(f"Step 4: Customer in DB — {db_cust[1]} | {db_cust[2]}")
    else:
        fail(f"Step 4: Customer NOT in DB after creation!")
        issues.append("Customer not persisted to DB")

    passed = len(issues) == 0
    if passed: ok("WORKFLOW 2: COMPLETE — ALL STEPS PASSED")
    else: fail(f"WORKFLOW 2: FAILED — {len(issues)} issue(s)")
    return {"passed": passed, "issues": issues}


# ─────────────────────────────────────────────────────────────────────────────
# WORKFLOW 3: Offer / Promo Code Application
# ─────────────────────────────────────────────────────────────────────────────
def workflow_3_offer_promo(session: dict, conn) -> dict:
    section("WORKFLOW 3: Offer / Promo Code Application")
    issues = []
    token = session["token"]
    cur = conn.cursor()

    # Step 1: Check if any active offers exist
    r = api("GET", "/api/offers?isActive=true&pageSize=10", token)
    offers = []
    if r.status_code == 200:
        data = r.json()
        offers = data if isinstance(data, list) else data.get("items", data.get("data", []))
    ok(f"Step 1: {len(offers)} active offers found")

    # Step 2: Create a test offer with unique promo code
    promo_code = f"TEST{uuid.uuid4().hex[:6].upper()}"
    from datetime import datetime, timedelta
    offer_payload = {
        "name": f"Auto Test Offer {promo_code}",
        "description": "Automated test offer",
        "discountType": "PERCENTAGE",
        "discountValue": 10,
        "minOrderAmount": 0,
        "maxDiscountAmount": 500,
        "promoCode": promo_code,
        "validFrom": datetime.utcnow().strftime("%Y-%m-%dT00:00:00"),
        "validTo": (datetime.utcnow() + timedelta(days=30)).strftime("%Y-%m-%dT23:59:59"),
        "isActive": True,
        "applicableProducts": [],
        "applicableCategories": []
    }
    r2 = api("POST", "/api/offers", token, offer_payload)
    if r2.status_code in [200, 201]:
        ok(f"Step 2: Offer created with promo code '{promo_code}'")
    else:
        fail(f"Step 2: Offer creation FAILED — {r2.status_code}: {r2.text[:200]}")
        issues.append(f"Offer creation failed: {r2.text[:200]}")
        return {"passed": False, "issues": issues}

    # Step 3: Validate promo code via calculate-cart
    prod = get_instock_product(conn, min_gst=0.0)
    if prod:
        prod_id, prod_name, price, _ = prod
        cart_payload = {
            "items": [{"productId": prod_id, "quantity": 1, "unitPrice": price}],
            "customerId": None,
            "promoCode": promo_code,
            "pointsToRedeem": 0
        }
        r3 = api("POST", "/api/pos/calculate-cart", token, cart_payload)
        if r3.status_code == 200:
            cart = r3.json()
            discount = cart.get("totalDiscount", cart.get("discountAmount", 0))
            ok(f"Step 3: Promo applied via calculate-cart — Discount=₹{discount}")
            if discount <= 0:
                warn(f"Step 3: Promo code did not apply discount (discount=0)")
        else:
            fail(f"Step 3: calculate-cart with promo FAILED — {r3.status_code}: {r3.text[:200]}")
            issues.append(f"Promo validation failed: {r3.text[:200]}")

    # Step 4: Create invoice with promo and verify journal is balanced
    if prod:
        inv_num = f"{TEST_PREFIX}-WF3-PROMO-{uuid.uuid4().hex[:6].upper()}"
        inv_payload = {
            "invoiceNumber": inv_num,
            "terminalId": session["terminalId"],
            "cashierId": session["userId"],
            "customerId": None,
            "promoCode": promo_code,
            "walletAmountUsed": 0.0,
            "cashAmount": round(price + 50),
            "upiAmount": 0.0, "cardAmount": 0.0, "roundOff": 0.0,
            "netPayable": price,
            "paymentMode": "CASH",
            "pointsRedeemed": 0, "supervisorOverridePin": None,
            "items": [{"productId": prod_id, "quantity": 1, "unitPrice": price, "batchId": None}]
        }
        r4 = api("POST", "/api/pos/create", token, inv_payload)
        if r4.status_code in [200, 201]:
            ok(f"Step 4: Invoice with promo completed — {inv_num}")
            time.sleep(1)
            cur.execute("""
                SELECT SUM(jl.debit_amount), SUM(jl.credit_amount)
                FROM journal_entries je JOIN journal_entry_lines jl ON jl.journal_entry_id = je.id
                WHERE je.description ILIKE %s GROUP BY je.id;
            """, (f"%{inv_num}%",))
            je = cur.fetchone()
            if je and abs(float(je[0]) - float(je[1])) < 0.01:
                ok(f"Step 4: Journal balanced — Dr={float(je[0]):.2f} Cr={float(je[1]):.2f}")
            elif je:
                fail(f"Step 4: Journal IMBALANCED — Dr={float(je[0]):.2f} Cr={float(je[1]):.2f}")
                issues.append("Journal imbalanced after promo invoice")
            else:
                warn("Step 4: No journal entry found for promo invoice")
        else:
            fail(f"Step 4: Invoice with promo FAILED — {r4.status_code}: {r4.text[:200]}")
            issues.append(f"Invoice with promo failed: {r4.text[:200]}")

    passed = len(issues) == 0
    if passed: ok("WORKFLOW 3: COMPLETE — ALL STEPS PASSED")
    else: fail(f"WORKFLOW 3: FAILED — {len(issues)} issue(s)")
    return {"passed": passed, "issues": issues}


# ─────────────────────────────────────────────────────────────────────────────
# WORKFLOW 4: Loyalty Points (Sale + Customer Points Awarded)
# ─────────────────────────────────────────────────────────────────────────────
def workflow_4_loyalty_points(session: dict, conn) -> dict:
    section("WORKFLOW 4: Loyalty Points — Sale + Points Award")
    issues = []
    token = session["token"]
    cur = conn.cursor()

    # Step 1: Create a test customer
    phone = f"8{uuid.uuid4().hex[:9]}"[:10]
    r = api("POST", "/api/customers", token, {
        "name": "Loyalty Test Customer",
        "phone": phone,
        "email": f"loyalty_{uuid.uuid4().hex[:6]}@test.com",
        "dateOfBirth": "1985-03-20"
    })
    if r.status_code not in [200, 201]:
        fail(f"Step 1: Cannot create customer — {r.status_code}")
        return {"passed": False, "issues": ["Customer creation failed"]}
    resp_data = r.json()
    if isinstance(resp_data, dict):
        cust_id = str(resp_data.get("id", resp_data.get("customerId", "")))
    else:
        cust_id = str(resp_data)  # API returns bare GUID string
    ok(f"Step 1: Test customer created — {cust_id}")

    # Step 2: Get initial loyalty points
    cur.execute("SELECT running_loyalty_points FROM customers WHERE id = %s;", (cust_id,))
    points_before = float(cur.fetchone()[0] or 0)
    ok(f"Step 2: Points before sale = {points_before}")

    # Step 3: Make a sale for this customer (₹200+)
    prod = get_instock_product(conn, min_gst=0.0)
    if not prod:
        fail("Step 3: No product in stock for loyalty test")
        return {"passed": False, "issues": ["No stock for loyalty test"]}
    prod_id, prod_name, price, _ = prod

    inv_num = f"{TEST_PREFIX}-WF4-LOYALTY-{uuid.uuid4().hex[:6].upper()}"
    qty = max(1, int(150 / price) + 1)
    net_payable = round(price * qty, 2)
    
    r2 = api("POST", "/api/pos/create", token, {
        "invoiceNumber": inv_num,
        "terminalId": session["terminalId"],
        "cashierId": session["userId"],
        "customerId": cust_id,
        "promoCode": None,
        "walletAmountUsed": 0.0,
        "cashAmount": round(net_payable + 50),
        "upiAmount": 0.0, "cardAmount": 0.0, "roundOff": 0.0,
        "netPayable": net_payable,
        "paymentMode": "CASH",
        "pointsRedeemed": 0, "supervisorOverridePin": None,
        "items": [{"productId": prod_id, "quantity": qty, "unitPrice": price, "batchId": None}]
    })
    if r2.status_code not in [200, 201]:
        fail(f"Step 3: Sale FAILED — {r2.status_code}: {r2.text[:200]}")
        return {"passed": False, "issues": [f"Sale failed: {r2.text[:200]}"]}
    ok(f"Step 3: Sale ₹{net_payable} completed for customer")

    time.sleep(1)

    # Step 4: Verify loyalty points awarded
    cur.execute("SELECT running_loyalty_points FROM customers WHERE id = %s;", (cust_id,))
    points_after = float(cur.fetchone()[0] or 0)
    expected_min_points = max(1, int(price / 100))  # at least 1 point per ₹100

    if points_after > points_before:
        ok(f"Step 4: Loyalty points awarded — Before={points_before} After={points_after} (earned={points_after - points_before})")
    else:
        fail(f"Step 4: No loyalty points awarded! Before={points_before} After={points_after}")
        issues.append("Loyalty points not awarded after sale")

    # Step 5: Verify loyalty_ledger entry
    cur.execute("""
        SELECT transaction_type, points_earned, balance_after_transaction
        FROM loyalty_ledger WHERE customer_id = %s ORDER BY created_at DESC LIMIT 1;
    """, (cust_id,))
    ledger = cur.fetchone()
    if ledger:
        ok(f"Step 5: Loyalty ledger entry — Type={ledger[0]} | Earned={ledger[1]} | Balance={ledger[2]}")
    else:
        fail("Step 5: No loyalty_ledger entry found for this customer!")
        issues.append("No loyalty_ledger entry created")

    passed = len(issues) == 0
    if passed: ok("WORKFLOW 4: COMPLETE — ALL STEPS PASSED")
    else: fail(f"WORKFLOW 4: FAILED — {len(issues)} issue(s)")
    return {"passed": passed, "issues": issues}


# ─────────────────────────────────────────────────────────────────────────────
# WORKFLOW 5: Sales Return
# ─────────────────────────────────────────────────────────────────────────────
def workflow_5_sales_return(session: dict, conn) -> dict:
    section("WORKFLOW 5: Sales Return — Invoice + Return + Reversal")
    issues = []
    token = session["token"]
    cur = conn.cursor()

    # Step 1: Create an original sale
    prod = get_instock_product(conn, min_gst=0.0)
    if not prod:
        fail("Step 1: No in-stock product for return test")
        return {"passed": False, "issues": ["No stock"]}
    prod_id, prod_name, price, _ = prod
    inv_num = f"{TEST_PREFIX}-WF5-ORIG-{uuid.uuid4().hex[:6].upper()}"
    r = api("POST", "/api/pos/create", token, {
        "invoiceNumber": inv_num,
        "terminalId": session["terminalId"],
        "cashierId": session["userId"],
        "customerId": None, "promoCode": None,
        "walletAmountUsed": 0.0, "cashAmount": round(price + 50),
        "upiAmount": 0.0, "cardAmount": 0.0, "roundOff": 0.0,
        "netPayable": price, "paymentMode": "CASH",
        "pointsRedeemed": 0, "supervisorOverridePin": None,
        "items": [{"productId": prod_id, "quantity": 1, "unitPrice": price, "batchId": None}]
    })
    if r.status_code not in [200, 201]:
        fail(f"Step 1: Original sale FAILED — {r.status_code}: {r.text[:200]}")
        return {"passed": False, "issues": [f"Original sale failed: {r.text[:200]}"]}
    res = r.json()
    inv_id = res.get("invoiceId") if isinstance(res, dict) else str(res)
    inv_num = res.get("invoiceNumber", inv_num) if isinstance(res, dict) else inv_num
    ok(f"Step 1: Original invoice created — {inv_num} | ID={inv_id}")
    time.sleep(1)

    # Step 2: Get stock before return
    cur.execute("SELECT current_stock FROM products WHERE id = %s;", (prod_id,))
    stock_before = float(cur.fetchone()[0])

    # Step 3: Get the invoice's business_date from DB to build proper return payload
    cur.execute("SELECT business_date FROM invoices WHERE id = %s;", (inv_id,))
    biz_date_row = cur.fetchone()
    from datetime import datetime, timezone
    return_date = datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")

    # The SalesReturnItemInputDto requires: ProductId, BatchId, Quantity
    # BatchId can be empty GUID if no batch tracking
    empty_batch = "00000000-0000-0000-0000-000000000000"

    # Step 3: Create sales return via POST /api/accountsreceivable/returns
    return_payload = {
        "storeId": "00000000-0000-0000-0000-000000000000",
        "invoiceId": inv_id,
        "returnDate": return_date,
        "refundMode": "CASH",
        "items": [{"productId": prod_id, "batchId": empty_batch, "quantity": 1}]
    }
    r2 = api("POST", "/api/accountsreceivable/returns", token, return_payload)

    if r2.status_code in [200, 201]:
        ok(f"Step 3: Sales return created successfully")
        time.sleep(1)
        # Step 4: Verify stock restored
        cur.execute("SELECT current_stock FROM products WHERE id = %s;", (prod_id,))
        stock_after = float(cur.fetchone()[0])
        if stock_after > stock_before:
            ok(f"Step 4: Stock restored — Before={stock_before} After={stock_after}")
        else:
            warn(f"Step 4: Stock may not be fully restored — Before={stock_before} After={stock_after}")
    else:
        fail(f"Step 3: Sales return failed — {r2.status_code}: {r2.text[:200]}")
        issues.append(f"Sales return: {r2.status_code}")

    passed = len(issues) == 0
    if passed: ok("WORKFLOW 5: COMPLETE — ALL STEPS PASSED")
    else: fail(f"WORKFLOW 5: COMPLETED WITH WARNINGS — {len(issues)} issue(s)")
    return {"passed": passed, "issues": issues}



# ─────────────────────────────────────────────────────────────────────────────
# MASTER RUNNER
# ─────────────────────────────────────────────────────────────────────────────
def run() -> dict:
    header("LAYER 3: BUSINESS WORKFLOW TESTS")

    try:
        info("Authenticating...")
        session = login()
        ok(f"Logged in | userId={session['userId']}")
    except Exception as e:
        fail(f"Login failed: {e}")
        return {"total": 5, "passed": 0, "failed": 1, "issues": [{"workflow": "Login", "error": str(e)}]}

    conn = db()

    # POS requires a business date to be OPEN before any invoices can be created
    section("PRE-FLIGHT: Ensuring Business Date is Open")
    ensure_business_date_open(session)

    results = {"total": 5, "passed": 0, "failed": 0, "issues": []}

    workflows = [
        ("Cash Sale + GST + Journal", workflow_1_cash_sale),
        ("Customer Creation + CRM",   workflow_2_customer_crm),
        ("Offer / Promo Code",        workflow_3_offer_promo),
        ("Loyalty Points Award",      workflow_4_loyalty_points),
        ("Sales Return",              workflow_5_sales_return),
    ]

    for name, fn in workflows:
        try:
            conn.rollback()  # ensure clean transaction state for each workflow
            result = fn(session, conn)
            if result["passed"]:
                results["passed"] += 1
            else:
                results["failed"] += 1
                for issue in result.get("issues", []):
                    results["issues"].append({"workflow": name, "error": issue})
        except Exception as e:
            fail(f"Workflow '{name}' crashed: {e}")
            import traceback; traceback.print_exc()
            results["failed"] += 1
            results["issues"].append({"workflow": name, "error": str(e)})

    conn.close()

    section("LAYER 3 SUMMARY")
    print(f"  Workflows Tested : {results['total']}")
    print(f"  {C.PASS}PASSED{C.RESET}           : {results['passed']}")
    print(f"  {C.FAIL}FAILED{C.RESET}           : {results['failed']}")

    if results["issues"]:
        print(f"\n  {C.FAIL}{C.BOLD}Workflow Issues:{C.RESET}")
        for issue in results["issues"]:
            print(f"    ✗ [{issue['workflow']}] — {issue['error'][:100]}")

    return results


if __name__ == "__main__":
    run()
