import { useBudgets, useBudgetSummary, useCreateBudget } from '../hooks/useBudgets';
import { Card, CardHeader } from '../components/ui/Card';
import { Badge } from '../components/ui/Badge';
import { Button } from '../components/ui/Button';
import { Input } from '../components/ui/Input';
import { useState } from 'react';
import { PlusCircle } from 'lucide-react';

const CATEGORIES = [
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
  { value: 50, label: 'Education & Work' },
  { value: 51, label: 'Charitable Giving' },
  { value: 52, label: 'Pets & Animals' },
  { value: 99, label: 'Other' },
];

const fmt = (n: number) =>
  new Intl.NumberFormat('en-CA', { style: 'currency', currency: 'CAD', maximumFractionDigits: 0 }).format(n);

export const BudgetsPage = () => {
  const now = new Date();
  const [showForm, setShowForm] = useState(false);
  const [newCategory, setNewCategory] = useState(12);
  const [newLimit, setNewLimit] = useState('');

  const { data: budgets = [], isLoading } = useBudgets();
  const { data: summary } = useBudgetSummary();
  const createBudget = useCreateBudget();

  const handleCreate = async () => {
    if (!newLimit || isNaN(+newLimit)) return;
    await createBudget.mutateAsync({
      category: newCategory,
      limitAmount: parseFloat(newLimit),
      currency: 'CAD',
      month: now.getMonth() + 1,
      year: now.getFullYear(),
    });
    setShowForm(false);
    setNewLimit('');
  };

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">Budgets</h1>
          <p className="text-sm text-gray-500 mt-0.5">
            {now.toLocaleDateString('en-CA', { month: 'long', year: 'numeric' })}
          </p>
        </div>
        <Button onClick={() => setShowForm(true)}>
          <PlusCircle className="h-4 w-4" /> Add Budget
        </Button>
      </div>

      {/* Summary KPIs */}
      {summary && (
        <div className="grid grid-cols-4 gap-4">
          {[
            { label: 'Budgeted', value: fmt(summary.totalBudgeted) },
            { label: 'Spent', value: fmt(summary.totalSpent) },
            { label: 'Remaining', value: fmt(summary.totalRemaining) },
            { label: 'On Track', value: `${summary.budgetsOnTrack}/${summary.totalBudgets}` },
          ].map(({ label, value }) => (
            <Card key={label}>
              <p className="text-xs font-medium text-gray-500 uppercase tracking-wide">{label}</p>
              <p className="text-2xl font-bold text-gray-900 mt-1">{value}</p>
            </Card>
          ))}
        </div>
      )}

      {/* Add budget form */}
      {showForm && (
        <Card>
          <CardHeader title="New Budget" />
          <div className="flex gap-4 items-end">
            <div className="flex-1">
              <label className="block text-sm font-medium text-gray-700 mb-1">Category</label>
              <select
                value={newCategory}
                onChange={(e) => setNewCategory(Number(e.target.value))}
                className="block w-full rounded-lg border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-brand-500"
              >
                {CATEGORIES.map(c => <option key={c.value} value={c.value}>{c.label}</option>)}
              </select>
            </div>
            <div className="flex-1">
              <Input
                label="Monthly Limit (CAD)"
                type="number"
                min="1"
                placeholder="500"
                value={newLimit}
                onChange={(e) => setNewLimit(e.target.value)}
              />
            </div>
            <div className="flex gap-2">
              <Button onClick={handleCreate} isLoading={createBudget.isLoading}>Save</Button>
              <Button variant="secondary" onClick={() => setShowForm(false)}>Cancel</Button>
            </div>
          </div>
        </Card>
      )}

      {/* Budget list */}
      <Card>
        <CardHeader title="All Budgets" />
        {isLoading ? (
          <p className="text-sm text-gray-400 py-4">Loading...</p>
        ) : budgets.length === 0 ? (
          <p className="text-sm text-gray-400 py-8 text-center">No budgets yet. Click "Add Budget" to create one.</p>
        ) : (
          <div className="space-y-4">
            {budgets.map((budget) => (
              <div key={budget.id}>
                <div className="flex items-center justify-between mb-1.5">
                  <div className="flex items-center gap-2">
                    <span className="text-sm font-medium text-gray-900">{budget.category}</span>
                    {budget.isOverBudget && <Badge label="Over budget" variant="danger" />}
                  </div>
                  <span className="text-sm text-gray-600">
                    {fmt(budget.spentAmount)} / {fmt(budget.limitAmount)}
                  </span>
                </div>
                <div className="h-2 rounded-full bg-gray-100 overflow-hidden">
                  <div
                    className={`h-full rounded-full transition-all ${budget.isOverBudget ? 'bg-red-500' : budget.spendingPercentage > 80 ? 'bg-amber-400' : 'bg-brand-500'}`}
                    style={{ width: `${Math.min(budget.spendingPercentage, 100)}%` }}
                  />
                </div>
                <p className="text-xs text-gray-400 mt-1">
                  {budget.isOverBudget
                    ? `${fmt(budget.spentAmount - budget.limitAmount)} over limit`
                    : `${fmt(budget.remainingAmount)} remaining`}
                </p>
              </div>
            ))}
          </div>
        )}
      </Card>
    </div>
  );
};
