import os
import random
from reportlab.lib.pagesizes import letter
from reportlab.platypus import SimpleDocTemplate, Paragraph, Spacer, Table, TableStyle
from reportlab.lib.styles import getSampleStyleSheet, ParagraphStyle
from reportlab.lib import colors

# Create target directory if it doesn't exist
os.makedirs("Supplier_invoice", exist_ok=True)

# Sample items to simulate wholesale purchases
items_catalog = [
    {"name": "Tata Salt 1kg", "barcode": "8901058002313", "cost": 22.00, "mrp": 28.00},
    {"name": "Britannia Bourbon 150g", "barcode": "8901063012345", "cost": 24.00, "mrp": 30.00},
    {"name": "Maggi Noodles 70g Pack", "barcode": "8901030753888", "cost": 11.50, "mrp": 14.00},
    {"name": "Premium Sliced Bread 400g", "barcode": "8902008801122", "cost": 32.00, "mrp": 40.00},
    {"name": "Fortune Mustard Oil 1L", "barcode": "8901725185550", "cost": 145.00, "mrp": 175.00},
    {"name": "Cadbury Dairy Milk Silk 150g", "barcode": "8901058002399", "cost": 75.00, "mrp": 90.00},
    {"name": "Bingo Potato Chips Salted 80g", "barcode": "8901725181122", "cost": 15.50, "mrp": 20.00},
    {"name": "Nescafe Classic Coffee 100g", "barcode": "8901058892113", "cost": 135.00, "mrp": 160.00},
    {"name": "Amul Butter 500g", "barcode": "8901262010011", "cost": 210.00, "mrp": 250.00},
    {"name": "Lipton Green Tea 100 Bags", "barcode": "8901030752103", "cost": 310.00, "mrp": 380.00}
]

def generate_busy_invoice():
    pdf_path = "Supplier_invoice/busy_erp_invoice.pdf"
    doc = SimpleDocTemplate(pdf_path, pagesize=letter, rightMargin=36, leftMargin=36, topMargin=36, bottomMargin=36)
    story = []
    styles = getSampleStyleSheet()
    
    title_style = ParagraphStyle('T1', parent=styles['Heading1'], fontSize=18, textColor=colors.HexColor('#0F172A'), spaceAfter=6)
    sub_style = ParagraphStyle('S1', parent=styles['Normal'], fontSize=8, leading=10, textColor=colors.HexColor('#475569'))
    cell_left = ParagraphStyle('CL1', parent=styles['Normal'], fontSize=8, leading=10)
    cell_left_b = ParagraphStyle('CLB1', parent=styles['Normal'], fontName='Helvetica-Bold', fontSize=8, leading=10)
    cell_right = ParagraphStyle('CR1', parent=styles['Normal'], fontSize=8, leading=10, alignment=2)
    cell_right_b = ParagraphStyle('CRB1', parent=styles['Normal'], fontName='Helvetica-Bold', fontSize=8, leading=10, alignment=2)

    story.append(Paragraph("<b>CHITRA WHOLESALERS (Busy Accounting Format)</b>", title_style))
    story.append(Paragraph("GSTIN: 33ABCDE1234F1Z1 | Address: 42, Oppanakara Street, Coimbatore - 641001", sub_style))
    story.append(Paragraph("Invoice No: BUSY-992211 | Date: 20-06-2026", sub_style))
    story.append(Spacer(1, 10))

    table_data = [[
        Paragraph("<b>S.No</b>", cell_left_b),
        Paragraph("<b>Description of Goods</b>", cell_left_b),
        Paragraph("<b>HSN/SAC Code</b>", cell_left_b),
        Paragraph("<b>Qty</b>", cell_right_b),
        Paragraph("<b>Rate (Rs.)</b>", cell_right_b),
        Paragraph("<b>Total Amount</b>", cell_right_b)
    ]]

    for idx, item in enumerate(items_catalog[:6], 1):
        # Extract numeric HSN from barcode value as placeholder
        hsn = item["barcode"][:8]
        qty = 20 + idx * 5
        total = qty * item["cost"]
        table_data.append([
            Paragraph(str(idx), cell_left),
            Paragraph(item["name"], cell_left),
            Paragraph(hsn, cell_left),
            Paragraph(str(qty), cell_right),
            Paragraph(f"{item['cost']:.2f}", cell_right),
            Paragraph(f"{total:.2f}", cell_right)
        ])

    t = Table(table_data, colWidths=[40, 200, 90, 50, 70, 90])
    t.setStyle(TableStyle([
        ('BACKGROUND', (0, 0), (-1, 0), colors.HexColor('#E2E8F0')),
        ('GRID', (0, 0), (-1, -1), 0.5, colors.HexColor('#94A3B8')),
        ('TOPPADDING', (0, 0), (-1, -1), 5),
        ('BOTTOMPADDING', (0, 0), (-1, -1), 5),
    ]))
    story.append(t)
    
    story.append(Spacer(1, 15))
    story.append(Paragraph("OUTPUT CGST 9.0%: Rs. 210.50<br/>OUTPUT SGST 9.0%: Rs. 210.50<br/>ROUND OFF: Rs. -0.20", sub_style))
    
    doc.build(story)
    print(f"Generated Busy ERP sample invoice.")

