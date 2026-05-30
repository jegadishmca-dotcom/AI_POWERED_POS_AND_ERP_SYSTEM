export interface ProductSearchResult {
  id: string;
  productCode: string;
  name: string;
  tamilName?: string;
  sellingPrice: number;
  primaryBarcode: string;
  cgstRate: number;
  sgstRate: number;
  isWeighable: boolean;
  mrp: number;
  purchasePrice: number;
  description?: string;
  taxSlabName: string;
}

export interface ImportResult {
  totalImported: number;
  totalFailed: number;
  errors: string[];
}
