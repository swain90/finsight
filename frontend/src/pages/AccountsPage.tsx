import { useAccounts, useCreateAccount } from '../hooks/useAccounts';
import { Card, CardHeader } from '../components/ui/Card';
import { Button } from '../components/ui/Button';
import { Input } from '../components/ui/Input';
import { Badge } from '../components/ui/Badge';
import { useState } from 'react';
import { PlusCircle, Building2 } from 'lucide-react';

const fmt = (n: number, currency = 'CAD') =>
  new Intl.NumberFormat('en-CA', { style: 'currency', currency }).format(n);

const ACCOUNT_TYPES = [
  { value: 1, label: 'Checking' }, { value: 2, label: 'Savings' },
  { value: 3, label: 'Credit Card' }, { value: 4, label: 'Investment' },
];

export const AccountsPage = () => {
  const { data: accounts = [], isLoading } = useAccounts();
  const createAccount = useCreateAccount();
  const [showForm, setShowForm] = useState(false);
  const [form, setForm] = useState({ name: '', type: 1, currency: 'CAD', institutionName: '' });

  const handleCreate = async () => {
    if (!form.name) return;
    await createAccount.mutateAsync(form);
    setShowForm(false);
    setForm({ name: '', type: 1, currency: 'CAD', institutionName: '' });
  };

  const totalBalance = accounts.reduce((s, a) => s + a.balance, 0);

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">Accounts</h1>
          <p className="text-sm text-gray-500 mt-0.5">Net worth: {fmt(totalBalance)}</p>
        </div>
        <Button onClick={() => setShowForm(true)}>
          <PlusCircle className="h-4 w-4" /> Add Account
        </Button>
      </div>

      {showForm && (
        <Card>
          <CardHeader title="New Account" />
          <div className="grid grid-cols-2 gap-4 mb-4">
            <Input label="Account Name" placeholder="e.g. TD Chequing" value={form.name}
              onChange={(e) => setForm({ ...form, name: e.target.value })} />
            <Input label="Institution" placeholder="e.g. TD Bank" value={form.institutionName}
              onChange={(e) => setForm({ ...form, institutionName: e.target.value })} />
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">Type</label>
              <select className="block w-full rounded-lg border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-brand-500"
                value={form.type} onChange={(e) => setForm({ ...form, type: Number(e.target.value) })}>
                {ACCOUNT_TYPES.map(t => <option key={t.value} value={t.value}>{t.label}</option>)}
              </select>
            </div>
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">Currency</label>
              <select className="block w-full rounded-lg border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-brand-500"
                value={form.currency} onChange={(e) => setForm({ ...form, currency: e.target.value })}>
                {['CAD', 'USD', 'EUR', 'GBP'].map(c => <option key={c}>{c}</option>)}
              </select>
            </div>
          </div>
          <div className="flex gap-2">
            <Button onClick={handleCreate} isLoading={createAccount.isLoading}>Save Account</Button>
            <Button variant="secondary" onClick={() => setShowForm(false)}>Cancel</Button>
          </div>
        </Card>
      )}

      {isLoading ? (
        <p className="text-sm text-gray-400">Loading accounts...</p>
      ) : accounts.length === 0 ? (
        <Card>
          <div className="text-center py-12">
            <Building2 className="h-12 w-12 text-gray-300 mx-auto mb-3" />
            <p className="text-gray-500 font-medium">No accounts yet</p>
            <p className="text-sm text-gray-400 mt-1">Add your first account to start tracking</p>
          </div>
        </Card>
      ) : (
        <div className="grid grid-cols-2 gap-4">
          {accounts.map((account) => (
            <Card key={account.id} className="hover:shadow-md transition-shadow">
              <div className="flex items-start justify-between">
                <div>
                  <p className="font-semibold text-gray-900">{account.name}</p>
                  <p className="text-sm text-gray-400">{account.institutionName ?? account.type}</p>
                </div>
                <Badge label={account.type} variant="neutral" />
              </div>
              <p className={`text-3xl font-bold mt-4 ${account.balance >= 0 ? 'text-gray-900' : 'text-red-600'}`}>
                {fmt(account.balance, account.currency)}
              </p>
              <p className="text-xs text-gray-400 mt-1">{account.currency}</p>
              {account.lastSyncedAt && (
                <p className="text-xs text-gray-300 mt-3">
                  Synced {new Date(account.lastSyncedAt).toLocaleDateString()}
                </p>
              )}
            </Card>
          ))}
        </div>
      )}
    </div>
  );
};
