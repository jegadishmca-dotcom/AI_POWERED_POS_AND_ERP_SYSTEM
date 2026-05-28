import { api } from '../../../utils/api';

export interface DashboardKpis {
  todaySales: number;
  todayOrders: number;
  avgOrderValue: number;
  salesGrowthPercentage: number;
}

export interface SalesTrend {
  date: string;
  grossSales: number;
  netSales: number;
  totalInvoices: number;
}

export interface TopProduct {
  productId: string;
  productName: string;
  totalQuantitySold: number;
  totalRevenue: number;
}

export const getDashboardKpis = async (): Promise<DashboardKpis> => {
  const response = await api.get('/api/analytics/dashboard');
  return response.data;
};

export const getSalesTrend = async (days: number = 7): Promise<SalesTrend[]> => {
  const response = await api.get('/api/analytics/sales-trend', { params: { days } });
  return response.data;
};

export const getTopProducts = async (): Promise<TopProduct[]> => {
  const response = await api.get('/api/analytics/top-products');
  return response.data;
};
