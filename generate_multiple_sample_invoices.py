import os
import psycopg2
import random
from reportlab.lib.pagesizes import letter
from reportlab.platypus import SimpleDocTemplate, Paragraph, Spacer, Table, TableStyle
from reportlab.lib.styles import getSampleStyleSheet, ParagraphStyle
from reportlab.lib import colors

# Fetch existing items from DB to build realistic invoices
db_items = []
try:
    conn = psycopg2.connect(
        host="192.168.29.64",
        port=5432,
        database="posdb",
        user="posadmin",
        password="pospassword"
    )
    cursor = conn.cursor()
    
    # Query up to 12 active products with barcodes
    query = """
        SELECT p.name, b.barcode, p.purchase_price, p.mrp
        FROM products p
        JOIN barcodes b ON p.id = b.product_id
        WHERE p.is_active = true AND b.is_deleted = false AND b.is_primary = true
        LIMIT 15
    """
    cursor.execute(query)
    rows = cursor.fetchall()
    for row in rows:
        db_items.append({
            "name": row[0],
            "barcode": row[1],
            "cost": float(row[2]),
            "mrp": float(row[3])
        })
    cursor.close()
    conn.close()
except Exception as e:
    print(f"Postgres Query skipped/failed: {e}")

# Fallbacks if DB query returned too few items
if len(db_items) < 10:
    fallback_items = [
        {"name": "Tata Salt 1kg", "barcode": "8901058002313", "cost": 22.00, "mrp": 28.00},
        {"name": "Britannia Bourbon 150g", "barcode": "8901063012345", "cost": 24.00, "mrp": 30.00},
        {"name": "Maggi Noodles 70g Pack", "barcode": "8901030753888", "cost": 11.50, "mrp": 14.00},
        {"name": "Premium Sliced Bread 400g", "barcode": "8902008801122", "cost": 32.00, "mrp": 40.00},
        {"name": "Fortune Mustard Oil 1L", "barcode": "8901725185550", "cost": 145.00, "mrp": 175.00},
        {"name": "Ariel Detergent Powder 1kg", "barcode": "8901030753448", "cost": 115.00, "mrp": 140.00},
        {"name": "Dettol Liquid Handwash 200ml", "barcode": "8901396388480", "cost": 75.00, "mrp": 99.00},
        {"name": "Colgate MaxFresh Paste 150g", "barcode": "8901117275818", "cost": 82.00, "mrp": 105.00},
        {"name": "Parle-G Gold Biscuits 100g", "barcode": "8901063142202", "cost": 8.50, "mrp": 10.00},
        {"name": "Surf Excel Matic Front Load 1kg", "barcode": "8901030704941", "cost": 190.00, "mrp": 230.00}
    ]
    for fb in fallback_items:
        if not any(x["barcode"] == fb["barcode"] for x in db_items):
            db_items.append(fb)

def generate_invoice(pdf_path, supplier_name, invoice_ref, items):
    doc = SimpleDocTemplate(pdf_path, pagesize=letter, rightMargin=36, leftMargin=36, topMargin=36, bottomMargin=36)
    story = []
    
    styles = getSampleStyleSheet()
    title_style = ParagraphStyle(
        'TitleStyle',
        parent=styles['Heading1'],
        fontSize=20,
        leading=24,
        textColor=colors.HexColor('#1E3A8A'), # Navy Blue
        spaceAfter=12
    )
    header_style = ParagraphStyle(
        'HeaderStyle',
        parent=styles['Normal'],
        fontSize=9,
        leading=12,
        textColor=colors.HexColor('#475569')
    )
    
    # Styles for table cell paragraphs to allow text wrapping
    cell_style_left = ParagraphStyle(
        'CellLeft',
        parent=styles['Normal'],
        fontName='Helvetica',
        fontSize=9,
        leading=11,
        textColor=colors.HexColor('#1E293B')
    )
    cell_style_left_bold = ParagraphStyle(
        'CellLeftBold',
        parent=styles['Normal'],
        fontName='Helvetica-Bold',
        fontSize=9,
        leading=11,
        textColor=colors.HexColor('#1E293B')
    )
    cell_style_right = ParagraphStyle(
        'CellRight',
        parent=styles['Normal'],
        fontName='Helvetica',
        fontSize=9,
        leading=11,
        alignment=2, # Right alignment
        textColor=colors.HexColor('#1E293B')
    )
    cell_style_right_bold = ParagraphStyle(
        'CellRightBold',
        parent=styles['Normal'],
        fontName='Helvetica-Bold',
        fontSize=9,
        leading=11,
        alignment=2, # Right alignment
        textColor=colors.HexColor('#1E293B')
    )
    
    story.append(Paragraph(f"<b>{supplier_name}</b>", title_style))
    story.append(Paragraph(f"GSTIN: 29{"".join(random.choices('ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789', k=13))} | Invoice Ref: {invoice_ref}", header_style))
    story.append(Paragraph("Date: 29 May 2026 | Term: Net 30 Days", header_style))
    story.append(Spacer(1, 15))
    
    # Define table data structure
    table_data = [
        [
            Paragraph("<b>Barcode</b>", cell_style_left_bold),
            Paragraph("<b>Item Description</b>", cell_style_left_bold),
            Paragraph("<b>Qty</b>", cell_style_right_bold),
            Paragraph("<b>Cost Price</b>", cell_style_right_bold),
            Paragraph("<b>MRP</b>", cell_style_right_bold),
            Paragraph("<b>Line Total</b>", cell_style_right_bold)
        ]
    ]
    
    # Populating items in table
    for item in items:
        line_total = item["qty"] * item["cost"]
        table_data.append([
            Paragraph(item["barcode"], cell_style_left),
            Paragraph(item["name"], cell_style_left),
            Paragraph(str(item["qty"]), cell_style_right),
            Paragraph(f"{item['cost']:.2f}", cell_style_right),
            Paragraph(f"{item['mrp']:.2f}", cell_style_right),
            Paragraph(f"{line_total:.2f}", cell_style_right)
        ])
        
    # Column widths adding up to 540 (Letter page width is 612 - 72 for 36pt margins = 540)
    col_widths = [90, 185, 45, 75, 65, 80]
    
    # Create Table with ReportLab TableStyle
    t = Table(table_data, colWidths=col_widths)
    t.setStyle(TableStyle([
        ('BACKGROUND', (0, 0), (-1, 0), colors.HexColor('#F1F5F9')), # Slate-100 header background
        ('ALIGN', (0, 0), (-1, -1), 'LEFT'),
        ('VALIGN', (0, 0), (-1, -1), 'MIDDLE'),
        ('TOPPADDING', (0, 0), (-1, -1), 6),
        ('BOTTOMPADDING', (0, 0), (-1, -1), 6),
        ('LEFTPADDING', (0, 0), (-1, -1), 6),
        ('RIGHTPADDING', (0, 0), (-1, -1), 6),
        ('GRID', (0, 0), (-1, -1), 0.5, colors.HexColor('#CBD5E1')), # Slate-300 grid border lines
    ]))
    
    story.append(t)
    story.append(Spacer(1, 20))
    story.append(Paragraph("<i>Authorized Signature & Stamp</i>", header_style))
    
    doc.build(story)
    print(f"Generated PDF with beautiful table layout: {os.path.abspath(pdf_path)}")

