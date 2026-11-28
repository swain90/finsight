import { useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { Card, CardHeader } from '../components/ui/Card';
import { Button } from '../components/ui/Button';
import { Spinner } from '../components/ui/Spinner';
import { UploadDropzone } from '../components/imports/UploadDropzone';
import { ReviewTable } from '../components/imports/ReviewTable';
import { ClosingBalanceForm, parseBalance, type BalanceEntry } from '../components/imports/ClosingBalanceForm';
import { ImportHistory } from '../components/imports/ImportHistory';
import { errorCode, errorMessage } from '../services/api';
import {
  useCommitImport, useImport, useRemoveImport, useUpdateImportRow, useUploadImport,
} from '../hooks/useImports';
import type { ImportBatch, ImportRowPatch } from '../types';

const selectClass =
  'rounded-md border border-gray-300 px-2 py-1 text-sm focus:outline-none focus:ring-2 focus:ring-brand-500';

interface ReviewProps {
  batch: ImportBatch;
  onClose: () => void;
  onCommitted: (batch: ImportBatch) => void;
}

const Review = ({ batch, onClose, onCommitted }: ReviewProps) => {
  const updateRow = useUpdateImportRow(batch.id);
  const commit = useCommitImport();
  const discard = useRemoveImport();
  const { refetch } = useImport(batch.id);
  const [entries, setEntries] = useState<Record<string, BalanceEntry>>({});
  const [error, setError] = useState<string | null>(null);
  const [assigning, setAssigning] = useState(false);

  const included = useMemo(() => batch.rows.filter((r) => !r.isExcluded), [batch.rows]);
  const unassignedIncluded = included.filter((r) => r.accountId === null).length;

  // Accounts that have at least one included row need a closing balance.
  const balanceAccounts = useMemo(() => {
    const ids = new Set(included.map((r) => r.accountId).filter((id): id is string => id !== null));
    return batch.accounts.filter((a) => ids.has(a.accountId));
  }, [included, batch.accounts]);

  // One group per assigned account (null = not yet assigned), in file order.
  const accountGroups = useMemo(() => {
    const counts = new Map<string | null, number>();
    for (const r of batch.rows) counts.set(r.accountId, (counts.get(r.accountId) ?? 0) + 1);
    return [...counts.entries()].map(([accountId, count]) => ({ accountId, count }));
  }, [batch.rows]);

  const rowsKey = included.map((r) => `${r.id}:${r.accountId}:${r.isExcluded}`).join('|');

  // Default each account's "as of" to the statement's PeriodEnd. A PDF statement's closing balance belongs to
  // its one account, so it pre-fills only while the batch has exactly one; an existing entry (possibly typed by
  // the user) is never overwritten.
  useEffect(() => {
    setEntries((prev) => {
      const next = { ...prev };
      const statementBalance =
        balanceAccounts.length === 1 && batch.statementClosingBalance != null
          ? batch.statementClosingBalance.toFixed(2)
          : '';
      for (const a of balanceAccounts) {
        if (!next[a.accountId]) {
          next[a.accountId] = { value: statementBalance, asOf: batch.periodEnd ?? '' };
        }
      }
      return next;
    });
  }, [balanceAccounts, batch.periodEnd, batch.statementClosingBalance]);

  /**
   * Moves rows to another account. Sequential (R22): the server recomputes duplicate fingerprints per
   * account on every PATCH. Afterwards the whole batch is refetched, because sibling rows of either
   * account can flip to/from duplicate and excluded (R27).
   */
  const assignRows = async (rowIds: string[], accountId: string) => {
    if (assigning || rowIds.length === 0) return;
    setAssigning(true);
    setError(null);
    try {
      for (const rowId of rowIds) {
        await updateRow.mutateAsync({ rowId, patch: { accountId } });
      }
    } catch (e) {
      setError(errorMessage(e));
    } finally {
      await refetch({ cancelRefetch: true });
      setAssigning(false);
    }
  };

  const reassignGroup = (from: string | null, to: string) => {
    if (!to || to === from) return;
    assignRows(batch.rows.filter((r) => r.accountId === from).map((r) => r.id), to);
  };

  const edit = (rowId: string, patch: ImportRowPatch) => {
    if (patch.accountId) {
      assignRows([rowId], patch.accountId);
      return;
    }
    setError(null);
    updateRow.mutateAsync({ rowId, patch }).catch((e) => setError(errorMessage(e)));
  };

  const allBalancesEntered = balanceAccounts.every((a) => {
    const e = entries[a.accountId];
    return !!e && parseBalance(e.value) !== null && e.asOf !== '';
  });
  const canCommit =
    included.length > 0 && unassignedIncluded === 0 && allBalancesEntered && balanceAccounts.length > 0;

  const doCommit = async () => {
    setError(null);
    try {
      const result = await commit.mutateAsync({
        id: batch.id,
        balances: balanceAccounts.map((a) => ({
          accountId: a.accountId,
          closingBalance: parseBalance(entries[a.accountId].value) as number,
          asOf: entries[a.accountId].asOf,
        })),
      });
      onCommitted(result);
    } catch (e) {
      setError(errorMessage(e));
      if (errorCode(e) === 'Import.AlreadyImported') refetch(); // show newly marked duplicates
    }
  };

  const doDiscard = async () => {
    setError(null);
    try {
      await discard.mutateAsync(batch.id);
      onClose();
    } catch (e) {
      setError(errorMessage(e));
    }
  };

  return (
    <Card>
      <div className="flex flex-wrap items-center gap-x-6 gap-y-2 mb-4 text-sm">
        <span className="text-gray-700">File: <span className="font-medium">{batch.fileName}</span></span>
        <span className="text-gray-700">Detected: <span className="font-medium">{batch.institution}</span></span>
        {accountGroups.map(({ accountId, count }) => (
          <label key={accountId ?? 'unassigned'} className="text-gray-700" data-testid="account-group">
            Account:{' '}
            <select
              aria-label={
                accountGroups.length === 1
                  ? 'Account for all rows'
                  : `Account for ${count} ${accountId === null ? 'unassigned ' : ''}rows`
              }
              disabled={assigning || batch.accounts.length === 0}
              className={`${selectClass} ${accountId === null ? 'border-amber-400 bg-amber-50' : ''}`}
              value={accountId ?? ''}
              onChange={(e) => reassignGroup(accountId, e.target.value)}
            >
              {accountId === null && <option value="">Choose account...</option>}
              {batch.accounts.map((a) => <option key={a.accountId} value={a.accountId}>{a.name}</option>)}
            </select>
            {accountGroups.length > 1 && <span className="text-gray-400"> · {count} rows</span>}
          </label>
        ))}
      </div>
      {batch.accounts.length === 0 && (
        <p className="text-sm text-amber-700 mb-4" role="note">
          No account to import into. Set this institution on one of your accounts first (
          <Link to="/accounts" className="font-medium text-brand-600 hover:underline">Accounts page</Link>).
        </p>
      )}

      <ReviewTable rows={batch.rows} accounts={batch.accounts} accountsDisabled={assigning} onUpdate={edit} />

      {balanceAccounts.length > 0 && (
        <div className="mt-6">
          <ClosingBalanceForm
            batchId={batch.id}
            accounts={balanceAccounts}
            entries={entries}
            rowsKey={rowsKey}
            onChange={(id, entry) => setEntries((prev) => ({ ...prev, [id]: entry }))}
          />
        </div>
      )}

      {error && <p role="alert" className="text-sm text-red-600 mt-4">{error}</p>}

      <div className="mt-6 flex justify-end gap-2">
        <Button variant="secondary" onClick={doDiscard} isLoading={discard.isLoading}>Discard</Button>
        <Button onClick={doCommit} disabled={!canCommit} isLoading={commit.isLoading}>
          Commit {included.length}
        </Button>
      </div>
    </Card>
  );
};

export const ImportPage = () => {
  const [batchId, setBatchId] = useState<string | null>(null);
  const [committed, setCommitted] = useState<ImportBatch | null>(null);
  const [uploadError, setUploadError] = useState<string | null>(null);
  const upload = useUploadImport();
  const { data: batch, isLoading, error: loadError } = useImport(batchId);

  const handleFile = async (file: File) => {
    setUploadError(null);
    setCommitted(null);
    try {
      const b = await upload.mutateAsync({ file });
      setBatchId(b.id);
    } catch (e) {
      setUploadError(errorMessage(e));
    }
  };

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold text-gray-900">Import</h1>
        <p className="text-sm text-gray-500 mt-0.5">Upload a monthly statement, review it and commit.</p>
      </div>

      {committed && (
        <Card>
          <CardHeader title="Import committed" />
          <p className="text-sm text-gray-700" data-testid="commit-summary">
            Imported {committed.rows.filter((r) => !r.isExcluded).length} transactions from {committed.fileName}.
          </p>
          {committed.reconciliationWarning && (
            <p className="text-sm text-amber-700 mt-2">⚠ {committed.reconciliationWarning}</p>
          )}
          <Link to="/transactions" className="inline-block mt-3 text-sm font-medium text-brand-600 hover:underline">
            View transactions
          </Link>
        </Card>
      )}

      {!batchId && (
        <UploadDropzone onFile={handleFile} isLoading={upload.isLoading} error={uploadError} />
      )}

      {batchId && isLoading && <div className="flex justify-center py-8"><Spinner /></div>}
      {batchId && loadError != null && (
        <p role="alert" className="text-sm text-red-600">{errorMessage(loadError)}</p>
      )}
      {batchId && batch && (
        <Review
          key={batch.id}
          batch={batch}
          onClose={() => setBatchId(null)}
          onCommitted={(b) => { setCommitted(b); setBatchId(null); }}
        />
      )}

      <ImportHistory
        onOpen={(id) => { setCommitted(null); setBatchId(id); }}
        onRemoved={(id) => { if (id === batchId) setBatchId(null); }}
      />
    </div>
  );
};
