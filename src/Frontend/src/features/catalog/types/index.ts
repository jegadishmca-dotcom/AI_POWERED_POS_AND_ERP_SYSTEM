export interface ProductSearchResult {
  id: string;
  productCode: string;
  name: string;
  tamilName?: string;
  sellingPrice: number;
  primaryBarcode: string;
}

export interface ImportResult {
  totalImported: number;
  totalFailed: number;
  errors: string[];
}
