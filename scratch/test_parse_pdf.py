import pdfplumber
import os

pdf_path = r"D:\JEGADISH\APPLE_SUPERMARKET_POS_PROJECT\AI_POWERED_POS_AND_ERP_SYSTEM\Supplier_invoice\apple.pdf"

if not os.path.exists(pdf_path):
    print("File not found")
else:
    with pdfplumber.open(pdf_path) as pdf:
        for i, page in enumerate(pdf.pages):
            print(f"--- Page {i+1} ---")
            text = page.extract_text()
            print(text)
