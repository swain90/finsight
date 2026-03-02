import { useState } from 'react';
import { useQueryClient } from 'react-query';
import { useAccounts } from '../hooks/useAccounts';
import { useTransactions } from '../hooks/useTransactions';
import { transactionsApi } from '../services/api';
import { Card } from '../components/ui/Card';
import { Spinner } from '../components/ui/Spinner';
import { Plus, X, AlertTriangle } from 'lucide-react';

function fmt(n: number, currency = 'CAD') {
  return new Intl.NumberFormat('en-CA', { style: 'currency', currency }).format(n);
}

const CATEGORIES = [
  { value: 0, label: 'Uncategorized' },
  { value: 10, label: 'Housing & Rent' },
  { value: 11, label: 'Utilities' },
  { value: 12, label: 'Groceries & Food' },
  { value: 13, label: 'Transportation' },
  { value: 14, label: 'Health & Medical' },
  { value: 20, label: 'Dining Out' },
  { value: 21, label: 'Entertainment' },
  { value: 22, label: 'Shopping & Clothing' },
  { value: 23, label: 'Personal Care' },
  { value: 24, label: 'Travel & Vacation' },
  { value: 30, label: 'Debt Payments' },
  { value: 31, label: 'Savings' },
  { value: 32, label: 'Insurance' },
  { value: 33, label: 'Bank Fees' },
  { value: 40, label: 'Salary' },
  { value: 41, label: 'Freelance Income' },
  { value: 42, label: 'Investment Income' },
  { value: 43, label: 'Government Benefits' },
  { value: 50, label: 'Education & Work' },
  { value: 51, label: 'Charitable Giving' },
  { value: 52, label: 'Pets & Animals' },
  { value: 99, label: 'Other' },
];

