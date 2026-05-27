import { api } from '../../../utils/api';

export interface StockAdjustmentItem {
  productId: string;
  productName?: string;
  batchId?: string | null;
  batchNumber?: string | null;
  adjustedQuantity: number;
  unitCost: number;
}

export interface StockAdjustment {
  id: string;
  adjustmentNumber: string;
  reason: string;
  status: 'PENDING' | 'APPROVED' | 'REJECTED';
  createdAt: string;
  approvedByName?: string | null;
  items: StockAdjustmentItem[];
}

export const getStockAdjustments = async (): Promise<StockAdjustment[]> => {
  const response = await api.get('/api/inventory/stock-adjustment');
  return response.data;
};

export const createStockAdjustment = async (payload: {
  storeId?: string | null;
  reason: string;
  items: {
    productId: string;
    batchId?: string | null;
    adjustedQuantity: number;
    unitCost: number;
  }[];
}): Promise<{ id: string }> => {
  const response = await api.post('/api/inventory/stock-adjustment', payload);
  return response.data;
};

export const approveStockAdjustment = async (id: string): Promise<boolean> => {
  const response = await api.post(`/api/inventory/stock-adjustment/${id}/approve`);
  return response.data;
};

export const rejectStockAdjustment = async (id: string): Promise<boolean> => {
  const response = await api.post(`/api/inventory/stock-adjustment/${id}/reject`);
  return response.data;
};
