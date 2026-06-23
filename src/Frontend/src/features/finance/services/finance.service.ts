import { api } from '../../../utils/api';

export interface Account {
  id: string;
  accountCode: string;
  name: string;
  accountType: 'ASSET' | 'LIABILITY' | 'EQUITY' | 'REVENUE' | 'EXPENSE';
  parentAccountId?: string;
  isActive: boolean;
  children?: Account[];
}

export const getAccounts = async (onlyActive = true, buildTree = true): Promise<Account[]> => {
  const response = await api.get<Account[]>('/accounts', { params: { onlyActive, buildTree } });
  return response.data;
};

export const createAccount = async (data: Partial<Account>): Promise<string> => {
  const response = await api.post<{ id: string }>('/accounts', data);
  return response.data.id;
};

export const updateAccount = async (id: string, data: Partial<Account>): Promise<void> => {
  await api.put(`/accounts/${id}`, data);
};

export const toggleAccountStatus = async (id: string, isActive: boolean): Promise<void> => {
  await api.post(`/accounts/${id}/toggle`, null, { params: { isActive } });
};

export const getTrialBalance = async (storeId?: string, asOfDate?: string): Promise<any> => {
  const response = await api.get('/financialreports/trial-balance', { params: { storeId, asOfDate, format: 'json' } });
  return response.data;
};

export const getProfitAndLoss = async (storeId?: string, startDate?: string, endDate?: string): Promise<any> => {
  const response = await api.get('/financialreports/profit-and-loss', { params: { storeId, startDate, endDate, format: 'json' } });
  return response.data;
};

export const getBalanceSheet = async (storeId?: string, asOfDate?: string): Promise<any> => {
  const response = await api.get('/financialreports/balance-sheet', { params: { storeId, asOfDate, format: 'json' } });
  return response.data;
};

export const getCashFlow = async (storeId?: string, startDate?: string, endDate?: string): Promise<any> => {
  const response = await api.get('/financialreports/cash-flow', { params: { storeId, startDate, endDate, format: 'json' } });
  return response.data;
};

export const getJournalEntries = async (storeId?: string, startDate?: string, endDate?: string): Promise<any[]> => {
  const response = await api.get('/journalentries', { params: { storeId, startDate, endDate } });
  return response.data;
};
