import { api } from '@/utils/api';
import { ProductSearchResult, ImportResult } from '../types';

export const searchProducts = async (q: string, limit: number = 20): Promise<ProductSearchResult[]> => {
  const { data } = await api.get('/api/catalog/search', { params: { q, limit } });
  return data;
};

export const importCsv = async (file: File): Promise<ImportResult> => {
  const formData = new FormData();
  formData.append('file', file);
  
  const { data } = await api.post('/api/catalog/import', formData, {
    headers: { 'Content-Type': 'multipart/form-data' },
  });
  return data;
};

export interface CreateProductPayload {
  productCode: string;
  name: string;
  tamilName?: string;
  description?: string;
  mrp: number;
  sellingPrice: number;
  purchasePrice: number;
  barcodeValue: string;
}

export const createProduct = async (payload: CreateProductPayload): Promise<string> => {
  const { data } = await api.post('/api/catalog', payload);
  return data;
};
