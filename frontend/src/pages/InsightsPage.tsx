import { useInsights } from '../hooks/useInsights';
import { Card, CardHeader } from '../components/ui/Card';
import { Sparkles, TrendingUp, TrendingDown, AlertTriangle } from 'lucide-react';
import {
  BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip,
  ResponsiveContainer
} from 'recharts';
import { useState } from 'react';

const fmt = (n: number) =>
  new Intl.NumberFormat('en-CA', { style: 'currency', currency: 'CAD', maximumFractionDigits: 0 }).format(n);

const MONTHS = ['Jan','Feb','Mar','Apr','May','Jun','Jul','Aug','Sep','Oct','Nov','Dec'];

export const InsightsPage = () => {
  const now = new Date();
  const [month, setMonth] = useState(now.getMonth() + 1);
  const [year, setYear] = useState(now.getFullYear());
  const { data: insights } = useInsights(month, year);

  const barData = insights?.categoryBreakdown.map(c => ({
    name: c.category.substring(0, 8),  // truncate for chart
    amount: c.amount,
    percentage: c.percentageOfTotal,
  })) ?? [];

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">AI Insights</h1>
          <p className="text-sm text-gray-500 mt-0.5">Powered by FinSight Intelligence</p>
        </div>
        {/* Month picker */}
        <div className="flex items-center gap-2">
          <select className="rounded-lg border border-gray-300 px-3 py-2 text-sm"
            value={month} onChange={e => setMonth(+e.target.value)}>
            {MONTHS.map((m, i) => <option key={m} value={i + 1}>{m}</option>)}
          </select>
          <select className="rounded-lg border border-gray-300 px-3 py-2 text-sm"
            value={year} onChange={e => setYear(+e.target.value)}>
            {[2024, 2025, 2026].map(y => <option key={y}>{y}</option>)}
          </select>
        </div>
      </div>

      {/* Summary KPIs */}
      <div className="grid grid-cols-3 gap-4">
        <Card>
          <p className="text-xs font-medium text-gray-500 uppercase tracking-wide">Total Spent</p>
          <div className="flex items-end gap-2 mt-1">
            <p className="text-2xl font-bold text-gray-900">{fmt(insights?.totalSpent ?? 0)}</p>
            <TrendingDown className="h-4 w-4 text-red-400 mb-1" />
          </div>
        </Card>
        <Card>
          <p className="text-xs font-medium text-gray-500 uppercase tracking-wide">Total Income</p>
          <div className="flex items-end gap-2 mt-1">
            <p className="text-2xl font-bold text-gray-900">{fmt(insights?.totalIncome ?? 0)}</p>
            <TrendingUp className="h-4 w-4 text-green-400 mb-1" />
          </div>
        </Card>
        <Card>
          <p className="text-xs font-medium text-gray-500 uppercase tracking-wide">Net Cash Flow</p>
          <p className={`text-2xl font-bold mt-1 ${(insights?.netFlow ?? 0) >= 0 ? 'text-green-600' : 'text-red-600'}`}>
            {(insights?.netFlow ?? 0) >= 0 ? '+' : ''}{fmt(insights?.netFlow ?? 0)}
          </p>
        </Card>
      </div>

      {/* AI recommendation */}
      {insights?.aiInsights?.map((insight, i) => (
        <Card key={i} className="bg-gradient-to-r from-brand-50 to-green-50 border-brand-100">
          <div className="flex gap-3">
            <div className="h-8 w-8 rounded-full bg-brand-500 flex items-center justify-center shrink-0">
              <Sparkles className="h-4 w-4 text-white" />
            </div>
            <div>
              <p className="text-sm font-semibold text-brand-800 mb-1">FinSight AI</p>
              <p className="text-sm text-gray-700 leading-relaxed">{insight}</p>
            </div>
          </div>
        </Card>
      ))}

      {/* Anomaly warning */}
      {(insights?.anomalyCount ?? 0) > 0 && (
        <Card className="border-amber-200 bg-amber-50">
          <div className="flex gap-3">
            <AlertTriangle className="h-5 w-5 text-amber-500 shrink-0 mt-0.5" />
            <div>
              <p className="text-sm font-semibold text-amber-800">
                {insights!.anomalyCount} spending anomal{insights!.anomalyCount === 1 ? 'y' : 'ies'} detected
              </p>
              <p className="text-sm text-amber-700 mt-0.5">
                Review your transactions to verify these unusual amounts.
              </p>
            </div>
          </div>
        </Card>
      )}

      {/* Spending by category bar chart */}
      <Card>
        <CardHeader title="Spending by Category" subtitle={`${MONTHS[month - 1]} ${year}`} />
        {barData.length > 0 ? (
          <ResponsiveContainer width="100%" height={280}>
            <BarChart data={barData} margin={{ top: 0, right: 0, left: 0, bottom: 0 }}>
              <CartesianGrid strokeDasharray="3 3" stroke="#f0f0f0" />
              <XAxis dataKey="name" tick={{ fontSize: 12 }} />
              <YAxis tickFormatter={(v) => `$${v}`} tick={{ fontSize: 12 }} />
              <Tooltip formatter={(v: number) => fmt(v)} />
              <Bar dataKey="amount" fill="#10b981" radius={[4, 4, 0, 0]} />
            </BarChart>
          </ResponsiveContainer>
        ) : (
          <p className="text-sm text-gray-400 text-center py-12">No spending data for this period.</p>
        )}
      </Card>

      {/* Category breakdown table */}
      {insights && insights.categoryBreakdown.length > 0 && (
        <Card>
          <CardHeader title="Category Breakdown" />
          <div className="divide-y divide-gray-50">
            {insights.categoryBreakdown.map((c) => (
              <div key={c.category} className="flex items-center justify-between py-3">
                <div className="flex items-center gap-3">
                  <div className="w-28 text-sm font-medium text-gray-800">{c.category}</div>
                  <div className="w-40 h-1.5 bg-gray-100 rounded-full overflow-hidden">
                    <div className="h-full bg-brand-500 rounded-full" style={{ width: `${c.percentageOfTotal}%` }} />
                  </div>
                  <span className="text-xs text-gray-400">{c.percentageOfTotal}%</span>
                </div>
                <div className="text-right">
                  <p className="text-sm font-semibold text-gray-900">{fmt(c.amount)}</p>
                  <p className="text-xs text-gray-400">{c.transactionCount} transactions</p>
                </div>
              </div>
            ))}
          </div>
        </Card>
      )}
    </div>
  );
};
