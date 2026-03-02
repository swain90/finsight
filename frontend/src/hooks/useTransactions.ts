import { useQuery } from 'react-query';
import { api } from '../services/api';
import type { Transaction } from '../types';

export const useTransactions = (accountId: string | null, page = 1) =>
  useQuery<Transaction[]>(['transactions', accountId, page], async () => {
    const url = accountId ? `/transactions/account/${accountId}` : '/transactions';
    const { data } = await api.get(url, {
      params: { page, pageSize: 25 }
    });
    return data;
  });
