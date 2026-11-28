import { useQuery, useMutation, useQueryClient } from 'react-query';
import { importsApi } from '../services/api';
import type { ClosingBalanceInput, ImportBatch, ImportBatchSummary, ImportRowPatch } from '../types';

export const useImportHistory = () =>
  useQuery<ImportBatchSummary[]>('imports', () => importsApi.list());

export const useImport = (id: string | null) =>
  useQuery<ImportBatch>(['import', id], () => importsApi.get(id as string), {
    enabled: !!id,
    staleTime: Infinity,
    // The batch's account picker lists the user's current accounts, which can change on the Accounts page while
    // the batch sits in the cache, so reopening a batch always refetches it.
    refetchOnMount: 'always',
  });

export const useUploadImport = () => {
  const qc = useQueryClient();
  return useMutation(
    ({ file, accountId }: { file: File; accountId?: string }) => importsApi.upload(file, accountId),
    {
      onSuccess: (batch) => {
        qc.setQueryData(['import', batch.id], batch);
        qc.invalidateQueries('imports');
      },
    }
  );
};

export const useUpdateImportRow = (batchId: string) => {
  const qc = useQueryClient();
  return useMutation(
    ({ rowId, patch }: { rowId: string; patch: ImportRowPatch }) => importsApi.updateRow(batchId, rowId, patch),
    {
      onSuccess: (row) => {
        qc.setQueryData<ImportBatch | undefined>(['import', batchId], (old) =>
          old ? { ...old, rows: old.rows.map((r) => (r.id === row.id ? row : r)) } : old
        );
      },
    }
  );
};

export const useCommitImport = () => {
  const qc = useQueryClient();
  return useMutation(
    ({ id, balances }: { id: string; balances: ClosingBalanceInput[] }) => importsApi.commit(id, balances),
    {
      onSuccess: (batch) => {
        qc.setQueryData(['import', batch.id], batch);
        // Commit changes balances, transactions and reports.
        qc.invalidateQueries();
      },
    }
  );
};

export const useRemoveImport = () => {
  const qc = useQueryClient();
  return useMutation((id: string) => importsApi.remove(id), {
    onSuccess: (_, id) => {
      qc.removeQueries(['import', id]);
      qc.invalidateQueries();
    },
  });
};
