import { LineChart, Line, XAxis, YAxis, Tooltip, Legend, ResponsiveContainer } from 'recharts';
import { Card } from '../ui/Card';
import { Spinner } from '../ui/Spinner';
import { useNetWorth } from '../../hooks/useReports';
import { fmt } from './format';

const monthLabel = (d: string) =>
  new Date(`${d}T00:00:00`).toLocaleDateString('en-CA', { month: 'short' });

export const NetWorthCard = () => {
  const { data, isLoading, isError } = useNetWorth(12);

  return (
    <Card className="col-span-2">
      <p className="text-xs font-medium text-gray-500 uppercase tracking-wide">Net Worth</p>
      {isLoading ? (
        <div className="flex justify-center py-8"><Spinner /></div>
      ) : isError || !data ? (
        <p className="text-sm text-red-600 mt-2">Could not load net worth.</p>
      ) : (
        <>
          <div className="flex items-end gap-6 mt-1">
            <div>
              <p className="text-3xl font-bold text-gray-900">{fmt(data.personal, data.currency)}</p>
              <p className="text-xs text-gray-400">Personal</p>
            </div>
            <div>
              <p className="text-lg font-semibold text-gray-600">{fmt(data.includingCorporation, data.currency)}</p>
              <p className="text-xs text-gray-400">Including corporation</p>
            </div>
          </div>
          <div className="mt-3">
            <ResponsiveContainer width="100%" height={140}>
              <LineChart data={data.history.map((p) => ({ ...p, label: monthLabel(p.monthEnd) }))}>
                <XAxis dataKey="label" tick={{ fontSize: 11 }} />
                <YAxis hide domain={['auto', 'auto']} />
                <Tooltip formatter={(v: number) => fmt(v, data.currency)} />
                <Legend wrapperStyle={{ fontSize: 11 }} />
                <Line type="monotone" dataKey="personal" name="Personal" stroke="#10b981" strokeWidth={2} dot={false} />
                <Line type="monotone" dataKey="includingCorporation" name="Including corporation"
                  stroke="#6b7280" strokeWidth={2} dot={false} />
              </LineChart>
            </ResponsiveContainer>
          </div>
        </>
      )}
    </Card>
  );
};
