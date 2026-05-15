$frontendDir = "d:\JEGADISH\AI_POWERED_POS_AND_ERP_SYSTEM\src\Frontend"

New-Item -ItemType Directory -Force -Path "$frontendDir\src\features\auth\types"
New-Item -ItemType Directory -Force -Path "$frontendDir\src\features\auth\api"
New-Item -ItemType Directory -Force -Path "$frontendDir\src\features\auth\store"
New-Item -ItemType Directory -Force -Path "$frontendDir\src\features\auth\components"
New-Item -ItemType Directory -Force -Path "$frontendDir\src\features\auth\routes"
New-Item -ItemType Directory -Force -Path "$frontendDir\src\utils"
New-Item -ItemType Directory -Force -Path "$frontendDir\src\components\ui"

# Types
@"
export interface User {
  id: string;
  username: string;
  fullName: string;
  role: string;
  storeId?: string;
}

export interface AuthResponse {
  accessToken: string;
  user: User;
}
"@ | Out-File -FilePath "$frontendDir\src\features\auth\types\index.ts" -Encoding utf8

# API
@"
import { api } from '@/utils/api';
import { AuthResponse } from '../types';

export const login = async (data: any): Promise<AuthResponse> => {
  const response = await api.post('/api/auth/login', { ...data, deviceId: 'device-123' });
  return response.data;
};

export const logout = async (): Promise<void> => {
  await api.post('/api/auth/logout');
};
"@ | Out-File -FilePath "$frontendDir\src\features\auth\api\auth.api.ts" -Encoding utf8

# Store
@"
import { create } from 'zustand';
import { User } from '../types';

interface AuthState {
  user: User | null;
  accessToken: string | null;
  setAuth: (user: User, token: string) => void;
  clearAuth: () => void;
}

export const useAuthStore = create<AuthState>((set) => ({
  user: null,
  accessToken: null,
  setAuth: (user, token) => set({ user, accessToken: token }),
  clearAuth: () => set({ user: null, accessToken: null }),
}));
"@ | Out-File -FilePath "$frontendDir\src\features\auth\store\auth.store.ts" -Encoding utf8

# Interceptors
@"
import axios from 'axios';
import { useAuthStore } from '../features/auth/store/auth.store';

export const api = axios.create({
  baseURL: import.meta.env.VITE_API_URL || 'http://localhost:5000',
  withCredentials: true, // Important for HttpOnly cookies
});

api.interceptors.request.use((config) => {
  const token = useAuthStore.getState().accessToken;
  if (token) {
    config.headers.Authorization = \`Bearer \${token}\`;
  }
  return config;
});

api.interceptors.response.use(
  (response) => response,
  async (error) => {
    const originalRequest = error.config;
    if (error.response?.status === 401 && !originalRequest._retry) {
      originalRequest._retry = true;
      try {
        const res = await axios.post(
          \`\${api.defaults.baseURL}/api/auth/refresh\`,
          {},
          { withCredentials: true }
        );
        useAuthStore.getState().setAuth(
          useAuthStore.getState().user!,
          res.data.accessToken
        );
        originalRequest.headers.Authorization = \`Bearer \${res.data.accessToken}\`;
        return api(originalRequest);
      } catch (refreshError) {
        useAuthStore.getState().clearAuth();
        window.location.href = '/login';
        return Promise.reject(refreshError);
      }
    }
    return Promise.reject(error);
  }
);
"@ | Out-File -FilePath "$frontendDir\src\utils\api.ts" -Encoding utf8

# Login Form Component
@"
import React from 'react';
import { useForm } from 'react-form';
import { z } from 'zod';

// Zod validation is typically done with react-hook-form + zodResolver
export const LoginForm = ({ onSubmit }: { onSubmit: (data: any) => void }) => {
  return (
    <form onSubmit={(e) => { e.preventDefault(); onSubmit({}); }} className="space-y-4">
      <div>
        <label className="block text-sm font-medium text-gray-700">Username</label>
        <input type="text" className="mt-1 block w-full rounded-md border-gray-300 shadow-sm focus:border-blue-500 focus:ring-blue-500 p-2 border" placeholder="admin" />
      </div>
      <div>
        <label className="block text-sm font-medium text-gray-700">Password</label>
        <input type="password" className="mt-1 block w-full rounded-md border-gray-300 shadow-sm focus:border-blue-500 focus:ring-blue-500 p-2 border" placeholder="••••••••" />
      </div>
      <button type="submit" className="w-full flex justify-center py-2 px-4 border border-transparent rounded-md shadow-sm text-sm font-medium text-white bg-blue-600 hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-blue-500 transition-colors">
        Log in
      </button>
    </form>
  );
};
"@ | Out-File -FilePath "$frontendDir\src\features\auth\components\LoginForm.tsx" -Encoding utf8

# Login Route
@"
import React from 'react';
import { LoginForm } from '../components/LoginForm';
import { login } from '../api/auth.api';
import { useAuthStore } from '../store/auth.store';

export const Login = () => {
  const setAuth = useAuthStore((state) => state.setAuth);

  const handleLogin = async (data: any) => {
    try {
      const response = await login(data);
      setAuth(response.user, response.accessToken);
    } catch (error) {
      console.error('Login failed', error);
    }
  };

  return (
    <div className="min-h-screen bg-slate-50 flex flex-col justify-center py-12 sm:px-6 lg:px-8">
      <div className="sm:mx-auto sm:w-full sm:max-w-md">
        <h2 className="mt-6 text-center text-3xl font-extrabold text-slate-900">
          Sign in to POS/ERP
        </h2>
      </div>

      <div className="mt-8 sm:mx-auto sm:w-full sm:max-w-md">
        <div className="bg-white py-8 px-4 shadow sm:rounded-lg sm:px-10 border border-slate-200">
          <LoginForm onSubmit={handleLogin} />
        </div>
      </div>
    </div>
  );
};
"@ | Out-File -FilePath "$frontendDir\src\features\auth\routes\Login.tsx" -Encoding utf8

Write-Host "Frontend Auth Scaffolded"
