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

export const closeShift = async (payload: { terminalId: string; cashierId: string; closingFloatCash: number; status: string }) => {
  const { data } = await api.post('/api/pos/session/close', payload);
  return data;
};

export const getZReport = async (terminalId: string, businessDate: string, cashierId: string) => {
  const { data } = await api.get('/api/pos/z-report', {
    params: { terminalId, businessDate, cashierId }
  });
  return data;
};
