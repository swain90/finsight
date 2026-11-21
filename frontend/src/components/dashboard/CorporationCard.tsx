import { Card, CardHeader } from '../ui/Card';
import { useCorporationSummary } from '../../hooks/useReports';
import { fmt } from './format';

/** Hidden entirely when the user has no Corporation-owned account. */
export const CorporationCard = ({ hasCorporation }: { hasCorporation: boolean }) => {
  const { data } = useCorporationSummary(hasCorporation);
  if (!hasCorporation || !data) return null;

  const rows: [string, number][] = [
    ['Revenue', data.revenue],
    ['Paid to owner', data.paidToOwner],
    ['Expenses', data.expenses],
    ['Balance', data.closingBalance],
  ];
  return (
    <Card>
      <CardHeader title="Corporation" subtitle={`Fiscal year ending ${data.fiscalYearEnd}`} />
      <div className="space-y-1.5">
        {rows.map(([label, value]) => (
          <div key={label} className="flex items-center justify-between text-sm">
            <span className="text-gray-600">{label}</span>
            <span className="font-medium text-gray-900">{fmt(value)}</span>
          </div>
        ))}
      </div>
    </Card>
  );
};
