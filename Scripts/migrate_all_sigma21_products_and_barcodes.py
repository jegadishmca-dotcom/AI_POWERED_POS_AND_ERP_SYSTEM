import os
import sys
import uuid
import pyodbc
import psycopg2
from psycopg2.extras import execute_values

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

NAMESPACE_SIGMA = uuid.UUID('6ba7b810-9dad-11d1-80b4-00c04fd430c8')

def to_uuid(prefix: str, key: str) -> str:
    return str(uuid.uuid5(NAMESPACE_SIGMA, f"{prefix}:{key.strip().lower()}"))

def get_mssql_conn():
    env = {}
    with open('.env.mssql', 'r', encoding='utf-8') as f:
        for line in f:
            line = line.strip()
            if '=' in line and not line.startswith('#'):
                k, v = line.split('=', 1)
                env[k] = v
    conn_str = f"DRIVER={{SQL Server}};SERVER={env['MSSQL_SERVER']};DATABASE=APPLE26-27;UID={env['MSSQL_USER']};PWD={env['MSSQL_PASSWORD']};"
    return pyodbc.connect(conn_str, timeout=30)

def get_pg_conn(db_name: str):
    from tests.config import DB_CONFIG
    cfg = DB_CONFIG.copy()
    cfg['port'] = 6432
    cfg['user'] = 'posadmin'
    cfg['password'] = '8t-MT5oPvmC2ERg6a2rs8n4kL52XiF1s'
    cfg['database'] = db_name
    return psycopg2.connect(**cfg)

