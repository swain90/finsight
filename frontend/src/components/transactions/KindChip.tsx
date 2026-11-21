import type { TransactionKind } from '../../types';

const LABELS: Record<TransactionKind, string | null> = {
  Regular: null,
  Transfer: 'Transfer',
  OwnerPay: 'Owner pay',
  InvestmentTrade: 'Investment trade',
  PendingTransfer: 'Pending transfer',
};

export const KindChip = ({ kind }: { kind: TransactionKind }) => {
  const label = LABELS[kind];
  if (!label) return null;
  return (
    <span className={`cat-pill ${kind === 'PendingTransfer' ? 'bg-amber-50 text-amber-700' : ''}`} style={{ marginLeft: 6 }}>
      {label}
    </span>
  );
};
