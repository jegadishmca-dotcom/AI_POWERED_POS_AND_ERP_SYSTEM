import { api } from '@/utils/api';

export interface CreateInvoicePayload {
  invoiceNumber: string;
  terminalId: string;
  customerId?: string;
  promoCode?: string;
  walletAmountUsed: number;
  cashAmount: number;
  upiAmount: number;
  cardAmount: number;
  items: Array<{
    productId: string;
    quantity: number;
    unitPrice: number;
    batchId?: string;
  }>;
}

export const createInvoice = async (payload: CreateInvoicePayload): Promise<string> => {
  const { data } = await api.post('/api/pos/create', payload);
  return data;
};

export const getProductBatches = async (productId: string): Promise<any[]> => {
  const { data } = await api.get('/api/inventory/batches', {
    params: { productId }
  });
  return data;
};

export const closeShift = async (payload: { sessionId: string; actualClosingCash: number }) => {
  const { data } = await api.post('/api/pos/session/close', payload);
  return data;
};

export const forceCloseShift = async (sessionId: string): Promise<boolean> => {
  const { data } = await api.post(`/api/pos/session/force-close/${sessionId}`);
  return data.success;
};

export const forceCloseAllShifts = async (): Promise<boolean> => {
  const { data } = await api.post('/api/pos/session/force-close-all');
  return data.success;
};

export const getZReport = async (terminalId: string, businessDate: string, cashierId: string, sessionId?: string) => {
  const { data } = await api.get('/api/pos/z-report', {
    params: { terminalId, businessDate, cashierId, sessionId }
  });
  return data;
};

export const getCurrentSession = async (terminalId: string, cashierId: string) => {
  const { data } = await api.get('/api/pos/session/current', {
    params: { terminalId, cashierId }
  });
  return data;
};

export const openSession = async (payload: { terminalId: string; cashierId: string; openingFloatCash: number }) => {
  const { data } = await api.post('/api/pos/session/open', payload);
  return data;
};

export const calculateCart = async (payload: any) => {
  const { data } = await api.post('/api/pos/calculate-cart', payload);
  return data;
};

export interface ActiveBusinessDateResponse {
  isOpen: boolean;
  businessDate: string | null;
  openedAt?: string;
}

export const getActiveBusinessDate = async (storeId?: string): Promise<ActiveBusinessDateResponse> => {
  const { data } = await api.get('/api/pos/business-date/active', {
    params: { storeId }
  });
  return data;
};

export const openBusinessDate = async (payload: { businessDate: string; storeId?: string; openedBy?: string; managerOverridePin?: string }): Promise<boolean> => {
  const { data } = await api.post('/api/pos/business-date/open', payload);
  return data.success;
};

export const closeBusinessDate = async (payload: { storeId?: string; closedBy?: string }): Promise<{ success: boolean; closedDate: string }> => {
  const { data } = await api.post('/api/pos/business-date/close', payload);
  return data;
};

export interface SessionSummaryDto {
  id: string;
  terminalId: string;
  cashierId: string;
  startTime: string;
  endTime: string | null;
  openingFloatCash: number;
  expectedClosingCash: number;
  actualClosingCash: number;
  difference: number;
  status: 'OPEN' | 'CLOSED';
  cashierName: string;
  terminalCode: string;
}

export interface BusinessDateMetricsDto {
  totalInvoices: number;
  totalSales: number;
  totalTax: number;
  totalDiscount: number;
  cashCollected: number;
  cardCollected: number;
  upiCollected: number;
  walletCollected: number;
}

export const getSessionsSummary = async (): Promise<SessionSummaryDto[]> => {
  const { data } = await api.get('/api/pos/sessions/summary');
  return data;
};

export const getBusinessDateMetrics = async (businessDate: string): Promise<BusinessDateMetricsDto> => {
  const { data } = await api.get('/api/pos/business-date/metrics', {
    params: { businessDate }
  });
  return data;
};

export const holdInvoice = async (payload: any): Promise<any> => {
  const { data } = await api.post('/api/pos/invoices/hold', payload);
  return data;
};

export const getHeldInvoices = async (): Promise<any[]> => {
  const { data } = await api.get('/api/pos/invoices/held');
  return data;
};

export const deleteHeldInvoice = async (id: string): Promise<any> => {
  const { data } = await api.delete(`/api/pos/invoices/hold/${id}`);
  return data;
};

