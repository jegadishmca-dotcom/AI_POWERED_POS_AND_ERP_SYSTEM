import axios from 'axios';
import { useAuthStore } from '../features/auth/store/auth.store';

export const getServerUrl = () => {
  const savedIp = localStorage.getItem('pos_server_ip');
  if (savedIp) {
    return savedIp.startsWith('http') ? savedIp : `https://${savedIp}`; // Default to https for remote
  }
  return import.meta.env.VITE_API_URL || '';
};

export const api = axios.create({
  baseURL: getServerUrl(),
  withCredentials: true, // Important for HttpOnly cookies
});

api.interceptors.request.use((config) => {
  const serverUrl = getServerUrl();
  config.baseURL = serverUrl;
  
  // If no server URL is configured, throw a clear error instead of sending to Vercel root
  if (!serverUrl) {
    return Promise.reject(new Error("SERVER_URL_MISSING"));
  }

  const token = useAuthStore.getState().accessToken;
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

api.interceptors.response.use(
  (response) => response,
  async (error) => {
    // Check if we manually rejected due to missing server URL
    if (error.message === "SERVER_URL_MISSING") {
      return Promise.reject(error);
    }

    const originalRequest = error.config;
    if (error.response?.status === 401 && !originalRequest._retry) {
      originalRequest._retry = true;
      try {
        const res = await axios.post(
          `${getServerUrl()}/api/auth/refresh`,
          {},
          { withCredentials: true }
        );
        useAuthStore.getState().setAuth(
          useAuthStore.getState().user!,
          res.data.accessToken
        );
        originalRequest.headers.Authorization = `Bearer ${res.data.accessToken}`;
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
