import { Link } from 'react-router-dom';
import { Card } from '../ui/Card';
import { Badge } from '../ui/Badge';
import { usePersonalIncome } from '../../hooks/useReports';
import { useNeedsAttention } from '../../hooks/useTransactions';
import { fmt } from './format';

export const PayFromCorporationCard = () => {
  const { data } = usePersonalIncome();
  const { data: pending } = useNeedsAttention();
  const count = pending?.length ?? 0;

  return (
    <Card>
      <p className="text-xs font-medium text-gray-500 uppercase tracking-wide">Pay from corporation (YTD)</p>
      <p className="text-2xl font-bold text-gray-900 mt-1">{fmt(data?.payFromCorporation ?? 0)}</p>
      {count > 0 && (
        <Link to="/transactions" className="inline-block mt-2">
          <Badge label={`${count} unresolved transfer${count === 1 ? '' : 's'}`} variant="warning" />
        </Link>
      )}
    </Card>
  );
};
