import { api } from '../../../utils/api';

export interface UserSettingsDto {
  id: string;
  username: string;
  fullName: string;
  roleId: string;
  roleName: string;
  isActive: boolean;
  storeId: string | null;
  createdAt: string;
}

export interface RoleDto {
  id: string;
  name: string;
  description: string;
}

export interface TerminalDto {
  id: string;
  terminalCode: string;
  name: string;
  isActive: boolean;
  createdAt: string;
}

// User CRUD APIs
export const getUsers = async (): Promise<UserSettingsDto[]> => {
  const response = await api.get('/api/settings/users');
  return response.data;
};

export const createUser = async (payload: any): Promise<any> => {
  const response = await api.post('/api/settings/users', payload);
  return response.data;
};

export const updateUser = async (id: string, payload: any): Promise<any> => {
  const response = await api.put(`/api/settings/users/${id}`, payload);
  return response.data;
};

export const changeUserPassword = async (id: string, payload: any): Promise<any> => {
  const response = await api.put(`/api/settings/users/${id}/change-password`, payload);
  return response.data;
};

export const getRoles = async (): Promise<RoleDto[]> => {
  const response = await api.get('/api/settings/roles');
  return response.data;
};

// Terminal CRUD APIs
export const getTerminals = async (): Promise<TerminalDto[]> => {
  const response = await api.get('/api/settings/terminals');
  return response.data;
};

export const createTerminal = async (payload: any): Promise<TerminalDto> => {
  const response = await api.post('/api/settings/terminals', payload);
  return response.data;
};

export const updateTerminal = async (id: string, payload: any): Promise<TerminalDto> => {
  const response = await api.put(`/api/settings/terminals/${id}`, payload);
  return response.data;
};

export const deleteTerminal = async (id: string): Promise<any> => {
  const response = await api.delete(`/api/settings/terminals/${id}`);
  return response.data;
};