def generate_marg_invoice():
    pdf_path = "Supplier_invoice/marg_erp_invoice.pdf"
    doc = SimpleDocTemplate(pdf_path, pagesize=letter, rightMargin=36, leftMargin=36, topMargin=36, bottomMargin=36)
    story = []
    styles = getSampleStyleSheet()
    
    title_style = ParagraphStyle('T2', parent=styles['Heading1'], fontSize=18, textColor=colors.HexColor('#1E3A8A'), spaceAfter=6)
    sub_style = ParagraphStyle('S2', parent=styles['Normal'], fontSize=8, leading=10, textColor=colors.HexColor('#334155'))
    cell_left = ParagraphStyle('CL2', parent=styles['Normal'], fontSize=8, leading=10)
    cell_left_b = ParagraphStyle('CLB2', parent=styles['Normal'], fontName='Helvetica-Bold', fontSize=8, leading=10)
    cell_right = ParagraphStyle('CR2', parent=styles['Normal'], fontSize=8, leading=10, alignment=2)
    cell_right_b = ParagraphStyle('CRB2', parent=styles['Normal'], fontName='Helvetica-Bold', fontSize=8, leading=10, alignment=2)

    story.append(Paragraph("<b>SUDHAN DISTRIBUTORS (Marg ERP Format)</b>", title_style))
    story.append(Paragraph("GSTIN: 33ZXYWV9876C1Z9 | Address: 110, Goods Shed Road, Madurai - 625001", sub_style))
    story.append(Paragraph("Bill No: MRG-3388221 | Date: 20-06-2026", sub_style))
    story.append(Spacer(1, 10))

    table_data = [[
        Paragraph("<b>S.N.</b>", cell_left_b),
        Paragraph("<b>Description of Goods</b>", cell_left_b),
        Paragraph("<b>HSN/SAC</b>", cell_left_b),
        Paragraph("<b>Qty.</b>", cell_right_b),
        Paragraph("<b>Rate</b>", cell_right_b),
        Paragraph("<b>Amount</b>", cell_right_b)
    ]]

    for idx, item in enumerate(items_catalog[3:9], 1):
        hsn = item["barcode"][:8]
        qty = 10 + idx * 2
        total = qty * item["cost"]
        table_data.append([
            Paragraph(str(idx), cell_left),
            Paragraph(item["name"], cell_left),
            Paragraph(hsn, cell_left),
            Paragraph(str(qty), cell_right),
            Paragraph(f"{item['cost']:.2f}", cell_right),
            Paragraph(f"{total:.2f}", cell_right)
        ])

    t = Table(table_data, colWidths=[30, 210, 80, 50, 80, 90])
    t.setStyle(TableStyle([
        ('BACKGROUND', (0, 0), (-1, 0), colors.HexColor('#F8FAFC')),
        ('LINEBELOW', (0, 0), (-1, 0), 1, colors.HexColor('#0284C7')),
        ('GRID', (0, 0), (-1, -1), 0.5, colors.HexColor('#CBD5E1')),
        ('TOPPADDING', (0, 0), (-1, -1), 4),
        ('BOTTOMPADDING', (0, 0), (-1, -1), 4),
    ]))
    story.append(t)
    
    story.append(Spacer(1, 15))
    story.append(Paragraph("CGST 9%: Rs. 140.00<br/>SGST 9%: Rs. 140.00<br/>ROUND OFF: Rs. 0.15", sub_style))

    doc.build(story)
    print(f"Generated Marg ERP sample invoice.")

