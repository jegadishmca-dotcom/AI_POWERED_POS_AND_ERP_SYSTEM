import { useEffect, useCallback } from 'react';

// Weighted barcode format: 21 + 5 digit item code + 5 digit weight/price + 1 checksum
// Example: 210012300500C -> Item 00123, 500g

export const useBarcodeScanner = (onScan: (barcode: string, weight?: number) => void) => {
  let barcodeBuffer = '';
  let lastKeyTime = 0;

  const handleKeyDown = useCallback((e: KeyboardEvent) => {
    // Ignore if typing in an input field (unless we want scanner to override)
    if ((e.target as HTMLElement).tagName === 'INPUT') return;

    const currentTime = new Date().getTime();
    
    // Scanner types very fast (< 30ms between strokes)
    if (currentTime - lastKeyTime > 50) {
      barcodeBuffer = ''; // Reset if typing too slow (human)
    }
    
    if (e.key === 'Enter') {
      if (barcodeBuffer.length > 3) {
        // Parse weighted barcode
        if (barcodeBuffer.startsWith('21') && barcodeBuffer.length === 13) {
          const itemCode = barcodeBuffer.substring(2, 7);
          const weightVal = parseInt(barcodeBuffer.substring(7, 12), 10);
          onScan(itemCode, weightVal / 1000); // Assuming grams, convert to Kg
        } else {
          onScan(barcodeBuffer);
        }
      }
      barcodeBuffer = '';
    } else if (e.key.length === 1) {
      barcodeBuffer += e.key;
    }

    lastKeyTime = currentTime;
  }, [onScan]);

  useEffect(() => {
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [handleKeyDown]);
};
