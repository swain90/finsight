import { useQuery } from 'react-query';
import { api } from '../services/api';
import type { SpendingInsights } from '../types';

const now = new Date();

export const useInsights = (month = now.getMonth() + 1, year = now.getFullYear()) =>
  useQuery<SpendingInsights>(['insights', month, year], async () => {
    const { data } = await api.get(`/insights/spending/${year}/${month}`);
    return data;
  });
