import { clsx } from 'clsx';

type Variant = 'success' | 'warning' | 'danger' | 'neutral';

const variants: Record<Variant, string> = {
  success: 'bg-green-50 text-green-700 ring-green-600/20',
  warning: 'bg-amber-50 text-amber-700 ring-amber-600/20',
  danger: 'bg-red-50 text-red-700 ring-red-600/20',
  neutral: 'bg-gray-50 text-gray-700 ring-gray-600/20',
};

export const Badge = ({ label, variant = 'neutral' }: { label: string; variant?: Variant }) => (
  <span className={clsx('inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium ring-1 ring-inset', variants[variant])}>
    {label}
  </span>
);
