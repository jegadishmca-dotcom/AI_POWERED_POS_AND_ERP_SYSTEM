import { api } from '../../../utils/api';

export const executiveDashboardApi = {
  getKpis: () => api.get('/api/executive/dashboard/kpis').then((res: any) => res.data),
  getTrends: (days = 30) => api.get(`/api/executive/dashboard/trends?days=${days}`).then((res: any) => res.data),
};

export const aiInsightsApi = {
  getInsights: (status?: string) => api.get(`/api/ai/insights${status ? `?status=${status}` : ''}`).then((res: any) => res.data),
  updateInsightStatus: (id: string, status: string, notes?: string) => 
    api.put(`/api/ai/insights/${id}/status`, { status, resolutionNotes: notes }).then((res: any) => res.data),
};

export const forecastApi = {
  getForecasts: (type = 'PRODUCT') => api.get(`/api/ai/forecasts?type=${type}`).then((res: any) => res.data),
  getAccuracy: () => api.get('/api/ai/forecasts/accuracy').then((res: any) => res.data),
};

export const recommendationApi = {
  getRecommendations: (businessArea?: string) => 
    api.get(`/api/ai/recommendations${businessArea ? `?businessArea=${businessArea}` : ''}`).then((res: any) => res.data),
};

export const alertCenterApi = {
  getAlerts: (severity?: string, includeResolved = false) => 
    api.get(`/api/ai/alerts?includeResolved=${includeResolved}${severity ? `&severity=${severity}` : ''}`).then((res: any) => res.data),
  acknowledgeAlert: (id: string) => api.put(`/api/ai/alerts/${id}/acknowledge`).then((res: any) => res.data),
  resolveAlert: (id: string) => api.put(`/api/ai/alerts/${id}/resolve`).then((res: any) => res.data),
};

export const storePerformanceApi = {
  getBenchmarks: () => api.get('/api/ai/store-performance').then((res: any) => res.data),
};
