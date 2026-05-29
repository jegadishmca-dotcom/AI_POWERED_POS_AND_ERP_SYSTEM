import os
import psycopg2
from reportlab.lib.pagesizes import letter
from reportlab.platypus import SimpleDocTemplate, Paragraph, Spacer
from reportlab.lib.styles import getSampleStyleSheet, ParagraphStyle
from reportlab.lib import colors

# 1. Fetch some existing products & barcodes from PostgreSQL
existing_items = []
try:
    conn = psycopg2.connect(
        host="192.168.29.64",
        port=5432,
        database="posdb",
        user="posadmin",
        password="pospassword"
    )
    cursor = conn.cursor()
    
    # Query products that have barcodes
    query = """
        SELECT p.name, b.barcode_value, p.purchase_price, p.mrp
        FROM products p
        JOIN barcodes b ON p.id = b.product_id
        WHERE p.is_active = true AND b.is_primary = true
        LIMIT 3
    """
    cursor.execute(query)
    rows = cursor.fetchall()
    for row in rows:
        existing_items.append({
            "name": row[0],
            "barcode": row[1],
            "cost": float(row[2]),
            "mrp": float(row[3])
        })
    cursor.close()
    conn.close()
except Exception as e:
    print(f"Postgres Query skipped/failed: {e}")

# Fallback defaults if database is empty or inaccessible
if len(existing_items) < 3:
    existing_items = [
        {"name": "Tata Salt 1kg", "barcode": "8901058002313", "cost": 22.00, "mrp": 28.00},
        {"name": "Britannia Bourbon 150g", "barcode": "8901063012345", "cost": 26.50, "mrp": 30.00},
        {"name": "Fortune Mustard Oil 1L", "barcode": "8901725185550", "cost": 145.00, "mrp": 175.00}
    ]

# 2. Add some custom scenarios to stress-test the parser:
test_items = [
    {
        "barcode": existing_items[0]["barcode"],
        "name": f"{existing_items[0]['name']}",
        "qty": 40,
        "cost": existing_items[0]["cost"],
        "mrp": existing_items[0]["mrp"]
    },
    {
        "barcode": existing_items[1]["barcode"],
        "name": f"{existing_items[1]['name']}",
        "qty": 65,
        "cost": round(existing_items[1]["cost"] + 5.00, 2), # price conflict
        "mrp": existing_items[1]["mrp"]
    },
    {
        "barcode": "8901030753888",
        "name": "Maggi Noodles 70g Pack",
        "qty": 120,
        "cost": 11.50,
        "mrp": 14.00
    },
    {
        "barcode": "8902008001122",
        "name": "Premium Sliced Bread 400g",
        "qty": 30,
        "cost": 32.00,
        "mrp": 40.00
    }
]

# 3. Build a monospaced text PDF that preserves rows on single lines
pdf_path = "sample_invoice.pdf"
doc = SimpleDocTemplate(pdf_path, pagesize=letter, rightMargin=36, leftMargin=36, topMargin=36, bottomMargin=36)
story = []

styles = getSampleStyleSheet()
title_style = ParagraphStyle(
    'TitleStyle',
    parent=styles['Heading1'],
    fontSize=18,
    textColor=colors.HexColor('#312E81'), # Indigo
    spaceAfter=12
)
header_style = ParagraphStyle(
    'HeaderStyle',
    parent=styles['Normal'],
    fontSize=9,
    textColor=colors.HexColor('#475569')
)

# Monospaced font ensures columns align visually while remaining as single lines of text
mono_style = ParagraphStyle(
    'MonoStyle',
    parent=styles['Normal'],
    fontName='Courier',
    fontSize=9,
    leading=12,
    textColor=colors.HexColor('#1E293B')
)

story.append(Paragraph("<b>GLOBAL WHOLESALE MART LTD</b>", title_style))
story.append(Paragraph("GSTIN: 29AAAAC1234A1Z0 | Invoice Ref: GWM-99882211", header_style))
story.append(Paragraph("Date: 29 May 2026 | Term: Cash On Delivery", header_style))
story.append(Spacer(1, 15))

# Header line
story.append(Paragraph("---------------------------------------------------------------------------", mono_style))
story.append(Paragraph("Barcode        Item Description                 Qty      Cost       MRP", mono_style))
story.append(Paragraph("---------------------------------------------------------------------------", mono_style))

# Items lines. We format with padding to align them cleanly
for item in test_items:
    barcode_str = item["barcode"].ljust(15)
    name_str = item["name"][:30].ljust(30)
    qty_str = str(item["qty"]).rjust(5)
    cost_str = f"{item['cost']:.2f}".rjust(10)
    mrp_str = f"{item['mrp']:.2f}".rjust(10)
    
    line = f"{barcode_str}{name_str}{qty_str}{cost_str}{mrp_str}"
    story.append(Paragraph(line, mono_style))

story.append(Paragraph("---------------------------------------------------------------------------", mono_style))
story.append(Spacer(1, 20))
story.append(Paragraph("<i>Authorized Signature & Stamp</i>", header_style))

# Generate the PDF
doc.build(story)
print(f"Sample test invoice PDF successfully generated at: {os.path.abspath(pdf_path)}")
