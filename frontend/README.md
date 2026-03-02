# FinSight Frontend

React 18 + TypeScript + Vite + TailwindCSS

## Stack
- **React 18** + TypeScript
- **React Router v6** — file-based routing
- **React Query** — server state, caching, refetch
- **Zustand** — auth store (token + user)
- **React Hook Form + Zod** — type-safe form validation
- **Recharts** — spending charts
- **Axios** — API client with automatic JWT attachment + 401 refresh
- **TailwindCSS** — utility-first styling

## Quick Start
```bash
cd frontend
npm install
npm run dev
# → http://localhost:3000
```

API is proxied to `http://localhost:8080` via Vite config.

## Pages
| Route | Page | Auth |
|-------|------|------|
| `/login` | LoginPage | Public |
| `/register` | RegisterPage | Public |
| `/` | DashboardPage | Protected |
| `/accounts` | AccountsPage | Protected |
| `/budgets` | BudgetsPage | Protected |
| `/insights` | InsightsPage | Protected |
