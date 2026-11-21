import { useAccounts } from '../hooks/useAccounts';
import { useBudgetSummary } from '../hooks/useBudgets';
import { useInsights } from '../hooks/useInsights';
import { Card, CardHeader } from '../components/ui/Card';
import { Badge } from '../components/ui/Badge';
import { Spinner } from '../components/ui/Spinner';
import { TrendingUp, TrendingDown, AlertTriangle, Sparkles } from 'lucide-react';
import { PieChart, Pie, Cell, ResponsiveContainer, Tooltip } from 'recharts';
import { useAuthStore } from '../store/authStore';
import { NetWorthCard } from '../components/dashboard/NetWorthCard';
import { CorporationCard } from '../components/dashboard/CorporationCard';
import { PayFromCorporationCard } from '../components/dashboard/PayFromCorporationCard';
import { fmt } from '../components/dashboard/format';

const CATEGORY_COLORS: Record<string, string> = {
  Food: '#10b981', Transportation: '#3b82f6', Entertainment: '#f59e0b',
  Shopping: '#8b5cf6', Utilities: '#ef4444', Subscription: '#ec4899',
  Healthcare: '#14b8a6', Other: '#6b7280',
};

export const DashboardPage = () => {
  const { user } = useAuthStore();
  const { data: accounts, isLoading: loadingAccounts } = useAccounts();
  const { data: summary } = useBudgetSummary();
  const { data: insights } = useInsights();

  const now = new Date();
  const hasCorporation = accounts?.some((a) => a.owner === 'Corporation') ?? false;

  const pieData = insights?.categoryBreakdown
    .filter(c => c.amount > 0)
    .map(c => ({ name: c.category, value: c.amount })) ?? [];

  return (
    <div className="space-y-6">
      {/* Header */}
      <div>
        <h1 className="text-2xl font-bold text-gray-900">
          Good {now.getHours() < 12 ? 'morning' : now.getHours() < 18 ? 'afternoon' : 'evening'},{' '}
          {user?.fullName?.split(' ')[0]} 👋
        </h1>
        <p className="text-sm text-gray-500 mt-0.5">
          {now.toLocaleDateString('en-CA', { weekday: 'long', year: 'numeric', month: 'long', day: 'numeric' })}
        </p>
      </div>

      {/* Net worth + corporation */}
      <div className="grid grid-cols-4 gap-4">
        <NetWorthCard />
        <PayFromCorporationCard />
        <CorporationCard hasCorporation={hasCorporation} />
      </div>

      {/* KPI Row */}
      <div className="grid grid-cols-3 gap-4">
        <Card>
          <p className="text-xs font-medium text-gray-500 uppercase tracking-wide">Monthly Spent</p>
          <div className="flex items-end gap-2 mt-1">
            <p className="text-2xl font-bold text-gray-900">{fmt(insights?.totalSpent ?? 0)}</p>
            <TrendingDown className="h-4 w-4 text-red-400 mb-1" />
          </div>
          <p className="text-xs text-gray-400 mt-1">vs last month</p>
        </Card>

        <Card>
          <p className="text-xs font-medium text-gray-500 uppercase tracking-wide">Monthly Income</p>
          <div className="flex items-end gap-2 mt-1">
            <p className="text-2xl font-bold text-gray-900">{fmt(insights?.totalIncome ?? 0)}</p>
            <TrendingUp className="h-4 w-4 text-green-400 mb-1" />
          </div>
          <p className={`text-xs mt-1 font-medium ${(insights?.netFlow ?? 0) >= 0 ? 'text-green-600' : 'text-red-600'}`}>
            {(insights?.netFlow ?? 0) >= 0 ? '+' : ''}{fmt(insights?.netFlow ?? 0)} net
          </p>
        </Card>

        <Card>
          <p className="text-xs font-medium text-gray-500 uppercase tracking-wide">Budget Health</p>
          <p className="text-2xl font-bold text-gray-900 mt-1">
            {summary ? `${Math.round(summary.overallSpendingPercentage)}%` : '—'}
          </p>
          <div className="flex items-center gap-1.5 mt-1">
            {(summary?.budgetsOverLimit ?? 0) > 0 ? (
              <Badge label={`${summary!.budgetsOverLimit} over limit`} variant="danger" />
            ) : (
              <Badge label="On track" variant="success" />
            )}
          </div>
        </Card>
      </div>

      {/* Main content */}
      <div className="grid grid-cols-3 gap-6">
        {/* Spending breakdown chart */}
        <Card className="col-span-1">
          <CardHeader title="Spending by Category" subtitle={now.toLocaleDateString('en-CA', { month: 'long', year: 'numeric' })} />
          {pieData.length > 0 ? (
            <>
              <ResponsiveContainer width="100%" height={200}>
                <PieChart>
                  <Pie data={pieData} cx="50%" cy="50%" innerRadius={55} outerRadius={85} dataKey="value">
                    {pieData.map((entry) => (
                      <Cell key={entry.name} fill={CATEGORY_COLORS[entry.name] ?? '#6b7280'} />
                    ))}
                  </Pie>
                  <Tooltip formatter={(v: number) => fmt(v)} />
                </PieChart>
              </ResponsiveContainer>
              <div className="space-y-1.5 mt-2">
                {pieData.slice(0, 5).map(({ name, value }) => (
                  <div key={name} className="flex items-center justify-between text-sm">
                    <div className="flex items-center gap-2">
                      <div className="h-2 w-2 rounded-full" style={{ background: CATEGORY_COLORS[name] ?? '#6b7280' }} />
                      <span className="text-gray-600">{name}</span>
                    </div>
                    <span className="font-medium text-gray-900">{fmt(value)}</span>
                  </div>
                ))}
              </div>
            </>
          ) : (
            <div className="flex items-center justify-center h-48 text-sm text-gray-400">No transactions yet</div>
          )}
        </Card>

        {/* AI Insights */}
        <Card className="col-span-2">
          <div className="flex items-center gap-2 mb-4">
            <Sparkles className="h-4 w-4 text-brand-500" />
            <h3 className="text-sm font-semibold text-gray-500 uppercase tracking-wide">AI Insights</h3>
          </div>

          {insights?.anomalyCount && insights.anomalyCount > 0 && (
            <div className="mb-4 flex gap-3 rounded-xl bg-amber-50 border border-amber-100 p-4">
              <AlertTriangle className="h-5 w-5 text-amber-500 shrink-0 mt-0.5" />
              <div>
                <p className="text-sm font-medium text-amber-800">Spending anomaly detected</p>
                <p className="text-xs text-amber-600 mt-0.5">
                  {insights.anomalyCount} unusual transaction{insights.anomalyCount > 1 ? 's' : ''} found this month.
                </p>
              </div>
            </div>
          )}

          {insights?.aiInsights?.map((insight, i) => (
            <div key={i} className="rounded-xl bg-gradient-to-br from-brand-50 to-green-50 border border-brand-100 p-4">
              <p className="text-sm text-gray-700 leading-relaxed">{insight}</p>
            </div>
          ))}

          {/* Budget alerts */}
          {summary && summary.alerts.length > 0 && (
            <div className="mt-4 space-y-2">
              <p className="text-xs font-semibold text-gray-500 uppercase tracking-wide">Budget Alerts</p>
              {summary.alerts.map((alert) => (
                <div key={alert.category} className="flex items-center justify-between rounded-lg bg-red-50 px-4 py-3">
                  <div>
                    <p className="text-sm font-medium text-red-800">{alert.category}</p>
                    <p className="text-xs text-red-600">{fmt(alert.overageAmount)} over budget</p>
                  </div>
                  <Badge label={`+${alert.overagePercentage}%`} variant="danger" />
                </div>
              ))}
            </div>
          )}
        </Card>
      </div>

      {/* Accounts overview */}
      <Card>
        <CardHeader title="Accounts" />
        {loadingAccounts ? (
          <div className="flex justify-center py-8"><Spinner /></div>
        ) : accounts?.length === 0 ? (
          <p className="text-sm text-gray-400 text-center py-8">No accounts yet. Add your first account to get started.</p>
        ) : (
          <div className="divide-y divide-gray-50">
            {accounts?.map((account) => (
              <div key={account.id} className="flex items-center justify-between py-3">
                <div>
                  <p className="text-sm font-medium text-gray-900">{account.name}</p>
                  <p className="text-xs text-gray-400">{account.institutionName ?? account.type}</p>
                </div>
                <div className="text-right">
                  <p className={`text-sm font-semibold ${account.balance >= 0 ? 'text-gray-900' : 'text-red-600'}`}>
                    {fmt(account.balance, account.currency)}
                  </p>
                  <p className="text-xs text-gray-400">{account.currency}</p>
                </div>
              </div>
            ))}
          </div>
        )}
      </Card>
    </div>
  );
};
