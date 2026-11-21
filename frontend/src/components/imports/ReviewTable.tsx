import { Badge } from '../ui/Badge';
import type { ImportAccount, ImportRow, ImportRowPatch } from '../../types';
import { CATEGORIES, categoryValue } from './categories';

interface ReviewTableProps {
  rows: ImportRow[];
  accounts: ImportAccount[];
  readOnly?: boolean;
  onUpdate: (rowId: string, patch: ImportRowPatch) => void;
}

const selectClass =
  'rounded-md border border-gray-300 px-2 py-1 text-sm focus:outline-none focus:ring-2 focus:ring-brand-500 disabled:bg-gray-50';

export const signedAmount = (r: ImportRow) => (r.type === 'Debit' ? -r.amount : r.amount);

export const fmtMoney = (n: number) =>
  new Intl.NumberFormat('en-CA', { style: 'currency', currency: 'CAD' }).format(n);

const fmtDate = (iso: string) =>
  new Date(`${iso}T00:00:00`).toLocaleDateString('en-CA', { month: 'short', day: '2-digit' });

export const ReviewTable = ({ rows, accounts, readOnly, onUpdate }: ReviewTableProps) => (
  <div className="overflow-x-auto">
    <table className="w-full text-sm">
      <thead>
        <tr className="text-left text-xs uppercase tracking-wide text-gray-500 border-b border-gray-100">
          <th className="py-2 pr-3 font-medium">Date</th>
          <th className="py-2 pr-3 font-medium">Description</th>
          <th className="py-2 pr-3 font-medium text-right">Amount</th>
          <th className="py-2 pr-3 font-medium">Category</th>
          <th className="py-2 pr-3 font-medium">Status</th>
          <th className="py-2 font-medium text-center">Exclude</th>
        </tr>
      </thead>
      <tbody>
        {rows.map((r) => {
          const amount = signedAmount(r);
          const uncategorized = r.category === 'Uncategorized';
          return (
            <tr
              key={r.id}
              data-testid="import-row"
              className={`border-b border-gray-50 ${r.isExcluded ? 'opacity-50' : ''}`}
            >
              <td className="py-2 pr-3 whitespace-nowrap text-gray-600">{fmtDate(r.date)}</td>
              <td className="py-2 pr-3 text-gray-900">
                {r.description}
                {r.accountId === null && !readOnly && (
                  <select
                    aria-label={`Account for ${r.description}`}
                    className={`${selectClass} ml-2`}
                    value=""
                    onChange={(e) => e.target.value && onUpdate(r.id, { accountId: e.target.value })}
                  >
                    <option value="">Choose account...</option>
                    {accounts.map((a) => <option key={a.accountId} value={a.accountId}>{a.name}</option>)}
                  </select>
                )}
              </td>
              <td className={`py-2 pr-3 text-right whitespace-nowrap font-medium ${amount < 0 ? 'text-gray-900' : 'text-green-700'}`}>
                {amount > 0 ? '+' : ''}{fmtMoney(amount)}
              </td>
              <td className="py-2 pr-3">
                <select
                  aria-label={`Category for ${r.description}`}
                  className={`${selectClass} ${uncategorized ? 'border-amber-400 bg-amber-50' : ''}`}
                  value={categoryValue(r.category)}
                  disabled={readOnly}
                  onChange={(e) => onUpdate(r.id, { category: Number(e.target.value) })}
                >
                  {CATEGORIES.map((c) => <option key={c.value} value={c.value}>{c.label}</option>)}
                </select>
              </td>
              <td className="py-2 pr-3">
                <div className="flex flex-wrap gap-1">
                  {r.isDuplicate && <Badge label="duplicate" variant="neutral" />}
                  {r.kindHint === 'PendingTransfer' && <Badge label="⇄ likely transfer" variant="warning" />}
                  {uncategorized && <Badge label="uncategorized" variant="warning" />}
                </div>
              </td>
              <td className="py-2 text-center">
                <input
                  type="checkbox"
                  aria-label={`Exclude ${r.description}`}
                  checked={r.isExcluded}
                  disabled={readOnly}
                  onChange={(e) => onUpdate(r.id, { isExcluded: e.target.checked })}
                />
              </td>
            </tr>
          );
        })}
      </tbody>
    </table>
  </div>
);