# --- Generate Supplier A Invoice ---
# Mix: 5 existing items (some matching, some cost changes), 5 new items
supplier_a_items = []
# Pick 5 existing items
for idx, x in enumerate(db_items[:5]):
    cost = x["cost"]
    mrp = x["mrp"]
    if idx % 2 == 1:
        # Create a price conflict
        cost = round(cost * 1.15, 2)
        mrp = round(mrp * 1.10, 2)
    supplier_a_items.append({
        "barcode": x["barcode"],
        "name": x["name"],
        "qty": random.randint(10, 100),
        "cost": cost,
        "mrp": mrp
    })
# Add 5 brand-new items
new_products_a = [
    {"name": "Cadbury Dairy Milk Silk 150g", "barcode": "8901058002399", "cost": 75.00, "mrp": 90.00},
    {"name": "Bingo Potato Chips Salted 80g", "barcode": "8901725181122", "cost": 15.50, "mrp": 20.00},
    {"name": "Nescafe Classic Coffee 100g", "barcode": "8901058892113", "cost": 135.00, "mrp": 160.00},
    {"name": "Amul Butter 500g", "barcode": "8901262010011", "cost": 210.00, "mrp": 250.00},
    {"name": "Lipton Green Tea 100 Bags", "barcode": "8901030752103", "cost": 310.00, "mrp": 380.00}
]
for item in new_products_a:
    supplier_a_items.append({
        "barcode": item["barcode"],
        "name": item["name"],
        "qty": random.randint(15, 50),
        "cost": item["cost"],
        "mrp": item["mrp"]
    })

generate_invoice(
    pdf_path="sample_invoice_metro_wholesale.pdf",
    supplier_name="METRO SUPER WHOLESALE LTD",
    invoice_ref="MWS-20260529-881",
    items=supplier_a_items
)

# --- Generate Supplier B Invoice ---
# Mix: 5 different existing items, 5 different new items
supplier_b_items = []
# Pick 5 different existing items
for idx, x in enumerate(db_items[5:10]):
    cost = x["cost"]
    mrp = x["mrp"]
    if idx % 2 == 1:
        # Create a price conflict
        cost = round(cost * 0.90, 2)
    supplier_b_items.append({
        "barcode": x["barcode"],
        "name": x["name"],
        "qty": random.randint(10, 120),
        "cost": cost,
        "mrp": mrp
    })
# Add 5 different brand-new items
new_products_b = [
    {"name": "Coca Cola 2.25L Bottle", "barcode": "8901764022254", "cost": 72.00, "mrp": 90.00},
    {"name": "Good Day Cashew Cookies 200g", "barcode": "8901063025000", "cost": 28.50, "mrp": 35.00},
    {"name": "Gillette Foam Sensitive 418g", "barcode": "8901396349911", "cost": 175.00, "mrp": 225.00},
    {"name": "Lizol Disinfectant 2L Citrus", "barcode": "8901396380903", "cost": 290.00, "mrp": 360.00},
    {"name": "Haldiram Bhujia Sev 400g", "barcode": "8904063200213", "cost": 85.00, "mrp": 110.00}
]
for item in new_products_b:
    supplier_b_items.append({
        "barcode": item["barcode"],
        "name": item["name"],
        "qty": random.randint(20, 80),
        "cost": item["cost"],
        "mrp": item["mrp"]
    })

generate_invoice(
    pdf_path="sample_invoice_supernova_suppliers.pdf",
    supplier_name="SUPERNOVA RETAIL SUPPLIERS",
    invoice_ref="SRS-20260529-041",
    items=supplier_b_items
)
