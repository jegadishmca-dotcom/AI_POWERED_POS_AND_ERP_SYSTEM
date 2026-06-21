import sys
import pypdfium2 as pdfium

def main():
    if len(sys.argv) < 3:
        print("Usage: python pdf_to_img.py <pdf_path> <image_path>")
        sys.exit(1)
        
    pdf_path = sys.argv[1]
    image_path = sys.argv[2]
    
    try:
        doc = pdfium.PdfDocument(pdf_path)
        page = doc[0]
        # render at scale 3 (approx 216 DPI) for high quality OCR text extraction
        bitmap = page.render(scale=3)
        pil_img = bitmap.to_pil()
        pil_img.save(image_path, "PNG")
        print("SUCCESS")
    except Exception as e:
        print(f"ERROR: {str(e)}")
        sys.exit(2)

if __name__ == "__main__":
    main()
