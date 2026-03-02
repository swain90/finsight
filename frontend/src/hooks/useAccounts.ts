import { useQuery, useMutation, useQueryClient } from 'react-query';
import { api } from '../services/api';
import type { Account } from '../types';

export const useAccounts = () =>
  useQuery<Account[]>('accounts', async () => {
    const { data } = await api.get('/accounts');
    return data;
  });

export const useCreateAccount = () => {
  const qc = useQueryClient();
  return useMutation(
    async (body: { name: string; type: number; currency: string; institutionName?: string }) => {
      const { data } = await api.post('/accounts', body);
      return data as string;
    },
    { onSuccess: () => qc.invalidateQueries('accounts') }
  );
};
