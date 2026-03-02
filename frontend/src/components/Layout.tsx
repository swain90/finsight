import { NavLink, Outlet, useNavigate } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';
import {
  LayoutDashboard, CreditCard, ArrowLeftRight,
  Target, Brain, LogOut, TrendingUp
} from 'lucide-react';

const navItems = [
  { to: '/dashboard', icon: LayoutDashboard, label: 'Overview' },
  { to: '/accounts', icon: CreditCard, label: 'Accounts' },
  { to: '/transactions', icon: ArrowLeftRight, label: 'Transactions' },
  { to: '/budgets', icon: Target, label: 'Budgets' },
  { to: '/insights', icon: Brain, label: 'AI Insights' },
];

export function Layout() {
  const { user, logout } = useAuth();
  const navigate = useNavigate();

  const handleLogout = () => {
    logout();
    navigate('/login');
  };

  return (
    <div className="app-shell">
      <aside className="sidebar">
        <div className="sidebar__brand">
          <TrendingUp size={22} className="sidebar__brand-icon" />
          <span className="sidebar__brand-name">FinSight</span>
        </div>

        <nav className="sidebar__nav">
          {navItems.map(({ to, icon: Icon, label }) => (
            <NavLink
              key={to}
              to={to}
              className={({ isActive }) =>
                `sidebar__link ${isActive ? 'sidebar__link--active' : ''}`
              }
            >
              <Icon size={18} />
              <span>{label}</span>
            </NavLink>
          ))}
        </nav>

        <div className="sidebar__footer">
          <div className="sidebar__user">
            <div className="sidebar__avatar">
              {user?.fullName.charAt(0).toUpperCase()}
            </div>
            <div className="sidebar__user-info">
              <span className="sidebar__user-name">{user?.fullName}</span>
              <span className="sidebar__user-email">{user?.email}</span>
            </div>
          </div>
          <button className="sidebar__logout" onClick={handleLogout} title="Log out">
            <LogOut size={16} />
          </button>
        </div>
      </aside>

      <main className="main-content">
        <Outlet />
      </main>
    </div>
  );
}
