import { clsx } from 'clsx';

interface CardProps { children: React.ReactNode; className?: string; }

export const Card = ({ children, className }: CardProps) => (
  <div className={clsx('bg-white rounded-2xl shadow-sm border border-gray-100 p-6', className)}>
    {children}
  </div>
);

export const CardHeader = ({ title, subtitle }: { title: string; subtitle?: string }) => (
  <div className="mb-4">
    <h3 className="text-sm font-semibold text-gray-500 uppercase tracking-wide">{title}</h3>
    {subtitle && <p className="text-xs text-gray-400 mt-0.5">{subtitle}</p>}
  </div>
);
