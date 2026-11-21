import { useState } from 'react';
import { Card } from '../ui/Card';
import { useNeedsAttention, useResolveTransfer } from '../../hooks/useTransactions';
import { errorMessage } from '../../services/api';

const fmt = (n: number) => new Intl.NumberFormat('en-CA', { style: 'currency', currency: 'CAD' }).format(n);
const day = (d: string) => new Date(`${d}T00:00:00`).toLocaleDateString('en-CA', { month: 'short', day: 'numeric' });

export const NeedsAttentionPanel = () => {
  const { data: items = [] } = useNeedsAttention();
  const resolve = useResolveTransfer();
  const [error, setError] = useState<string | null>(null);
  const [busyId, setBusyId] = useState<string | null>(null);

  if (items.length === 0) return null;

  const run = async (id: string, partnerId?: string) => {
    setError(null);
    setBusyId(id);
    try {
      await resolve.mutateAsync({ id, partnerId });
    } catch (err) {
      setError(errorMessage(err));
    } finally {
      setBusyId(null);
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
                  disabled={busyId === item.transactionId}
                  onClick={() => run(item.transactionId, c.transactionId)}
                >
                  Pair with {c.accountName} · {day(c.date)} · {c.description} {fmt(c.amount)}
                </button>
              ))}
              <button
                className="btn btn--ghost"
                disabled={busyId === item.transactionId}
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
