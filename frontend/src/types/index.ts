// ─── Auth ──────────────────────────────────────────────────────────────────
export interface AuthResult {
  userId: string;
  email: string;
  fullName: string;
  accessToken: string;
  expiresAt: string;
}

// ─── Accounts ──────────────────────────────────────────────────────────────
export interface Account {
  id: string;
  name: string;
  type: 'Checking' | 'Savings' | 'CreditCard' | 'Investment' | 'Retirement' | 'Mortgage' | 'Loan'
    | 'Cash' | 'LineOfCredit' | 'Tfsa' | 'Rrsp' | 'Fhsa' | 'Crypto';
  balance: number;
  currency: string;
  institutionName?: string;
  isActive: boolean;
  lastSyncedAt?: string;
  createdAt: string;
  institution: 'Other' | 'Cibc' | 'Td' | 'Mbna' | 'Rbc' | 'Wealthsimple';
  owner: 'Personal' | 'Corporation';
  externalAccountRef: string | null;
  isLiability: boolean;
  isInvestment: boolean;
}

export interface AppSettings {
  corporateFiscalYearEndMonth: number;
  corporateFiscalYearEndDay: number;
}

// ─── Transactions ───────────────────────────────────────────────────────────
export interface Transaction {
  id: string;
  description: string;
  amount: number;
  currency: string;
  type: 'Debit' | 'Credit';
  category: TransactionCategory;
  merchantName?: string;
  occurredAt: string;
  isAnomaly: boolean;
}

export type TransactionCategory =
  | 'Uncategorized' | 'Food' | 'Transportation' | 'Housing'
  | 'Utilities' | 'Healthcare' | 'Entertainment' | 'Shopping'
  | 'Income' | 'Transfer' | 'Subscription' | 'Education' | 'Travel' | 'Other';

// ─── Budgets ────────────────────────────────────────────────────────────────
export interface Budget {
  id: string;
  category: string;
  limitAmount: number;
  spentAmount: number;
  remainingAmount: number;
  currency: string;
  spendingPercentage: number;
  isOverBudget: boolean;
  month: number;
  year: number;
}

export interface BudgetSummary {
  month: number;
  year: number;
  totalBudgets: number;
  budgetsOnTrack: number;
  budgetsOverLimit: number;
  totalBudgeted: number;
  totalSpent: number;
  totalRemaining: number;
  currency: string;
  overallSpendingPercentage: number;
  alerts: OverBudgetAlert[];
}

export interface OverBudgetAlert {
  category: string;
  limitAmount: number;
  spentAmount: number;
  overageAmount: number;
  overagePercentage: number;
}

// ─── Insights ────────────────────────────────────────────────────────────────
export interface SpendingInsights {
  month: number;
  year: number;
  totalSpent: number;
  totalIncome: number;
  netFlow: number;
  currency: string;
  categoryBreakdown: CategoryBreakdown[];
  aiInsights: string[];
  anomalyCount: number;
}

export interface CategoryBreakdown {
  category: string;
  amount: number;
  transactionCount: number;
  percentageOfTotal: number;
}
