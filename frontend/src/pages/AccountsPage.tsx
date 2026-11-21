import { useAccounts, useCreateAccount, useUpdateAccount, useSettings, useUpdateSettings } from '../hooks/useAccounts';
import { Card, CardHeader } from '../components/ui/Card';
import { Button } from '../components/ui/Button';
import { Input } from '../components/ui/Input';
import { Badge } from '../components/ui/Badge';
import { useEffect, useState } from 'react';
import { PlusCircle, Building2, Pencil } from 'lucide-react';
import type { Account } from '../types';

const fmt = (n: number, currency = 'CAD') =>
  new Intl.NumberFormat('en-CA', { style: 'currency', currency }).format(n);

const ACCOUNT_TYPES = [
  { value: 1, label: 'Checking' }, { value: 2, label: 'Savings' },
  { value: 3, label: 'Credit Card' }, { value: 4, label: 'Investment' },
  { value: 5, label: 'Retirement' }, { value: 6, label: 'Mortgage' },
  { value: 7, label: 'Loan' }, { value: 8, label: 'Cash' },
  { value: 9, label: 'Line of Credit' }, { value: 10, label: 'TFSA' },
  { value: 11, label: 'RRSP' }, { value: 12, label: 'FHSA' },
  { value: 13, label: 'Crypto' },
];

const INSTITUTIONS = [
  { value: 0, label: 'Other' }, { value: 1, label: 'CIBC' }, { value: 2, label: 'TD' },
  { value: 3, label: 'MBNA' }, { value: 4, label: 'RBC' }, { value: 5, label: 'Wealthsimple' },
];

const OWNERS = [{ value: 0, label: 'Personal' }, { value: 1, label: 'Corporation' }];

const MONTHS = ['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August',
  'September', 'October', 'November', 'December'];

const selectClass =
  'block w-full rounded-lg border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-brand-500';

const enumValue = (list: { value: number; label: string }[], name: string, fallback = 0) =>
  list.find((o) => o.label.replace(/\s/g, '').toLowerCase() === name.toLowerCase())?.value ?? fallback;

interface FormState {
  name: string;
  type: number;
  currency: string;
  institutionName: string;
  institution: number;
  owner: number;
  externalAccountRef: string;
}

const EMPTY_FORM: FormState = {
  name: '', type: 1, currency: 'CAD', institutionName: '', institution: 0, owner: 0, externalAccountRef: '',
};

const FiscalYearEndField = () => {
  const { data: settings } = useSettings();
  const updateSettings = useUpdateSettings();
  const [month, setMonth] = useState(12);
  const [day, setDay] = useState(31);

  useEffect(() => {
    if (settings) {
      setMonth(settings.corporateFiscalYearEndMonth);
      setDay(settings.corporateFiscalYearEndDay);
    }
  }, [settings]);

  return (
    <Card>
      <CardHeader title="Corporation fiscal year end" />
      <div className="flex items-end gap-3">
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1">Month</label>
          <select className={selectClass} value={month} onChange={(e) => setMonth(Number(e.target.value))}>
            {MONTHS.map((m, i) => <option key={m} value={i + 1}>{m}</option>)}
          </select>
        </div>
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1">Day</label>
          <select className={selectClass} value={day} onChange={(e) => setDay(Number(e.target.value))}>
            {Array.from({ length: 31 }, (_, i) => i + 1).map((d) => <option key={d} value={d}>{d}</option>)}
          </select>
        </div>
        <Button
          isLoading={updateSettings.isLoading}
          onClick={() => updateSettings.mutate({ corporateFiscalYearEndMonth: month, corporateFiscalYearEndDay: day })}
        >
          Save
        </Button>
      </div>
      {updateSettings.isError && (
        <p className="text-sm text-red-600 mt-2">Could not save. Check that the date is valid.</p>
      )}
    </Card>
  );
};

