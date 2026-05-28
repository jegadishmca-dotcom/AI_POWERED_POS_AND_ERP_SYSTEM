import { api } from '../../../utils/api';

export interface GstReportRow {
  taxRate: number;
  taxableAmount: number;
  cgstCollected: number;
  sgstCollected: number;
  cessCollected: number;
  totalTax: number;
}

export interface MarginSummary {
  totalRevenue: number;
  totalCost: number;
  totalProfit: number;
  marginPercentage: number;
}

export interface CategoryMargin {
  categoryName: string;
  quantitySold: number;
  revenue: number;
  cost: number;
  profit: number;
  marginPercentage: number;
}

export interface ProductMargin {
  productId: string;
  productName: string;
  quantitySold: number;
  revenue: number;
  cost: number;
  profit: number;
  marginPercentage: number;
}

export interface MarginReport {
  summary: MarginSummary;
  categoryMargins: CategoryMargin[];
  productMargins: ProductMargin[];
}

export interface LowStockItem {
  productId: string;
  productCode: string;
  productName: string;
  currentStock: number;
  reorderPoint: number;
}

export interface NearExpiryBatch {
  batchId: string;
  productCode: string;
  productName: string;
  batchNumber: string;
  expiryDate: string;
  daysRemaining: number;
}

export interface InventoryInsights {
  totalValuation: number;
  lowStockCount: number;
  nearExpiryCount: number;
  lowStockItems: LowStockItem[];
  nearExpiryBatches: NearExpiryBatch[];
}

export const getGstReport = async (fromDate?: string, toDate?: string): Promise<GstReportRow[]> => {
  const response = await api.get('/api/reports/gst', { params: { fromDate, toDate } });
  return response.data;
};

export const getMarginReport = async (fromDate?: string, toDate?: string): Promise<MarginReport> => {
  const response = await api.get('/api/reports/margin', { params: { fromDate, toDate } });
  return response.data;
};

export const getInventoryInsights = async (): Promise<InventoryInsights> => {
  const response = await api.get('/api/reports/inventory-insights');
  return response.data;
};
