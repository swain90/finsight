import { useEffect, useState } from 'react';
import { CheckCircle2, AlertTriangle } from 'lucide-react';
import { Input } from '../ui/Input';
import { errorMessage, importsApi } from '../../services/api';
import type { ImportAccount, ReconciliationResult } from '../../types';
import { fmtMoney } from './ReviewTable';

export interface BalanceEntry { value: string; asOf: string }

/** Parses a typed amount ("1,204.33"); returns null when empty or not a number. */
export const parseBalance = (s: string | undefined): number | null => {
  const t = (s ?? '').replace(/[,\s$]/g, '');
  if (t === '') return null;
  const n = Number(t);
  return Number.isFinite(n) ? n : null;
};

export const RECONCILE_DEBOUNCE_MS = 400;

interface RowProps {
  batchId: string;
  account: ImportAccount;
  entry: BalanceEntry;
  /** Changes whenever included rows change, so the check re-runs. */
  rowsKey: string;
  onChange: (entry: BalanceEntry) => void;
}

const AccountBalance = ({ batchId, account, entry, rowsKey, onChange }: RowProps) => {
  const [result, setResult] = useState<ReconciliationResult | null>(null);
  const [error, setError] = useState<string | null>(null);
  const balance = parseBalance(entry.value);

  useEffect(() => {
    if (balance === null) {
      setResult(null);
      setError(null);
      return;
    }
    let cancelled = false;
    const timer = setTimeout(() => {
      importsApi
        .reconciliation(batchId, account.accountId, balance, entry.asOf || undefined)
        .then((r) => { if (!cancelled) { setResult(r); setError(null); } })
        .catch((e) => { if (!cancelled) { setResult(null); setError(errorMessage(e)); } });
    }, RECONCILE_DEBOUNCE_MS);
    return () => { cancelled = true; clearTimeout(timer); };
  }, [batchId, account.accountId, balance, entry.asOf, rowsKey]);

  const label = account.isLiability ? 'Amount owed' : 'Closing balance';

  return (
    <div className="rounded-lg border border-gray-100 p-4" data-testid="closing-balance">
      <p className="text-sm font-medium text-gray-900 mb-2">{account.name}</p>
      <div className="flex flex-wrap items-end gap-4">
        <Input
          label={label}
          id={`balance-${account.accountId}`}
          inputMode="decimal"
          placeholder="0.00"
          value={entry.value}
          onChange={(e) => onChange({ ...entry, value: e.target.value })}
        />
        <Input
          label="As of"
          id={`asof-${account.accountId}`}
          type="date"
          value={entry.asOf}
          onChange={(e) => onChange({ ...entry, asOf: e.target.value })}
        />
      </div>
      <div className="mt-2 text-sm" aria-live="polite">
        {error && <p className="text-red-600">{error}</p>}
        {result && !error && (
          result.skipped ? (
            <p className="text-gray-500">{result.skipReason}</p>
          ) : result.isBalanced ? (
            <p className="text-green-700 flex items-center gap-1">
              <CheckCircle2 className="h-4 w-4" /> ✓ Balanced
            </p>
          ) : (
            <>
              {Math.abs(result.difference ?? 0) >= 0.005 && (
                <p className="text-amber-700 flex items-center gap-1">
                  <AlertTriangle className="h-4 w-4" /> ⚠ Off by {fmtMoney(Math.abs(result.difference ?? 0))}
                </p>
              )}
              {result.warning && (
                <p className="text-amber-700 flex items-center gap-1">
                  <AlertTriangle className="h-4 w-4" /> ⚠ {result.warning}
                </p>
              )}
            </>
          )
        )}
      </div>
    </div>
  );
};

interface ClosingBalanceFormProps {
  batchId: string;
  accounts: ImportAccount[];
  entries: Record<string, BalanceEntry>;
  rowsKey: string;
  onChange: (accountId: string, entry: BalanceEntry) => void;
}

export const ClosingBalanceForm = ({ batchId, accounts, entries, rowsKey, onChange }: ClosingBalanceFormProps) => (
  <div className="space-y-3">
    {accounts.map((a) => (
      <AccountBalance
        key={a.accountId}
        batchId={batchId}
        account={a}
        entry={entries[a.accountId] ?? { value: '', asOf: '' }}
        rowsKey={rowsKey}
        onChange={(e) => onChange(a.accountId, e)}
      />
    ))}
  </div>
);