export const AccountsPage = () => {
  const { data: accounts = [], isLoading } = useAccounts();
  const createAccount = useCreateAccount();
  const updateAccount = useUpdateAccount();
  const [showForm, setShowForm] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [form, setForm] = useState<FormState>(EMPTY_FORM);

  const closeForm = () => {
    setShowForm(false);
    setEditingId(null);
    setForm(EMPTY_FORM);
  };

  const openEdit = (a: Account) => {
    setEditingId(a.id);
    setShowForm(true);
    setForm({
      name: a.name,
      type: enumValue(ACCOUNT_TYPES, a.type, 1),
      currency: a.currency,
      institutionName: a.institutionName ?? '',
      institution: enumValue(INSTITUTIONS, a.institution),
      owner: enumValue(OWNERS, a.owner),
      externalAccountRef: a.externalAccountRef ?? '',
    });
  };

  const handleSave = async () => {
    if (!form.name) return;
    const ref = form.externalAccountRef.trim() || null;
    if (editingId) {
      await updateAccount.mutateAsync({
        id: editingId,
        payload: { name: form.name, type: form.type, institution: form.institution, owner: form.owner, externalAccountRef: ref },
      });
    } else {
      await createAccount.mutateAsync({
        name: form.name,
        type: form.type,
        currency: form.currency,
        institutionName: form.institutionName || undefined,
        institution: form.institution,
        owner: form.owner,
        externalAccountRef: ref,
      });
    }
    closeForm();
  };

  // Liabilities store a positive amount owed, so net worth = assets - liabilities.
  const totalBalance = accounts.reduce((s, a) => s + (a.isLiability ? -a.balance : a.balance), 0);

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">Accounts</h1>
          <p className="text-sm text-gray-500 mt-0.5">Net worth: {fmt(totalBalance)}</p>
        </div>
        <Button onClick={() => { setEditingId(null); setForm(EMPTY_FORM); setShowForm(true); }}>
          <PlusCircle className="h-4 w-4" /> Add Account
        </Button>
      </div>

      {showForm && (
        <Card>
          <CardHeader title={editingId ? 'Edit Account' : 'New Account'} />
          <div className="grid grid-cols-2 gap-4 mb-4">
            <Input label="Account Name" placeholder="e.g. TD Chequing" value={form.name}
              onChange={(e) => setForm({ ...form, name: e.target.value })} />
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">Institution</label>
              <select className={selectClass} value={form.institution}
                onChange={(e) => setForm({ ...form, institution: Number(e.target.value) })}>
                {INSTITUTIONS.map(i => <option key={i.value} value={i.value}>{i.label}</option>)}
              </select>
            </div>
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">Type</label>
              <select className={selectClass}
                value={form.type} onChange={(e) => setForm({ ...form, type: Number(e.target.value) })}>
                {ACCOUNT_TYPES.map(t => <option key={t.value} value={t.value}>{t.label}</option>)}
              </select>
            </div>
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">Owner</label>
              <select className={selectClass} value={form.owner}
                onChange={(e) => setForm({ ...form, owner: Number(e.target.value) })}>
                {OWNERS.map(o => <option key={o.value} value={o.value}>{o.label}</option>)}
              </select>
            </div>
            <Input label="External account ref" placeholder="e.g. last 4 digits" maxLength={50}
              value={form.externalAccountRef}
              onChange={(e) => setForm({ ...form, externalAccountRef: e.target.value })} />
            {!editingId && (
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">Currency</label>
                <select className={selectClass}
                  value={form.currency} onChange={(e) => setForm({ ...form, currency: e.target.value })}>
                  {['CAD', 'USD', 'EUR', 'GBP'].map(c => <option key={c}>{c}</option>)}
                </select>
              </div>
            )}
          </div>
          <div className="flex gap-2">
            <Button onClick={handleSave} isLoading={createAccount.isLoading || updateAccount.isLoading}>Save Account</Button>
            <Button variant="secondary" onClick={closeForm}>Cancel</Button>
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
                  <p className="text-sm text-gray-400">
                    {account.institution !== 'Other' ? account.institution.toUpperCase() : (account.institutionName ?? account.type)}
                  </p>
                </div>
                <div className="flex items-center gap-2">
                  {account.owner === 'Corporation' && <Badge label="Corporation" variant="warning" />}
                  <Badge label={account.type} variant="neutral" />
                  <button type="button" aria-label={`Edit ${account.name}`} onClick={() => openEdit(account)}
                    className="text-gray-400 hover:text-gray-700">
                    <Pencil className="h-4 w-4" />
                  </button>
                </div>
              </div>
              <p className={`text-3xl font-bold mt-4 ${account.isLiability ? 'text-red-600' : account.balance >= 0 ? 'text-gray-900' : 'text-red-600'}`}>
                {fmt(account.balance, account.currency)}
                {account.isLiability && <span className="text-sm font-medium text-gray-500 ml-2">owed</span>}
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

      <FiscalYearEndField />
    </div>
  );
};
