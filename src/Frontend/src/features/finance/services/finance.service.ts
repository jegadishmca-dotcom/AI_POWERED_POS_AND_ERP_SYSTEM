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
  const response = await api.get('/api/accounts', { params: { onlyActive, buildTree } });
  return Array.isArray(response.data) ? response.data : [];
};

export const createAccount = async (data: Partial<Account>): Promise<string> => {
  const response = await api.post<{ id: string }>('/api/accounts', data);
  return response.data.id;
};

export const updateAccount = async (id: string, data: Partial<Account>): Promise<void> => {
  await api.put(`/api/accounts/${id}`, data);
};

export const toggleAccountStatus = async (id: string, isActive: boolean): Promise<void> => {
  await api.post(`/api/accounts/${id}/toggle`, null, { params: { isActive } });
};

export const getTrialBalance = async (storeId?: string, asOfDate?: string) => {
  const params: any = {};
  if (storeId) params.storeId = storeId;
  if (asOfDate) params.asOfDate = asOfDate;
  const response = await api.get('/api/financialreports/trial-balance', { params });
  return response.data;
};

export const getProfitAndLoss = async (storeId?: string, startDate?: string, endDate?: string) => {
  const params: any = {};
  if (storeId) params.storeId = storeId;
  if (startDate) params.startDate = startDate;
  if (endDate) params.endDate = endDate;
  const response = await api.get('/api/financialreports/profit-and-loss', { params });
  return response.data;
};

export const getBalanceSheet = async (storeId?: string, asOfDate?: string) => {
  const params: any = {};
  if (storeId) params.storeId = storeId;
  if (asOfDate) params.asOfDate = asOfDate;
  const response = await api.get('/api/financialreports/balance-sheet', { params });
  return response.data;
};

export const getCashFlow = async (storeId?: string, startDate?: string, endDate?: string) => {
  const params: any = {};
  if (storeId) params.storeId = storeId;
  if (startDate) params.startDate = startDate;
  if (endDate) params.endDate = endDate;
  const response = await api.get('/api/financialreports/cash-flow', { params });
  return response.data;
};

export const getCashPosition = async () => {
  const response = await api.get('/api/financialreports/cash-position');
  return response.data;
};

export const getJournalEntries = async (storeId?: string, startDate?: string, endDate?: string): Promise<any[]> => {
  const params: any = {};
  if (storeId) params.storeId = storeId;
  if (startDate) params.startDate = startDate;
  if (endDate) params.endDate = endDate;
  const response = await api.get('/api/journalentries', { params });
  return Array.isArray(response.data) ? response.data : [];
};
