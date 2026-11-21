import { useState } from 'react';
import { Spinner } from '../ui/Spinner';
import { Card } from '../ui/Card';
import { useNeedsAttention, useResolveTransfer } from '../../hooks/useTransactions';
import { errorMessage } from '../../services/api';

const fmt = (n: number) => new Intl.NumberFormat('en-CA', { style: 'currency', currency: 'CAD' }).format(n);
const day = (d: string) => new Date(`${d}T00:00:00`).toLocaleDateString('en-CA', { month: 'short', day: 'numeric' });

export const NeedsAttentionPanel = () => {
  const { data: items = [], isLoading, isError, error: loadError } = useNeedsAttention();
  const resolve = useResolveTransfer();
  const [error, setError] = useState<string | null>(null);

  if (isLoading) return <Card className="mb-4"><Spinner size="sm" /></Card>;
  if (isError) {
    return (
      <Card className="mb-4 border-red-200">
        <p role="alert" className="text-sm text-red-600">{errorMessage(loadError, 'Could not load transfers needing attention.')}</p>
      </Card>
    );
  }
  if (items.length === 0) return null;
  const busy = resolve.isLoading;

  const run = async (id: string, partnerId?: string) => {
    setError(null);
    try {
      await resolve.mutateAsync({ id, partnerId });
    } catch (err) {
      setError(errorMessage(err));
    }
  };

  return (
    <Card className="mb-4 border-amber-200">
      <h3 className="text-sm font-semibold text-amber-800 mb-3">
        {items.length} transfer{items.length === 1 ? '' : 's'} need attention
      </h3>
      {error && <p role="alert" className="text-sm text-red-600 mb-2">{error}</p>}
      <ul className="divide-y divide-gray-100">
        {items.map((item) => (
          <li key={item.transactionId} className="py-3">
            <p className="text-sm text-gray-900">
              {day(item.date)} · {item.accountName} · {item.description}{' '}
              <span className="font-medium">{item.type === 'Credit' ? '+' : '−'}{fmt(item.amount)}</span>
            </p>
            <div className="flex flex-wrap gap-2 mt-2">
              {item.candidates.map((c) => (
                <button
                  key={c.transactionId}
                  className="btn btn--ghost"
                  disabled={busy}
                  onClick={() => run(item.transactionId, c.transactionId)}
                >
                  Pair with {c.accountName} · {day(c.date)} · {c.description} {fmt(c.amount)}
                </button>
              ))}
              <button
                className="btn btn--ghost"
                disabled={busy}
                onClick={() => run(item.transactionId)}
              >
                Not a transfer
              </button>
            </div>
          </li>
        ))}
      </ul>
    </Card>
  );
};
