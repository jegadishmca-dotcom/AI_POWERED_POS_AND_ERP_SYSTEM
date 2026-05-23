import { useEffect, useRef, useCallback } from 'react';

// Weighted barcode format: 21 + 5 digit item code + 5 digit weight/price + 1 checksum
// Example: 2100123005000 -> Item 00123, 500g

export const useBarcodeScanner = (onScan: (barcode: string, weight?: number) => void) => {
  // FIX GAP-20: Use refs so buffer persists across renders (avoids stale closure bug)
  const barcodeBuffer = useRef('');
  const lastKeyTime = useRef(0);
  const onScanRef = useRef(onScan);

  // Keep the callback ref current without re-registering the listener
  useEffect(() => {
    onScanRef.current = onScan;
  }, [onScan]);

  const handleKeyDown = useCallback((e: KeyboardEvent) => {
    // Ignore if typing in an input field (unless scanner fires there too)
    if ((e.target as HTMLElement).tagName === 'INPUT') return;

    const currentTime = Date.now();

    // Scanner types very fast (< 50ms between strokes). Humans type slower.
    if (currentTime - lastKeyTime.current > 50) {
      barcodeBuffer.current = ''; // Reset if typing too slow (human)
    }

    if (e.key === 'Enter') {
      const scanned = barcodeBuffer.current;
      barcodeBuffer.current = '';

      if (scanned.length > 3) {
        // Parse weighted barcode (GS1 standard: starts with 21, 13 chars)
        if (scanned.startsWith('21') && scanned.length === 13) {
          const itemCode = scanned.substring(2, 7);
          const weightVal = parseInt(scanned.substring(7, 12), 10);
          onScanRef.current(itemCode, weightVal / 1000); // grams → kg
        } else {
          onScanRef.current(scanned);
        }
      }
    } else if (e.key.length === 1) {
      barcodeBuffer.current += e.key;
    }

    lastKeyTime.current = currentTime;
  }, []); // Empty deps — safe because we use refs

  useEffect(() => {
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [handleKeyDown]);
};
