import { NavLink } from 'react-router-dom';
import { LayoutDashboard, CreditCard, ArrowLeftRight, PiggyBank, Sparkles, LogOut } from 'lucide-react';
import { useAuthStore } from '../../store/authStore';
import { clsx } from 'clsx';

const navItems = [
  { to: '/', icon: LayoutDashboard, label: 'Dashboard' },
  { to: '/accounts', icon: CreditCard, label: 'Accounts' },
  { to: '/transactions', icon: ArrowLeftRight, label: 'Transactions' },
  { to: '/budgets', icon: PiggyBank, label: 'Budgets' },
  { to: '/insights', icon: Sparkles, label: 'AI Insights' },
];

export const Sidebar = () => {
  const { user, logout } = useAuthStore();

  return (
    <aside className="flex h-screen w-64 flex-col bg-gray-900 text-white">
      {/* Logo */}
      <div className="flex items-center gap-2 px-6 py-5 border-b border-gray-800">
        <div className="h-8 w-8 rounded-lg bg-brand-500 flex items-center justify-center text-white font-bold text-sm">F</div>
        <span className="font-semibold text-lg tracking-tight">FinSight</span>
      </div>

      {/* Nav */}
      <nav className="flex-1 overflow-y-auto px-3 py-4 space-y-0.5">
        {navItems.map(({ to, icon: Icon, label }) => (
          <NavLink
            key={to}
            to={to}
            end={to === '/'}
            className={({ isActive }) =>
              clsx(
                'flex items-center gap-3 px-3 py-2 rounded-lg text-sm font-medium transition-colors',
                isActive
                  ? 'bg-brand-600 text-white'
                  : 'text-gray-400 hover:text-white hover:bg-gray-800'
              )
            }
          >
            <Icon className="h-4 w-4 shrink-0" />
            {label}
          </NavLink>
        ))}
      </nav>

      {/* User */}
      <div className="border-t border-gray-800 px-4 py-4">
        <div className="flex items-center gap-3 mb-3">
          <div className="h-8 w-8 rounded-full bg-brand-500 flex items-center justify-center text-xs font-semibold">
            {user?.fullName?.charAt(0).toUpperCase()}
          </div>
          <div className="min-w-0">
            <p className="text-sm font-medium text-white truncate">{user?.fullName}</p>
            <p className="text-xs text-gray-400 truncate">{user?.email}</p>
          </div>
        </div>
        <button
          onClick={logout}
          className="flex w-full items-center gap-2 rounded-lg px-3 py-2 text-sm text-gray-400 hover:text-white hover:bg-gray-800 transition-colors"
        >
          <LogOut className="h-4 w-4" /> Sign out
        </button>
      </div>
    </aside>
  );
};
