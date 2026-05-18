import { api } from '@/utils/api';

export interface CustomerDto {
  id: string;
  phone: string;
  name: string;
  walletBalance: number;
  loyaltyPoints: number;
  tierName: string;
}

export interface RegisterCustomerPayload {
  phone: string;
  name: string;
  tamilName?: string;
  dob?: string;
  marketingConsent: boolean;
}

export const searchCustomers = async (q: string): Promise<CustomerDto[]> => {
  const { data } = await api.get('/api/customers/search', { params: { q } });
  return data;
};

export const registerCustomer = async (payload: RegisterCustomerPayload): Promise<string> => {
  const { data } = await api.post('/api/customers', payload);
  return data;
};
