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
  }>;
}

export const createInvoice = async (payload: CreateInvoicePayload): Promise<string> => {
  const { data } = await api.post('/api/pos/create', payload);
  return data;
};
