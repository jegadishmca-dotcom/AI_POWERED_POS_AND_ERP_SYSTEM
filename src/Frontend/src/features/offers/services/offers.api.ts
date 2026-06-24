import { api } from '../../../utils/api';

export const getOffers = async () => {
  const { data } = await api.get('/api/offers');
  return data;
};

export const createOffer = async (offer: any) => {
  const { data } = await api.post('/api/offers', offer);
  return data;
};

export const updateOffer = async (id: string, offer: any) => {
  const { data } = await api.put(`/api/offers/${id}`, offer);
  return data;
};

export const deleteOffer = async (id: string) => {
  await api.delete(`/api/offers/${id}`);
};

export const getOfferUsageMetrics = async () => {
  const { data } = await api.get('/api/offers/analytics/usage');
  return data;
};
