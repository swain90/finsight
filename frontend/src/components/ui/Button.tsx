import { clsx } from 'clsx';

interface ButtonProps extends React.ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: 'primary' | 'secondary' | 'ghost' | 'danger';
  size?: 'sm' | 'md' | 'lg';
  isLoading?: boolean;
}

const variants = {
  primary: 'bg-brand-500 hover:bg-brand-600 text-white shadow-sm',
  secondary: 'bg-white hover:bg-gray-50 text-gray-700 border border-gray-300 shadow-sm',
  ghost: 'hover:bg-gray-100 text-gray-600',
  danger: 'bg-red-600 hover:bg-red-700 text-white shadow-sm',
};

const sizes = {
  sm: 'px-3 py-1.5 text-sm',
  md: 'px-4 py-2 text-sm',
  lg: 'px-6 py-3 text-base',
};

export const Button = ({ variant = 'primary', size = 'md', isLoading, children, className, disabled, ...rest }: ButtonProps) => (
  <button
    className={clsx(
      'inline-flex items-center gap-2 rounded-lg font-medium transition-colors focus:outline-none focus:ring-2 focus:ring-brand-500 focus:ring-offset-2 disabled:opacity-50 disabled:cursor-not-allowed',
      variants[variant], sizes[size], className
    )}
    disabled={disabled || isLoading}
    {...rest}
  >
    {isLoading && <span className="h-4 w-4 animate-spin rounded-full border-2 border-current border-t-transparent" />}
    {children}
  </button>
);