export function TransactionsPage() {
  const qc = useQueryClient();
  const { data: accounts = [] } = useAccounts();
  const [selectedAccount, setSelectedAccount] = useState<string | null>(null);
  const { data: transactions = [], isLoading } = useTransactions(selectedAccount);
  const [showCreate, setShowCreate] = useState(false);
  const [saving, setSaving] = useState(false);
  const [filter, setFilter] = useState('');
  const [form, setForm] = useState({
    accountId: '', description: '', amount: '',
    currency: 'CAD', type: 1, category: 0,
    merchantName: '', occurredAt: new Date().toISOString().slice(0, 10)
  });

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    setSaving(true);
    try {
      await transactionsApi.record({
        accountId: form.accountId,
        description: form.description,
        amount: +form.amount,
        currency: form.currency,
        type: form.type,
        occurredAt: new Date(form.occurredAt).toISOString(),
        category: form.category || undefined,
        merchantName: form.merchantName || undefined,
      });
      setShowCreate(false);
      setSelectedAccount(form.accountId);
      await qc.invalidateQueries(['transactions', form.accountId]);
    } finally { setSaving(false); }
  };

  const filtered = transactions.filter(tx =>
    !filter || tx.description.toLowerCase().includes(filter.toLowerCase()) ||
    tx.merchantName?.toLowerCase().includes(filter.toLowerCase()) ||
    tx.category.toLowerCase().includes(filter.toLowerCase())
  );

  return (
    <div className="page">
      <header className="page-header">
        <div>
          <h1 className="page-title">Transactions</h1>
          <p className="page-subtitle">{filtered.length} records</p>
        </div>
        <button className="btn btn--primary" onClick={() => setShowCreate(true)}>
          <Plus size={16} /> Record Transaction
        </button>
      </header>

      <div className="tx-controls">
        <div className="account-tabs">
          <button
            className={`account-tab ${!selectedAccount ? 'account-tab--active' : ''}`}
            onClick={() => setSelectedAccount(null)}
          >All</button>
          {accounts.map((a) => (
            <button
              key={a.id}
              className={`account-tab ${selectedAccount === a.id ? 'account-tab--active' : ''}`}
              onClick={() => setSelectedAccount(a.id)}
            >{a.name}</button>
          ))}
        </div>
        <input
          className="search-input"
          placeholder="Search transactions..."
          value={filter}
          onChange={e => setFilter(e.target.value)}
        />
      </div>

      {isLoading ? (
        <div className="loading-state"><Spinner /></div>
      ) : (
        <Card className="tx-table-card">
          {filtered.length === 0 ? (
            <div className="empty-state">No transactions found</div>
          ) : (
            <table className="tx-table">
              <thead>
                <tr>
                  <th>Date</th>
                  <th>Description</th>
                  <th>Merchant</th>
                  <th>Category</th>
                  <th style={{ textAlign: 'right' }}>Amount</th>
                </tr>
              </thead>
              <tbody>
                {filtered.map((tx) => (
                  <tr key={tx.id} className={tx.isAnomaly ? 'tx-table__row--anomaly' : ''}>
                    <td className="tx-table__date">
                      {new Date(tx.occurredAt).toLocaleDateString('en-CA', { month: 'short', day: 'numeric' })}
                    </td>
                    <td>
                      <span className="tx-table__desc">{tx.description}</span>
                      {tx.isAnomaly && <span className="anomaly-tag"><AlertTriangle size={11} /> anomaly</span>}
                    </td>
                    <td className="tx-table__merchant">{tx.merchantName ?? '—'}</td>
                    <td><span className="cat-pill">{tx.category}</span></td>
                    <td style={{ textAlign: 'right' }}>
                      <span className={`tx-amount ${tx.type === 'Credit' ? 'tx-amount--credit' : 'tx-amount--debit'}`}>
                        {tx.type === 'Credit' ? '+' : '−'}{fmt(tx.amount, tx.currency)}
                      </span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </Card>
      )}

      {showCreate && (
        <div className="modal-backdrop" onClick={() => setShowCreate(false)}>
          <div className="modal modal--lg" onClick={e => e.stopPropagation()}>
            <div className="modal__header">
              <h2>Record Transaction</h2>
              <button className="modal__close" onClick={() => setShowCreate(false)}><X size={18} /></button>
            </div>
            <form onSubmit={submit} className="modal__body">
              <div className="field">
                <label>Account</label>
                <select value={form.accountId} onChange={e => setForm(f => ({ ...f, accountId: e.target.value }))} required>
                  <option value="">Select account…</option>
                  {accounts.map((a) => <option key={a.id} value={a.id}>{a.name}</option>)}
                </select>
              </div>
              <div className="field">
                <label>Description</label>
                <input value={form.description} onChange={e => setForm(f => ({ ...f, description: e.target.value }))} required />
              </div>
              <div className="field-row">
                <div className="field">
                  <label>Amount</label>
                  <input type="number" min="0.01" step="0.01" value={form.amount}
                    onChange={e => setForm(f => ({ ...f, amount: e.target.value }))} required />
                </div>
                <div className="field">
                  <label>Currency</label>
                  <select value={form.currency} onChange={e => setForm(f => ({ ...f, currency: e.target.value }))}>
                    {['CAD', 'USD', 'EUR', 'GBP'].map(c => <option key={c}>{c}</option>)}
                  </select>
                </div>
                <div className="field">
                  <label>Type</label>
                  <select value={form.type} onChange={e => setForm(f => ({ ...f, type: +e.target.value }))}>
                    <option value={1}>Debit</option>
                    <option value={2}>Credit</option>
                  </select>
                </div>
              </div>
              <div className="field-row">
                <div className="field">
                  <label>Category</label>
                  <select value={form.category} onChange={e => setForm(f => ({ ...f, category: +e.target.value }))}>
                    {CATEGORIES.map(c => <option key={c.value} value={c.value}>{c.label}</option>)}
                  </select>
                </div>
                <div className="field">
                  <label>Date</label>
                  <input type="date" value={form.occurredAt}
                    onChange={e => setForm(f => ({ ...f, occurredAt: e.target.value }))} required />
                </div>
              </div>
              <div className="field">
                <label>Merchant (optional)</label>
                <input value={form.merchantName} onChange={e => setForm(f => ({ ...f, merchantName: e.target.value }))} />
              </div>
              <div className="modal__actions">
                <button type="button" className="btn btn--ghost" onClick={() => setShowCreate(false)}>Cancel</button>
                <button type="submit" className="btn btn--primary" disabled={saving}>
                  {saving ? <Spinner size="sm" /> : 'Record'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
