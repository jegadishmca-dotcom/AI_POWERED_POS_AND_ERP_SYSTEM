import axios from 'axios';

// Ensure the base URL points to your API
const API_BASE_URL = import.meta.env.VITE_API_URL || 'http://localhost:5000/api';

const api = axios.create({
  baseURL: API_BASE_URL,
});

// Attach token interceptor if auth is implemented
api.interceptors.request.use((config) => {
  const token = localStorage.getItem('token');
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

export const executiveDashboardApi = {
  getKpis: () => api.get('/executive/dashboard/kpis').then(res => res.data),
  getTrends: (days = 30) => api.get(`/executive/dashboard/trends?days=${days}`).then(res => res.data),
};

export const aiInsightsApi = {
  getInsights: (status?: string) => api.get(`/ai/insights${status ? `?status=${status}` : ''}`).then(res => res.data),
  updateInsightStatus: (id: string, status: string, notes?: string) => 
    api.put(`/ai/insights/${id}/status`, { status, resolutionNotes: notes }).then(res => res.data),
};

export const forecastApi = {
  getForecasts: (type = 'PRODUCT') => api.get(`/ai/forecasts?type=${type}`).then(res => res.data),
  getAccuracy: () => api.get('/ai/forecasts/accuracy').then(res => res.data),
};

export const recommendationApi = {
  getRecommendations: (businessArea?: string) => 
    api.get(`/ai/recommendations${businessArea ? `?businessArea=${businessArea}` : ''}`).then(res => res.data),
};

export const alertCenterApi = {
  getAlerts: (severity?: string, includeResolved = false) => 
    api.get(`/ai/alerts?includeResolved=${includeResolved}${severity ? `&severity=${severity}` : ''}`).then(res => res.data),
  acknowledgeAlert: (id: string) => api.put(`/ai/alerts/${id}/acknowledge`).then(res => res.data),
  resolveAlert: (id: string) => api.put(`/ai/alerts/${id}/resolve`).then(res => res.data),
};

export const storePerformanceApi = {
  // Placeholder for the Store Benchmark API which may be part of executive dashboard or insights
  getBenchmarks: () => api.get('/ai/store-performance').then(res => res.data),
};