def generate_zoho_invoice():
    pdf_path = "Supplier_invoice/zoho_books_invoice.pdf"
    doc = SimpleDocTemplate(pdf_path, pagesize=letter, rightMargin=36, leftMargin=36, topMargin=36, bottomMargin=36)
    story = []
    styles = getSampleStyleSheet()
    
    title_style = ParagraphStyle('T3', parent=styles['Heading1'], fontSize=16, textColor=colors.HexColor('#0D9488'), spaceAfter=6)
    sub_style = ParagraphStyle('S3', parent=styles['Normal'], fontSize=8, leading=10, textColor=colors.HexColor('#4B5563'))
    cell_left = ParagraphStyle('CL3', parent=styles['Normal'], fontSize=8, leading=10)
    cell_left_b = ParagraphStyle('CLB3', parent=styles['Normal'], fontName='Helvetica-Bold', fontSize=8, leading=10)
    cell_right = ParagraphStyle('CR3', parent=styles['Normal'], fontSize=8, leading=10, alignment=2)
    cell_right_b = ParagraphStyle('CRB3', parent=styles['Normal'], fontName='Helvetica-Bold', fontSize=8, leading=10, alignment=2)

    story.append(Paragraph("<b>MADURAI TRADING HOUSE (Zoho Books Layout)</b>", title_style))
    story.append(Paragraph("GSTIN: 33ZOHOB1234M1Z5 | Address: GST Road, Guindy, Chennai - 600032", sub_style))
    story.append(Paragraph("Invoice Ref: INV-ZH-928 | Date: 20-06-2026", sub_style))
    story.append(Spacer(1, 10))

    # Note: No Serial Number Column!
    table_data = [[
        Paragraph("<b>Item & Description</b>", cell_left_b),
        Paragraph("<b>HSN/SAC</b>", cell_left_b),
        Paragraph("<b>Qty</b>", cell_right_b),
        Paragraph("<b>Rate</b>", cell_right_b),
        Paragraph("<b>Amount</b>", cell_right_b)
    ]]

    for idx, item in enumerate(items_catalog[5:10], 1):
        hsn = item["barcode"][:8]
        qty = 15 + idx * 3
        total = qty * item["cost"]
        table_data.append([
            Paragraph(item["name"], cell_left),
            Paragraph(hsn, cell_left),
            Paragraph(str(qty), cell_right),
            Paragraph(f"{item['cost']:.2f}", cell_right),
            Paragraph(f"{total:.2f}", cell_right)
        ])

    t = Table(table_data, colWidths=[230, 90, 50, 80, 90])
    t.setStyle(TableStyle([
        ('LINEABOVE', (0, 0), (-1, 0), 1, colors.HexColor('#0D9488')),
        ('LINEBELOW', (0, 0), (-1, 0), 1, colors.HexColor('#0D9488')),
        ('LINEBELOW', (0, 1), (-1, -1), 0.5, colors.HexColor('#E5E7EB')),
        ('TOPPADDING', (0, 0), (-1, -1), 6),
        ('BOTTOMPADDING', (0, 0), (-1, -1), 6),
    ]))
    story.append(t)
    
    story.append(Spacer(1, 15))
    story.append(Paragraph("OUTPUT CGST: Rs. 98.40<br/>OUTPUT SGST: Rs. 98.40<br/>ROUND OFF: Rs. -0.10", sub_style))

    doc.build(story)
    print(f"Generated Zoho Books sample invoice.")

