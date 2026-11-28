import { Card, CardHeader } from '../ui/Card';
import { useCorporationSummary } from '../../hooks/useReports';
import { Spinner } from '../ui/Spinner';
import { errorMessage } from '../../services/api';
import { fmt } from './format';

/** Hidden entirely when the user has no Corporation-owned account. */
export const CorporationCard = ({ hasCorporation }: { hasCorporation: boolean }) => {
  const { data, isLoading, isError, error } = useCorporationSummary(hasCorporation);
  if (!hasCorporation) return null;
  if (isLoading || isError || !data) {
    return (
      <Card>
        <CardHeader title="Corporation" />
        {isLoading ? <Spinner /> : (
          <p role="alert" className="text-sm text-red-600">{errorMessage(error, 'Could not load corporation summary.')}</p>
        )}
      </Card>
    );
  }

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
