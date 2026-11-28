import { useState } from 'react';
import { Badge } from '../ui/Badge';
import { Button } from '../ui/Button';
import { Card, CardHeader } from '../ui/Card';
import { errorMessage } from '../../services/api';
import { useImportHistory, useRemoveImport } from '../../hooks/useImports';
import type { ImportBatchSummary } from '../../types';

interface ImportHistoryProps {
  onOpen: (id: string) => void;
  /** Called after a batch is removed so the page can clear it if it was open. */
  onRemoved: (id: string) => void;
}

const STATUS: Record<string, { label: string; variant: 'success' | 'warning' | 'neutral' }> = {
  PendingReview: { label: 'Pending review', variant: 'warning' },
  Committed: { label: 'Committed', variant: 'success' },
  Discarded: { label: 'Discarded', variant: 'neutral' },
};

export const ImportHistory = ({ onOpen, onRemoved }: ImportHistoryProps) => {
  const { data: imports = [], isLoading } = useImportHistory();
  const remove = useRemoveImport();
  const [confirming, setConfirming] = useState<ImportBatchSummary | null>(null);
  const [error, setError] = useState<string | null>(null);

  const doRemove = async (b: ImportBatchSummary) => {
    setError(null);
    try {
      await remove.mutateAsync(b.id);
      onRemoved(b.id);
    } catch (e) {
      setError(errorMessage(e));
    } finally {
      setConfirming(null);
    }
  };

  return (
    <Card>
      <CardHeader title="Import history" />
      {error && <p role="alert" className="text-sm text-red-600 mb-2">{error}</p>}
      {isLoading ? (
        <p className="text-sm text-gray-400">Loading...</p>
      ) : imports.length === 0 ? (
        <p className="text-sm text-gray-400">No imports yet.</p>
      ) : (
        <ul className="divide-y divide-gray-100">
          {imports.map((b) => {
            const s = STATUS[b.status] ?? { label: b.status, variant: 'neutral' as const };
            return (
              <li key={b.id} className="flex items-center justify-between gap-3 py-3" data-testid="import-history-item">
                <div className="min-w-0">
                  <p className="text-sm font-medium text-gray-900 truncate">{b.fileName}</p>
                  <p className="text-xs text-gray-400">
                    {b.institution} ·{' '}
                    {b.status === 'Committed'
                      ? `${b.includedRowCount} imported of ${b.rowCount} rows`
                      : `${b.rowCount} rows`}
                    {b.periodStart && b.periodEnd && ` · ${b.periodStart} to ${b.periodEnd}`}
                  </p>
                </div>
                <div className="flex items-center gap-2 shrink-0">
                  <Badge label={s.label} variant={s.variant} />
                  {b.status === 'PendingReview' && (
                    <>
                      <Button size="sm" variant="secondary" onClick={() => onOpen(b.id)}>Review</Button>
                      <Button size="sm" variant="ghost" onClick={() => doRemove(b)} disabled={remove.isLoading}>
                        Discard
                      </Button>
                    </>
                  )}
                  {b.status === 'Committed' && (
                    <Button size="sm" variant="danger" onClick={() => setConfirming(b)}>Undo</Button>
                  )}
                </div>
              </li>
            );
          })}
        </ul>
      )}

      {confirming && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
          <div role="dialog" aria-modal="true" aria-labelledby="undo-title"
            className="w-full max-w-md rounded-2xl bg-white p-6 shadow-xl">
            <h2 id="undo-title" className="text-lg font-semibold text-gray-900">Undo this import?</h2>
            <p className="text-sm text-gray-600 mt-2">
              This removes the {confirming.includedRowCount}{' '}
              {confirming.includedRowCount === 1 ? 'transaction' : 'transactions'} imported from {confirming.fileName} and
              reverses their effect on your balances. This cannot be undone.
            </p>
            <div className="mt-5 flex justify-end gap-2">
              <Button variant="secondary" onClick={() => setConfirming(null)}>Cancel</Button>
              <Button variant="danger" isLoading={remove.isLoading} onClick={() => doRemove(confirming)}>
                Undo import
              </Button>
            </div>
          </div>
        </div>
      )}
    </Card>
  );
};
