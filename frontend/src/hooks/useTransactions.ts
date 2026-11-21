import { useQuery, useMutation, useQueryClient } from 'react-query';
import { api, transactionsApi } from '../services/api';
import type { NeedsAttentionItem, Transaction } from '../types';

export const useTransactions = (accountId: string | null, page = 1) =>
  useQuery<Transaction[]>(['transactions', accountId, page], async () => {
    const url = accountId ? `/transactions/account/${accountId}` : '/transactions';
    const { data } = await api.get(url, {
      params: { page, pageSize: 25 }
    });
    return data;
  });

export const useNeedsAttention = () =>
  useQuery<NeedsAttentionItem[]>('needs-attention', () => transactionsApi.needsAttention());

export const useResolveTransfer = () => {
  const qc = useQueryClient();
  return useMutation(
    ({ id, partnerId }: { id: string; partnerId?: string }) => transactionsApi.resolveTransfer(id, partnerId),
    {
      onSuccess: () =>
        Promise.all(['needs-attention', 'transactions', 'reports', 'insights', 'accounts'].map((k) => qc.invalidateQueries(k))),
    }
  );
};
