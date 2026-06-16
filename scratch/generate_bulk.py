# Save this in a scratch folder as scratch/generate_bulk.py and run it
import csv

headers = ['ProductCode', 'Name', 'TamilName', 'Description', 'Mrp', 'SellingPrice', 'PurchasePrice', 'Barcode', 'TaxSlabName', 'IsWeighable', 'HasExpiry', 'Uom']
with open('bulk_products_test.csv', 'w', newline='', encoding='utf-8') as f:
    writer = csv.writer(f)
    writer.writerow(headers)
    for i in range(1, 10001): # Generates 10,000 products
        writer.writerow([
            f"BULK-{i:05d}",
            f"Bulk Test Product {i}",
            "",
            "Stress testing catalog load",
            "100.00",
            "90.00",
            "70.00",
            f"99{i:011d}",
            "GST 18%",
            "FALSE",
            "FALSE",
            "Pcs"
        ])
