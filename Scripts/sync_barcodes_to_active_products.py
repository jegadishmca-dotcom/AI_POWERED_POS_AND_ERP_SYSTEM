import sys
import os
import uuid
import hashlib
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import psycopg2
import pyodbc
from tests.config import DB_CONFIG

def to_uuid(prefix: str, legacy_id) -> str:
    hash_val = hashlib.md5(f"{prefix}_{legacy_id}".encode('utf-8')).hexdigest()
    return str(uuid.UUID(hash_val))

def run():
    print("=" * 70)
    print("BARCODE RE-LINKING & EAN BACKFILL ETL")
    print("=" * 70)

    cfg = DB_CONFIG.copy()
    cfg['port'] = 6432
    cfg['user'] = 'posadmin'
    cfg['password'] = '8t-MT5oPvmC2ERg6a2rs8n4kL52XiF1s'
    pg_conn = psycopg2.connect(**cfg)
    pg_conn.autocommit = False
    pg_cur = pg_conn.cursor()

    default_store_id = "00000000-0000-0000-0000-000000000000"

    # Step 1: Deduplicate barcodes
    print("[1/4] Deduplicating barcode table collisions...")
    pg_cur.execute("""
        DELETE FROM barcodes a 
        USING barcodes b
        WHERE a.id < b.id 
          AND a.barcode = b.barcode;
    """)
    print(f"  Removed {pg_cur.rowcount} duplicate barcode rows.")

    # Step 2: Ensure store_id is set
    print("[2/4] Setting default store_id...")
    pg_cur.execute("""
        UPDATE barcodes 
        SET store_id = %s
        WHERE store_id IS NULL;
    """, (default_store_id,))
    print(f"  Updated {pg_cur.rowcount} barcodes with store_id.")

    # Step 3: Re-link barcodes to active products
    print("[3/4] Re-linking barcodes from legacy soft-deleted products to active products...")
    pg_cur.execute("""
        UPDATE barcodes b
        SET product_id = new_p.id,
            is_deleted = false
        FROM products old_p
        JOIN products new_p ON old_p.product_code = new_p.product_code
        WHERE b.product_id = old_p.id
          AND old_p.is_deleted = true
          AND new_p.is_deleted = false;
    """)
    relinked = pg_cur.rowcount
    print(f"  Successfully re-linked {relinked} barcodes to active products.")

    # Step 4: Backfill any missing EANs from MS SQL ShortName
    print("[4/4] Syncing missing manufacturer EANs from MS SQL Master_Inventory_Product...")
    env = {}
    with open('.env.mssql', 'r', encoding='utf-8') as f:
        for line in f:
            line = line.strip()
            if '=' in line and not line.startswith('#'):
                k, v = line.split('=', 1)
                env[k] = v

    ms_conn_str = f"DRIVER={{SQL Server}};SERVER={env['MSSQL_SERVER']};DATABASE=APPLE26-27;UID={env['MSSQL_USER']};PWD={env['MSSQL_PASSWORD']};"
    ms_conn = pyodbc.connect(ms_conn_str, timeout=10)
    ms_cur = ms_conn.cursor()

    ms_cur.execute("""
        SELECT LTRIM(RTRIM(ID)), LTRIM(RTRIM(ShortName))
        FROM Master_Inventory_Product 
        WHERE ShortName IS NOT NULL AND LEN(LTRIM(RTRIM(ShortName))) >= 8 AND ISNUMERIC(LTRIM(RTRIM(ShortName))) = 1;
    """)
    ms_eans = ms_cur.fetchall()

    pg_cur.execute("SELECT barcode FROM barcodes WHERE store_id = %s;", (default_store_id,))
    existing_bcodes = set(r[0] for r in pg_cur.fetchall())

    pg_cur.execute("SELECT product_code, id FROM products WHERE is_deleted = false;")
    code_to_pid = dict(pg_cur.fetchall())

    inserted_eans = 0
    for pcode, ean in ms_eans:
        pcode = str(pcode).strip()
        ean = str(ean).strip()
        if ean in existing_bcodes:
            continue
        pid = code_to_pid.get(pcode)
        if not pid:
            continue

        bid = to_uuid("barcode", f"{pcode}_{ean}")
        try:
            pg_cur.execute("""
                INSERT INTO barcodes (id, store_id, product_id, barcode, is_primary, is_deleted, created_at)
                VALUES (%s, %s, %s, %s, true, false, NOW())
                ON CONFLICT (store_id, barcode) DO NOTHING;
            """, (bid, default_store_id, pid, ean))
            existing_bcodes.add(ean)
            inserted_eans += 1
        except Exception:
            pass

    print(f"  Inserted {inserted_eans} previously missing manufacturer EANs.")

    pg_conn.commit()

    # Final stats
    pg_cur.execute("""
        SELECT count(*) 
        FROM barcodes b 
        JOIN products p ON b.product_id = p.id 
        WHERE p.is_deleted = false;
    """)
    total_active_barcodes = pg_cur.fetchone()[0]

    print("\n" + "=" * 70)
    print(f"[COMPLETE] Total barcodes now ACTIVE and SCAN-READY: {total_active_barcodes}")
    print("=" * 70)

if __name__ == "__main__":
    run()