def migrate_to_database(db_name: str, ms_conn):
    print(f"\n{'='*70}")
    print(f"MIGRATING SIGMA21 PRODUCT & BARCODE MASTER -> {db_name}")
    print(f"{'='*70}")

    pg_conn = get_pg_conn(db_name)
    pg_conn.autocommit = False
    pg_cur = pg_conn.cursor()
    ms_cur = ms_conn.cursor()

    # 1. Resolve Store
    pg_cur.execute("SELECT id FROM stores WHERE is_deleted = false ORDER BY created_at ASC LIMIT 1;")
    s_row = pg_cur.fetchone()
    if s_row:
        store_id = str(s_row[0])
    else:
        store_id = "00000000-0000-0000-0000-000000000000"
        pg_cur.execute("""
            INSERT INTO stores (id, store_code, store_name, is_active, created_at)
            VALUES (%s, 'STORE-01', 'Apple Supermarket Head Office', true, NOW())
            ON CONFLICT (id) DO NOTHING;
        """, (store_id,))
    print(f"  Using Store ID: {store_id}")

    # 2. Foundational Masters: Tax Slabs
    tax_rates = [
        (0.0, 'GST 0%', 0.0, 0.0),
        (5.0, 'GST 5%', 2.5, 2.5),
        (12.0, 'GST 12%', 6.0, 6.0),
        (18.0, 'GST 18%', 9.0, 9.0),
        (28.0, 'GST 28%', 14.0, 14.0)
    ]
    tax_map = {}
    for rate, name, cgst, sgst in tax_rates:
        tid = to_uuid("tax_slab", f"gst_{int(rate)}")
        pg_cur.execute("""
            INSERT INTO tax_slabs (id, store_id, name, cgst_rate, sgst_rate, igst_rate, cess_rate, is_deleted, created_at)
            VALUES (%s, %s, %s, %s, %s, %s, 0.0, false, NOW())
            ON CONFLICT (id) DO UPDATE SET cgst_rate = EXCLUDED.cgst_rate, sgst_rate = EXCLUDED.sgst_rate
            RETURNING id;
        """, (tid, store_id, name, cgst, sgst, rate))
        tax_map[int(rate)] = tid
    print(f"  Tax Slabs synced ({len(tax_map)} slabs).")

    # 3. Foundational Masters: UOMs
    uoms = [
        ("Pieces", "Pcs"),
        ("Kilograms", "Kgs"),
        ("Boxes", "Box"),
        ("Litres", "Ltrs"),
        ("Packets", "Pack"),
        ("Grams", "Gms"),
        ("Dozen", "DOZ"),
        ("Set", "SET")
    ]
    uom_map = {}
    for name, sym in uoms:
        uid = to_uuid("uom", sym.lower())
        pg_cur.execute("""
            INSERT INTO unit_of_measures (id, store_id, name, symbol, is_deleted, created_at)
            VALUES (%s, %s, %s, %s, false, NOW())
            ON CONFLICT (id) DO UPDATE SET name = EXCLUDED.name, symbol = EXCLUDED.symbol
            RETURNING id;
        """, (uid, store_id, name, sym))
        uom_map[sym.lower()] = uid
        uom_map[name.lower()] = uid
    default_uom_id = uom_map['pcs']
    print(f"  Unit of Measures synced.")

    # 4. Foundational Masters: Categories & Brands from MS SQL
    ms_cur.execute("SELECT DISTINCT LTRIM(RTRIM(Category)) FROM Master_Inventory_Product WHERE Category IS NOT NULL AND LTRIM(RTRIM(Category)) <> '';")
    cat_map = {}
    for row in ms_cur.fetchall():
        cname = row[0].strip()
        cid = to_uuid("category", cname)
        pg_cur.execute("""
            INSERT INTO categories (id, store_id, name, is_deleted, created_at)
            VALUES (%s, %s, %s, false, NOW())
            ON CONFLICT (id) DO UPDATE SET name = EXCLUDED.name;
        """, (cid, store_id, cname))
        cat_map[cname.lower()] = cid

    default_cat_id = to_uuid("category", "general_fmcg")
    pg_cur.execute("""
        INSERT INTO categories (id, store_id, name, is_deleted, created_at)
        VALUES (%s, %s, 'General / FMCG', false, NOW())
        ON CONFLICT (id) DO UPDATE SET name = EXCLUDED.name;
    """, (default_cat_id, store_id))
    cat_map['general'] = default_cat_id

    ms_cur.execute("SELECT DISTINCT LTRIM(RTRIM(Company)) FROM Master_Inventory_Product WHERE Company IS NOT NULL AND LTRIM(RTRIM(Company)) <> '';")
    brand_map = {}
    for row in ms_cur.fetchall():
        bname = row[0].strip()
        bid = to_uuid("brand", bname)
        pg_cur.execute("""
            INSERT INTO brands (id, store_id, name, is_deleted, created_at)
            VALUES (%s, %s, %s, false, NOW())
            ON CONFLICT (id) DO UPDATE SET name = EXCLUDED.name;
        """, (bid, store_id, bname))
        brand_map[bname.lower()] = bid

    pg_conn.commit()
    print(f"  Categories ({len(cat_map)}) and Brands ({len(brand_map)}) synced.")

    # 5. Extract & Stream All Products from Master_Inventory_Product
    print("  Streaming Products from Sigma21 Master_Inventory_Product...", flush=True)
    ms_cur.execute("""
        WITH LatestBatch AS (
            SELECT 
                ProductName,
                MRP,
                SalesRate1,
                PurchaseRate,
                ROW_NUMBER() OVER (PARTITION BY ProductName ORDER BY ID DESC) as rnk
            FROM Master_Batch
            WHERE Status = 1
        )
        SELECT 
            p.ID AS ProductCode,
            LTRIM(RTRIM(p.Name)) AS Name,
            ISNULL(LTRIM(RTRIM(p.TamilName)), N'') AS TamilName,
            ISNULL(LTRIM(RTRIM(p.Category)), N'') AS Category,
            ISNULL(LTRIM(RTRIM(p.Company)), N'') AS Company,
            ISNULL(LTRIM(RTRIM(p.HSNCode)), N'') AS HSNCode,
            CASE 
                WHEN ISNULL(b.MRP, 0) > 0 THEN CAST(b.MRP AS DECIMAL(18,2))
                WHEN ISNULL(p.PMRP, 0) > 0 THEN CAST(p.PMRP AS DECIMAL(18,2))
                ELSE 1.00 
            END AS Mrp,
            CASE 
                WHEN ISNULL(b.SalesRate1, 0) > 0 THEN CAST(b.SalesRate1 AS DECIMAL(18,2))
                WHEN ISNULL(p.Rate1, 0) > 0 THEN CAST(p.Rate1 AS DECIMAL(18,2))
                WHEN ISNULL(b.MRP, 0) > 0 THEN CAST(b.MRP AS DECIMAL(18,2))
                ELSE 1.00 
            END AS SellingPrice,
            CASE 
                WHEN ISNULL(b.PurchaseRate, 0) > 0 THEN CAST(b.PurchaseRate AS DECIMAL(18,2))
                WHEN ISNULL(p.PPurchaseRate, 0) > 0 THEN CAST(p.PPurchaseRate AS DECIMAL(18,2))
                ELSE 0.00 
            END AS PurchasePrice,
            ISNULL(g.Percentage, 0) AS GstPercentage,
            CASE 
                WHEN p.Weight > 0 
                  OR p.Name LIKE '%VELLAM%' OR p.Name LIKE '%RICE%' OR p.Name LIKE '%PARUPPU%' OR p.Name LIKE '%SUGAR%'
                  OR p.Name LIKE '%DHAL%' OR p.Name LIKE '%DAL%' OR p.Name LIKE '%ATTA%' OR p.Name LIKE '%MAIDA%' OR p.Name LIKE '%RAVA%'
                  OR p.Name LIKE '%KG%' OR p.Name LIKE '%1K%' OR p.Name LIKE '%2K%' OR p.Name LIKE '%5K%' OR p.Name LIKE '%10K%' OR p.Name LIKE '%25K%'
                  OR p.Name LIKE '%500G%' OR p.Name LIKE '%250G%' OR p.Name LIKE '%100G%' OR p.Name LIKE '%50G%' OR p.Name LIKE '%GRAM%' OR p.Name LIKE '%GRM%'
                  OR p.Name LIKE '%KILO%' OR p.Name LIKE '%LOOSE%' OR p.Name LIKE '%OIL%' OR p.Name LIKE '%GHEE%' OR p.Name LIKE '%SALT%'
                THEN 1
                ELSE 0
            END AS IsWeighable,
            CASE 
                WHEN p.Weight > 0 
                  OR p.Name LIKE '%VELLAM%' OR p.Name LIKE '%RICE%' OR p.Name LIKE '%PARUPPU%' OR p.Name LIKE '%SUGAR%'
                  OR p.Name LIKE '%DHAL%' OR p.Name LIKE '%DAL%' OR p.Name LIKE '%ATTA%' OR p.Name LIKE '%MAIDA%' OR p.Name LIKE '%RAVA%'
                  OR p.Name LIKE '%KG%' OR p.Name LIKE '%1K%' OR p.Name LIKE '%2K%' OR p.Name LIKE '%5K%' OR p.Name LIKE '%10K%' OR p.Name LIKE '%25K%'
                  OR p.Name LIKE '%500G%' OR p.Name LIKE '%250G%' OR p.Name LIKE '%100G%' OR p.Name LIKE '%50G%' OR p.Name LIKE '%GRAM%' OR p.Name LIKE '%GRM%'
                  OR p.Name LIKE '%KILO%' OR p.Name LIKE '%LOOSE%' OR p.Name LIKE '%OIL%' OR p.Name LIKE '%GHEE%' OR p.Name LIKE '%SALT%'
                THEN 'kgs'
                WHEN p.Box = 1 THEN 'box'
                ELSE 'pcs'
            END AS Uom,
            ISNULL(p.Stock, 50.0) AS CurrentStock
        FROM Master_Inventory_Product p
        LEFT JOIN LatestBatch b ON b.ProductName = p.ID AND b.rnk = 1
        LEFT JOIN Master_Base_GST g ON p.GSTInterStateOutput = g.ID;
    """)

    insert_prod_sql = """
        INSERT INTO products (
            id, store_id, product_code, name, tamil_name, hsn_code,
            is_weighable, mrp, selling_price, purchase_price, current_stock,
            is_active, is_deleted, unit_of_measure_id, tax_slab_id, category_id, brand_id,
            created_at
        ) VALUES %s
        ON CONFLICT (store_id, product_code) DO UPDATE SET
            name = EXCLUDED.name,
            tamil_name = EXCLUDED.tamil_name,
            hsn_code = EXCLUDED.hsn_code,
            mrp = EXCLUDED.mrp,
            selling_price = EXCLUDED.selling_price,
            purchase_price = EXCLUDED.purchase_price,
            is_weighable = EXCLUDED.is_weighable,
            tax_slab_id = EXCLUDED.tax_slab_id,
            category_id = EXCLUDED.category_id,
            brand_id = EXCLUDED.brand_id,
            current_stock = GREATEST(products.current_stock, EXCLUDED.current_stock),
            is_active = true,
            is_deleted = false,
            updated_at = NOW();
    """

    prod_rows = []
    seen_pcodes = set()
    total_prods = 0

    while True:
        rows = ms_cur.fetchmany(5000)
        if not rows:
            break
        for r in rows:
            pcode = str(r[0]).strip()[:50]
            pname = str(r[1]).strip()[:200]
            if not pcode or not pname or pcode in seen_pcodes:
                continue
            seen_pcodes.add(pcode)

            pid = to_uuid("product", pcode)
            tname = str(r[2] or '').strip()[:200]
            cat_name = str(r[3] or '').strip().lower()
            cid = cat_map.get(cat_name, default_cat_id)
            brand_name = str(r[4] or '').strip().lower()
            bid = brand_map.get(brand_name)
            hsn = str(r[5] or '').strip()[:20]

            mrp = float(r[6] or 1.0)
            sprice = float(r[7] or 1.0)
            cprice = float(r[8] or 0.0)

            # Enforce constraints: MRP > 0, SP > 0, SP <= MRP, PurchasePrice >= 0
            if mrp <= 0: mrp = 1.0
            if sprice <= 0: sprice = mrp
            if sprice > mrp: sprice = mrp
            if cprice < 0: cprice = 0.0

            gst_pct = int(float(r[9] or 0))
            tid = tax_map.get(gst_pct, tax_map[0])
            is_weighable = bool(r[10])
            uom_sym = str(r[11] or 'pcs').lower()
            uid = uom_map.get(uom_sym, default_uom_id)
            stock = max(float(r[12] or 0.0), 50.0)

            prod_rows.append((
                pid, store_id, pcode, pname, tname, hsn,
                is_weighable, mrp, sprice, cprice, stock, True, False,
                uid, tid, cid, bid
            ))

        if prod_rows:
            template_p = "(%s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, NOW())"
            execute_values(pg_cur, insert_prod_sql, prod_rows, template=template_p)
            pg_conn.commit()
            total_prods += len(prod_rows)
            print(f"    Streamed {total_prods} products...", flush=True)
            prod_rows = []

    print(f"  [OK] Product Catalog complete ({total_prods} products inserted/updated).")

    # Re-fetch actual product IDs from PostgreSQL to guarantee 100% foreign key match
    pg_cur.execute("SELECT product_code, id, mrp, purchase_price FROM products WHERE store_id = %s;", (store_id,))
    code_to_prod_info = {r[0]: (str(r[1]), float(r[2]), float(r[3])) for r in pg_cur.fetchall()}

    # 6. Stream Initial Batches for all products
    print("  Ensuring Product Batches exist for active billing...", flush=True)
    insert_batch_sql = """
        INSERT INTO product_batches (
            id, store_id, product_id, batch_number, mrp, cost_price,
            available_quantity, is_active, created_at
        ) VALUES %s
        ON CONFLICT (id) DO UPDATE SET
            available_quantity = GREATEST(product_batches.available_quantity, EXCLUDED.available_quantity),
            is_active = true;
    """
    batch_rows = []
    total_batches = 0
    for pcode, (pid, mrp, cprice) in code_to_prod_info.items():
        batch_id = to_uuid("batch", f"{store_id}_{pcode}")
        batch_rows.append((
            batch_id, store_id, pid, f"BAT-{pcode}", mrp, cprice, 50.0, True
        ))
        if len(batch_rows) >= 5000:
            template_b = "(%s, %s, %s, %s, %s, %s, %s, %s, NOW())"
            execute_values(pg_cur, insert_batch_sql, batch_rows, template=template_b)
            pg_conn.commit()
            total_batches += len(batch_rows)
            print(f"    Streamed {total_batches} batches...", flush=True)
            batch_rows = []

    if batch_rows:
        template_b = "(%s, %s, %s, %s, %s, %s, %s, %s, NOW())"
        execute_values(pg_cur, insert_batch_sql, batch_rows, template=template_b)
        pg_conn.commit()
        total_batches += len(batch_rows)
        print(f"    Streamed {total_batches} batches...", flush=True)

    # 7. Stream ALL Barcodes from ALL 4 Sigma21 Sources
    print("  Streaming ALL Barcodes from Sigma21 (MIP.ShortName, FMCG.AID, MultiShortName, BatchNo)...", flush=True)

    ms_cur.execute("""
        WITH UnifiedBarcodes AS (
            -- Source A: Master_Inventory_Product.ShortName (Primary)
            SELECT 
                LTRIM(RTRIM(ID)) AS ProductCode,
                LTRIM(RTRIM(ShortName)) AS Barcode,
                1 AS Priority
            FROM Master_Inventory_Product
            WHERE ShortName IS NOT NULL AND LEN(LTRIM(RTRIM(ShortName))) >= 3

            UNION

            -- Source B: Sigma_FMCG_Product via p.AID = s.ID
            SELECT 
                LTRIM(RTRIM(p.ID)) AS ProductCode,
                LTRIM(RTRIM(s.Barcode)) AS Barcode,
                2 AS Priority
            FROM Master_Inventory_Product p
            JOIN Sigma_FMCG_Product s ON p.AID = s.ID
            WHERE s.Barcode IS NOT NULL AND LEN(LTRIM(RTRIM(s.Barcode))) >= 3

            UNION

            -- Source C: Trans_Inventory_Product_MultiShortName (Multi-barcodes)
            SELECT 
                LTRIM(RTRIM(m.ProductID)) AS ProductCode,
                LTRIM(RTRIM(m.ShortName)) AS Barcode,
                3 AS Priority
            FROM Trans_Inventory_Product_MultiShortName m
            JOIN Master_Inventory_Product p ON m.ProductID = p.ID
            WHERE m.ShortName IS NOT NULL AND LEN(LTRIM(RTRIM(m.ShortName))) >= 3

            UNION

            -- Source D: Master_Batch numeric barcodes (>= 8 digits)
            SELECT 
                LTRIM(RTRIM(b.ProductName)) AS ProductCode,
                LTRIM(RTRIM(b.BatchNo)) AS Barcode,
                4 AS Priority
            FROM Master_Batch b
            JOIN Master_Inventory_Product p ON b.ProductName = p.ID
            WHERE b.BatchNo IS NOT NULL AND LEN(LTRIM(RTRIM(b.BatchNo))) >= 8 AND ISNUMERIC(LTRIM(RTRIM(b.BatchNo))) = 1
        )
        SELECT ProductCode, Barcode, Priority FROM UnifiedBarcodes ORDER BY Priority ASC;
    """)

    insert_bcode_sql = """
        INSERT INTO barcodes (id, store_id, product_id, barcode, is_primary, created_at, is_deleted)
        VALUES %s
        ON CONFLICT (store_id, barcode) DO UPDATE SET
            product_id = EXCLUDED.product_id,
            is_primary = EXCLUDED.is_primary,
            is_deleted = false;
    """

    # Pre-populate seen_barcodes with existing barcodes in the DB
    pg_cur.execute("SELECT barcode FROM barcodes WHERE store_id = %s;", (store_id,))
    seen_barcodes = {str(r[0]).strip().lower() for r in pg_cur.fetchall()}
    print(f"    Pre-existing barcodes in DB: {len(seen_barcodes)}")

    bcode_rows = []
    total_bcodes = 0

    while True:
        rows = ms_cur.fetchmany(5000)
        if not rows:
            break
        for r in rows:
            pcode = str(r[0]).strip()
            bcode = str(r[1]).strip()
            priority = int(r[2])
            bcode_norm = bcode.lower()

            if not bcode or bcode_norm in seen_barcodes:
                continue

            target_info = code_to_prod_info.get(pcode)
            if not target_info:
                continue

            target_pid = target_info[0]
            seen_barcodes.add(bcode_norm)
            bid = str(uuid.uuid4())
            is_primary = (priority == 1)

            bcode_rows.append((bid, store_id, target_pid, bcode, is_primary))

        if bcode_rows:
            template_bc = "(%s, %s, %s, %s, %s, NOW(), false)"
            execute_values(pg_cur, insert_bcode_sql, bcode_rows, template=template_bc)
            pg_conn.commit()
            total_bcodes += len(bcode_rows)
            print(f"    Streamed {total_bcodes} barcodes...", flush=True)
            bcode_rows = []

    print(f"  [OK] Barcodes complete ({total_bcodes} barcodes inserted/updated).")
    pg_cur.close()
    pg_conn.close()
    print(f"  >>> MIGRATION TO {db_name} COMPLETED SUCCESSFULLY! <<<\n")

def main():
    print("Connecting to Sigma21 MS SQL on 192.168.1.10...")
    ms_conn = get_mssql_conn()
    print("Connected to MS SQL successfully!")

    # 1. Migrate to posdb_live (Production Live Database)
    migrate_to_database("posdb_live", ms_conn)

    # 2. Migrate to posdb_uat (UAT / Dev Database)
    migrate_to_database("posdb_uat", ms_conn)

    ms_conn.close()
    print("\nALL DATABASES FULLY SYNCHRONIZED WITH SIGMA21!")

if __name__ == '__main__':
    main()
