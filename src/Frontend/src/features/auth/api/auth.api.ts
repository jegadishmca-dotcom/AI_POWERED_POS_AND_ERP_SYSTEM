import { api } from '@/utils/api';
import { AuthResponse } from '../types';

export const login = async (data: any): Promise<AuthResponse> => {
  const response = await api.post('/api/auth/login', { ...data, deviceId: 'device-123' });
  return response.data;
};

export const logout = async (): Promise<void> => {
  await api.post('/api/auth/logout');
};
