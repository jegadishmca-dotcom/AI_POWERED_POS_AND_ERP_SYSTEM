import { api } from '@/utils/api';
import { ProductSearchResult, ImportResult } from '../types';

export const searchProducts = async (q: string, limit: number = 20): Promise<ProductSearchResult[]> => {
  const { data } = await api.get('/api/catalog/search', { params: { q, limit } });
  return data;
};

export const importCsv = async (
  file: File, 
  jobId?: string, 
  onProgress?: (progress: { processedRows: number; totalRows: number; importedCount: number; failedCount: number; percent: number }) => void
): Promise<ImportResult> => {
  const formData = new FormData();
  formData.append('file', file);
  if (jobId) {
    formData.append('jobId', jobId);
  }

  let intervalId: any = null;
  if (jobId && onProgress) {
    intervalId = setInterval(async () => {
      try {
        const { data } = await api.get(`/api/catalog/import-status/${jobId}`);
        if (data) {
          onProgress({
            processedRows: data.processedRows || 0,
            totalRows: data.totalRows || 0,
            importedCount: data.importedCount || 0,
            failedCount: data.failedCount || 0,
            percent: data.percent || 0
          });
          if (data.isCompleted) {
            clearInterval(intervalId);
          }
        }
      } catch (e) {
        // ignore status check errors
      }
    }, 400);
  }
  
  try {
    const { data } = await api.post('/api/catalog/import', formData, {
      headers: { 'Content-Type': 'multipart/form-data' },
    });
    if (intervalId) clearInterval(intervalId);
    return data;
  } catch (err) {
    if (intervalId) clearInterval(intervalId);
    throw err;
  }
};

export interface TaxSlab {
  id: string;
  name: string;
  cgstRate: number;
  sgstRate: number;
  igstRate: number;
  cessRate: number;
}

export const getTaxSlabs = async (): Promise<TaxSlab[]> => {
  const { data } = await api.get('/api/catalog/tax-slabs');
  return data;
};

export interface UnitOfMeasure {
  id: string;
  name: string;
  symbol: string;
}

export interface Category {
  id: string;
  name: string;
  parentCategoryId?: string;
}

export const getUoms = async (): Promise<UnitOfMeasure[]> => {
  const { data } = await api.get('/api/catalog/uoms');
  return data;
};

export const getCategories = async (): Promise<Category[]> => {
  const { data } = await api.get('/api/catalog/categories');
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
  taxSlabId?: string;
  categoryId?: string;
  unitOfMeasureId?: string;
}

export const createProduct = async (payload: CreateProductPayload): Promise<string> => {
  const { data } = await api.post('/api/catalog', payload);
  return data;
};

export interface UpdateProductPayload extends CreateProductPayload {
  id: string;
}

export const updateProduct = async (id: string, payload: UpdateProductPayload): Promise<boolean> => {
  const { data } = await api.put(`/api/catalog/${id}`, payload);
  return data;
};

export const deleteProduct = async (id: string): Promise<boolean> => {
  const { data } = await api.delete(`/api/catalog/${id}`);
  return data;
};