def generate_vyapar_invoice():
    pdf_path = "Supplier_invoice/vyapar_invoice.pdf"
    doc = SimpleDocTemplate(pdf_path, pagesize=letter, rightMargin=36, leftMargin=36, topMargin=36, bottomMargin=36)
    story = []
    styles = getSampleStyleSheet()
    
    title_style = ParagraphStyle('T4', parent=styles['Heading1'], fontSize=18, textColor=colors.HexColor('#DC2626'), spaceAfter=6)
    sub_style = ParagraphStyle('S4', parent=styles['Normal'], fontSize=8, leading=10, textColor=colors.HexColor('#4B5563'))
    cell_left = ParagraphStyle('CL4', parent=styles['Normal'], fontSize=8, leading=10)
    cell_left_b = ParagraphStyle('CLB4', parent=styles['Normal'], fontName='Helvetica-Bold', fontSize=8, leading=10)
    cell_right = ParagraphStyle('CR4', parent=styles['Normal'], fontSize=8, leading=10, alignment=2)
    cell_right_b = ParagraphStyle('CRB4', parent=styles['Normal'], fontName='Helvetica-Bold', fontSize=8, leading=10, alignment=2)

    story.append(Paragraph("<b>KOVAI RETAIL DISTRIBUTORS (Vyapar Format)</b>", title_style))
    story.append(Paragraph("GSTIN: 33VYAP1234K1Z4 | Address: 21B, Trichy Road, Coimbatore - 641005", sub_style))
    story.append(Paragraph("Invoice Ref: VY-99818 | Date: 20-06-2026", sub_style))
    story.append(Spacer(1, 10))

    table_data = [[
        Paragraph("<b>No.</b>", cell_left_b),
        Paragraph("<b>Product Name</b>", cell_left_b),
        Paragraph("<b>HSN</b>", cell_left_b),
        Paragraph("<b>Quantity</b>", cell_right_b),
        Paragraph("<b>Rate</b>", cell_right_b),
        Paragraph("<b>Amount</b>", cell_right_b)
    ]]

    for idx, item in enumerate(items_catalog[1:7], 1):
        hsn = item["barcode"][:8]
        qty = 5 + idx * 4
        total = qty * item["cost"]
        table_data.append([
            Paragraph(str(idx), cell_left),
            Paragraph(item["name"], cell_left),
            Paragraph(hsn, cell_left),
            Paragraph(str(qty), cell_right),
            Paragraph(f"{item['cost']:.2f}", cell_right),
            Paragraph(f"{total:.2f}", cell_right)
        ])

    t = Table(table_data, colWidths=[30, 210, 80, 50, 80, 90])
    t.setStyle(TableStyle([
        ('BACKGROUND', (0, 0), (-1, 0), colors.HexColor('#F3F4F6')),
        ('GRID', (0, 0), (-1, -1), 0.5, colors.HexColor('#E5E7EB')),
        ('TOPPADDING', (0, 0), (-1, -1), 5),
        ('BOTTOMPADDING', (0, 0), (-1, -1), 5),
    ]))
    story.append(t)
    
    story.append(Spacer(1, 15))
    story.append(Paragraph("OUTPUT CGST: Rs. 112.50<br/>OUTPUT SGST: Rs. 112.50<br/>ROUND OFF: Rs. -0.05", sub_style))

    doc.build(story)
    print(f"Generated Vyapar sample invoice.")

if __name__ == "__main__":
    generate_busy_invoice()
    generate_marg_invoice()
    generate_zoho_invoice()
    generate_vyapar_invoice()
