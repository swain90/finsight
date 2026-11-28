import { useQuery, useMutation, useQueryClient } from 'react-query';
import { api, accountsApi, settingsApi } from '../services/api';
import type { Account, AppSettings } from '../types';
import type { AccountPayload } from '../services/api';

export const useAccounts = () =>
  useQuery<Account[]>('accounts', async () => {
    const { data } = await api.get('/accounts');
    return data;
  });

export const useCreateAccount = () => {
  const qc = useQueryClient();
  return useMutation(
    async (body: AccountPayload & { currency: string; institutionName?: string }) => {
      const { data } = await api.post('/accounts', body);
      return data as string;
    },
    { onSuccess: () => qc.invalidateQueries('accounts') }
  );
};

export const useUpdateAccount = () => {
  const qc = useQueryClient();
  return useMutation(
    ({ id, payload }: { id: string; payload: AccountPayload }) => accountsApi.update(id, payload),
    { onSuccess: () => qc.invalidateQueries('accounts') }
  );
};

export const useSettings = () => useQuery<AppSettings>('settings', () => settingsApi.get());

export const useUpdateSettings = () => {
  const qc = useQueryClient();
  return useMutation((payload: AppSettings) => settingsApi.update(payload), {
    onSuccess: () => qc.invalidateQueries('settings'),
  });
};
