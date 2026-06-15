export interface ProductSearchResult {
  id: string;
  productCode: string;
  name: string;
  tamilName?: string;
  sellingPrice: number;
  primaryBarcode: string;
  cgstRate: number;
  sgstRate: number;
  cessRate: number;
  isWeighable: boolean;
  mrp: number;
  purchasePrice: number;
  description?: string;
  taxSlabName: string;
  taxSlabId: string;
  categoryId?: string;
  unitOfMeasureId?: string;
}

export interface ImportResult {
  totalImported: number;
  totalFailed: number;
  errors: string[];
}
