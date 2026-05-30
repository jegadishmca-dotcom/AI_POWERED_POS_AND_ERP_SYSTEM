import { api } from '@/utils/api';

export interface ForecastRecommendation {
  productId: string;
  productCode: string;
  productName: string;
  currentStock: number;
  avgDailySales: number;
  forecastedDemand: number;
  recommendedOrderQty: number;
  supplierId: string;
  supplierName: string;
  unitCost: number;
  totalCost: number;
}

export interface ExpiryMarkdown {
  productId: string;
  productCode: string;
  productName: string;
  batchId: string;
  batchNumber: string;
  expiryDate: string;
  daysLeft: number;
  currentStock: number;
  originalPrice: number;
  suggestedPrice: number;
  discountPercent: number;
}

export interface ChatMessageResponse {
  text: string;
  chartType?: 'BAR' | 'PIE' | 'LINE' | 'TABLE';
  chartData?: any[];
}

export const getForecastReplenishment = async (): Promise<ForecastRecommendation[]> => {
  const { data } = await api.get('/api/aiautomation/forecast-replenishment');
  return data;
};

export const generatePurchaseOrders = async (items: Array<{ productId: string; supplierId: string; quantity: number; unitCost: number }>): Promise<{ success: boolean; poNumbers: string[] }> => {
  const { data } = await api.post('/api/aiautomation/generate-po', { items });
  return data;
};

export const getNearExpiryMarkdowns = async (): Promise<ExpiryMarkdown[]> => {
  const { data } = await api.get('/api/aiautomation/near-expiry-markdowns');
  return data;
};

export const applyMarkdowns = async (markdowns: Array<{ productId: string; batchId: string; newPrice: number }>): Promise<{ success: boolean }> => {
  const { data } = await api.post('/api/aiautomation/apply-markdowns', { markdowns });
  return data;
};

export const sendChatMessage = async (prompt: string): Promise<ChatMessageResponse> => {
  const { data } = await api.post('/api/aiautomation/chat', { prompt });
  return data;
};
