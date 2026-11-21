import { Link } from 'react-router-dom';
import { Card } from '../ui/Card';
import { Badge } from '../ui/Badge';
import { usePersonalIncome } from '../../hooks/useReports';
import { useNeedsAttention } from '../../hooks/useTransactions';
import { Spinner } from '../ui/Spinner';
import { errorMessage } from '../../services/api';
import { fmt } from './format';

export const PayFromCorporationCard = () => {
  const income = usePersonalIncome();
  const attention = useNeedsAttention();
  const pending = attention.data;
  const count = pending?.length ?? 0;

  return (
    <Card>
      <p className="text-xs font-medium text-gray-500 uppercase tracking-wide">Pay from corporation (YTD)</p>
      {income.isLoading ? (
        <div className="mt-2"><Spinner size="sm" /></div>
      ) : income.isError || !income.data ? (
        <p role="alert" className="text-sm text-red-600 mt-1">{errorMessage(income.error, 'Could not load pay from corporation.')}</p>
      ) : (
        <p className="text-2xl font-bold text-gray-900 mt-1">{fmt(income.data.payFromCorporation)}</p>
      )}
      {attention.isError && (
        <p role="alert" className="text-xs text-red-600 mt-2">{errorMessage(attention.error, 'Could not load unresolved transfers.')}</p>
      )}
      {count > 0 && (
        <Link to="/transactions" className="inline-block mt-2">
          <Badge label={`${count} unresolved transfer${count === 1 ? '' : 's'}`} variant="warning" />
        </Link>
      )}
    </Card>
  );
};
