# Direct PostgreSQL verification script connecting to 192.168.1.5
param (
    [string]$Server = "192.168.1.5",
    [int]$Port = 5432,
    [string]$DbName = "posdb_uat",
    [string]$User = "posadmin",
    [string]$Password = "posadmin"
)

Add-Type -TypeDefinition @"
using System;
using System.Data;
using Npgsql;

public class PgTester {
    public static void Run(string connStr) {
        using (var conn = new NpgsqlConnection(connStr)) {
            conn.Open();
            Console.WriteLine("[INFO] Connected successfully to PostgreSQL on " + conn.Host + ":" + conn.Port + " / " + conn.Database);
            
            var sql = @"
                SELECT '1. PRODUCTS TOTAL' AS Metric, COUNT(*)::text AS Value FROM products
                UNION ALL
                SELECT '2. WEIGHABLE (KGS) PRODUCTS', COUNT(*)::text FROM products WHERE is_weighable = true
                UNION ALL
                SELECT '3. BARCODES TOTAL', COUNT(*)::text FROM barcodes
                UNION ALL
                SELECT '4. SUPPLIERS TOTAL', COUNT(*)::text FROM suppliers
                UNION ALL
                SELECT '5. PRODUCTS WITH PREFERRED SUPPLIER', COUNT(*)::text FROM products WHERE preferred_supplier_id IS NOT NULL
                UNION ALL
                SELECT '6. CUSTOMERS TOTAL', COUNT(*)::text FROM customers
                UNION ALL
                SELECT '7. CUSTOMERS WITH LOYALTY POINTS (>0)', COUNT(*)::text FROM customers WHERE running_loyalty_points > 0
                UNION ALL
                SELECT '8. TOTAL LOYALTY POINTS SUM', ROUND(SUM(running_loyalty_points), 2)::text FROM customers
                UNION ALL
                SELECT '9. TOTAL CUSTOMER LEDGER BALANCE SUM', ROUND(SUM(running_wallet_balance), 2)::text FROM customers
                UNION ALL
                SELECT '10. PRODUCT STOCK BATCHES TOTAL', COUNT(*)::text FROM product_batches
                UNION ALL
                SELECT '11. BATCHES WITH POSITIVE STOCK (>0)', COUNT(*)::text FROM product_batches WHERE available_quantity > 0
                UNION ALL
                SELECT '12. TOTAL PHYSICAL STOCK QUANTITY SUM', ROUND(SUM(available_quantity), 3)::text FROM product_batches WHERE available_quantity > 0;";

            using (var cmd = new NpgsqlCommand(sql, conn))
            using (var reader = cmd.ExecuteReader()) {
                Console.WriteLine("\n==================================================");
                Console.WriteLine(" MIGRATED DATABASE VERIFICATION METRICS (" + conn.Database + ")");
                Console.WriteLine("==================================================");
                while (reader.Read()) {
                    Console.WriteLine(string.Format("{0,-42} : {1}", reader[0], reader[1]));
                }
            }

            Console.WriteLine("\n==================================================");
            Console.WriteLine(" SPECIFIC ITEM CHECK: 'A VELLAM 1K' & WEIGHABLE ITEMS");
            Console.WriteLine("==================================================");
            var sqlVellam = @"
                SELECT 
                    p.product_code,
                    p.name,
                    u.symbol AS uom_symbol,
                    p.is_weighable,
                    p.mrp,
                    p.selling_price,
                    COALESCE(b.batch_number, 'N/A') AS batch_number,
                    COALESCE(b.available_quantity, 0) AS stock_qty
                FROM products p
                JOIN unit_of_measures u ON p.unit_of_measure_id = u.id
                LEFT JOIN product_batches b ON b.product_id = p.id
                WHERE p.name LIKE '%VELLAM%' OR p.product_code = 'BA-4287'
                LIMIT 5;";

            using (var cmd = new NpgsqlCommand(sqlVellam, conn))
            using (var reader = cmd.ExecuteReader()) {
                while (reader.Read()) {
                    Console.WriteLine(string.Format("Code: {0} | Name: {1,-20} | UOM: {2} | Weighable: {3} | MRP: ₹{4} | Price: ₹{5} | Stock: {6} ({7})",
                        reader[0], reader[1], reader[2], reader[3], reader[4], reader[5], reader[7], reader[6]));
                }
            }

            Console.WriteLine("\n==================================================");
            Console.WriteLine(" DATA INTEGRITY & ANOMALY CHECKS");
            Console.WriteLine("==================================================");
            var sqlIntegrity = @"
                SELECT 'Orphaned Products (Missing UOM)' AS Check_Name, COUNT(*)::text AS Anomaly_Count FROM products WHERE unit_of_measure_id IS NULL
                UNION ALL
                SELECT 'Orphaned Products (Missing TaxSlab)', COUNT(*)::text FROM products WHERE tax_slab_id IS NULL
                UNION ALL
                SELECT 'Duplicate Barcodes', COUNT(*)::text FROM (SELECT barcode_value FROM barcodes GROUP BY barcode_value HAVING COUNT(*) > 1) d
                UNION ALL
                SELECT 'Invalid/Blank Customer Names', COUNT(*)::text FROM customers WHERE name IS NULL OR TRIM(name) = '';";

            using (var cmd = new NpgsqlCommand(sqlIntegrity, conn))
            using (var reader = cmd.ExecuteReader()) {
                while (reader.Read()) {
                    Console.WriteLine(string.Format("{0,-42} : {1}", reader[0], reader[1]));
                }
            }
        }
    }
}
"@ -ReferencedAssemblies "System.Data.dll"

# Try loading Npgsql if available, or call API/dotnet script
