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

export interface PurchaseBill {
  id: string;
  storeId: string;
  supplierId: string;
  supplierName: string;
  grnHeaderId?: string;
  billNumber: string;
  billDate: string;
  subTotal: number;
  taxAmount: number;
  totalAmount: number;
  status: string;
  dueDate?: string;
  createdAt: string;
}

export interface SupplierPayment {
  id: string;
  storeId: string;
  supplierId: string;
  supplierName: string;
  paymentNumber: string;
  paymentDate: string;
  paymentMode: string;
  referenceNumber?: string;
  amount: number;
  notes?: string;
  status: string;
  createdAt: string;
}

export interface CustomerReceipt {
  id: string;
  storeId: string;
  customerId: string;
  customerName: string;
  receiptNumber: string;
  receiptDate: string;
  paymentMode: string;
  referenceNumber?: string;
  amount: number;
  notes?: string;
  createdAt: string;
}

export interface CreditMonitoring {
  customerId: string;
  customerName: string;
  phone: string;
  creditLimit: number;
  outstandingBalance: number;
  availableCredit: number;
  utilizationPercentage: number;
  overdueDays: number;
  riskLevel: 'SAFE' | 'WARNING' | 'CRITICAL';
  lastPaymentDate?: string;
  isBlocked: boolean;
}

export interface FinanceDashboardData {
  cashBalance: number;
  bankBalance: number;
  inventoryValue: number;
  accountsReceivable: number;
  accountsPayable: number;
  gstInput: number;
  gstOutput: number;
  gstPayable: number;
  workingCapital: number;
  profit: number;
  salesToday: number;
  purchasesToday: number;
}

export const getSupplierBills = async (storeId?: string): Promise<PurchaseBill[]> => {
  const params: any = {};
  if (storeId) params.storeId = storeId;
  const response = await api.get('/api/accountspayable/bills', { params });
  return Array.isArray(response.data) ? response.data : [];
};

export const getSupplierPayments = async (storeId?: string): Promise<SupplierPayment[]> => {
  const params: any = {};
  if (storeId) params.storeId = storeId;
  const response = await api.get('/api/accountspayable/payments', { params });
  return Array.isArray(response.data) ? response.data : [];
};

export const getCustomerReceipts = async (storeId?: string): Promise<CustomerReceipt[]> => {
  const params: any = {};
  if (storeId) params.storeId = storeId;
  const response = await api.get('/api/accountsreceivable/receipts', { params });
  return Array.isArray(response.data) ? response.data : [];
};

export const getCreditMonitoring = async (storeId?: string): Promise<CreditMonitoring[]> => {
  const params: any = {};
  if (storeId) params.storeId = storeId;
  const response = await api.get('/api/accountsreceivable/credit-monitoring', { params });
  return Array.isArray(response.data) ? response.data : [];
};

export const getFinanceDashboard = async (storeId?: string): Promise<FinanceDashboardData> => {
  const params: any = {};
  if (storeId) params.storeId = storeId;
  const response = await api.get('/api/finance/dashboard', { params });
  return response.data;
};

