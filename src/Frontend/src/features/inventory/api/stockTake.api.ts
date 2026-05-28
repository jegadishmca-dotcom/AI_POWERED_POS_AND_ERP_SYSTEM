import { api } from '../../../utils/api';

// Stock Position Interfaces
export interface StockPosition {
  productId: string;
  productCode: string;
  productName: string;
  categoryName: string;
  currentStock: number;
  lastUnitCost: number;
  totalValue: number;
}

// Stock Take Interfaces
export interface StockTakeItem {
  productId: string;
  productName: string;
  batchId?: string | null;
  batchNumber?: string | null;
  systemQuantity: number;
  physicalQuantity: number;
  varianceQuantity: number;
}

export interface StockTake {
  id: string;
  takeNumber: string;
  scheduledDate: string;
  status: 'DRAFT' | 'REVIEW' | 'APPROVED' | 'REJECTED';
  createdAt: string;
  totalItemsCount: number;
  approvedByName?: string | null;
  items?: StockTakeItem[];
}

// API Endpoints for Stock Position
export const getStockPositions = async (params: {
  storeId?: string | null;
  categoryId?: string | null;
  searchTerm?: string | null;
}): Promise<StockPosition[]> => {
  const response = await api.get('/api/inventory/stock-position', { params });
  return response.data;
};

// API Endpoints for Stock Take
export const getStockTakes = async (storeId?: string | null): Promise<StockTake[]> => {
  const response = await api.get('/api/inventory/stock-take', { params: { storeId } });
  return response.data;
};

export const getStockTakeDetails = async (id: string): Promise<StockTake> => {
  const response = await api.get(`/api/inventory/stock-take/${id}`);
  return response.data;
};

export const createOrUpdateStockTake = async (payload: {
  id?: string | null;
  storeId?: string | null;
  scheduledDate: string;
  status: 'DRAFT' | 'REVIEW';
  items: {
    productId: string;
    batchId?: string | null;
    physicalQuantity: number;
  }[];
}): Promise<{ id: string }> => {
  const response = await api.post('/api/inventory/stock-take', payload);
  return response.data;
};

export const approveStockTake = async (id: string): Promise<boolean> => {
  const response = await api.post(`/api/inventory/stock-take/${id}/approve`);
  return response.data;
};

export const rejectStockTake = async (id: string): Promise<boolean> => {
  const response = await api.post(`/api/inventory/stock-take/${id}/reject`);
  return response.data;
};
