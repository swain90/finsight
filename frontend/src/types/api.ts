export interface AuthResponse {
  userId: string;
  email: string;
  fullName: string;
  accessToken: string;
  expiresAt: string;
}

export interface Account {
  id: string;
  name: string;
  type: string;
  balance: number;
  currency: string;
  institutionName: string | null;
  isActive: boolean;
  lastSyncedAt: string | null;
  createdAt: string;
  institution: string;
  owner: string;
  externalAccountRef: string | null;
  isLiability: boolean;
  isInvestment: boolean;
}

export interface Transaction {
  id: string;
  description: string;
  amount: number;
  currency: string;
  type: 'Debit' | 'Credit';
  category: string;
  merchantName: string | null;
  occurredAt: string;
  isAnomaly: boolean;
}

export interface Budget {
  id: string;
  category: string;
  limitAmount: number;
  spentAmount: number;
  remainingAmount: number;
  spendingPercentage: number;
  isOverBudget: boolean;
  currency: string;
  month: number;
  year: number;
}

export interface BudgetSummary {
  month: number;
  year: number;
  currency: string;
  totalBudgeted: number;
  totalSpent: number;
  totalRemaining: number;
  overBudgetCount: number;
  budgets: Budget[];
}

export interface SpendingInsights {
  month: number;
  year: number;
  totalSpent: number;
  totalIncome: number;
  netFlow: number;
  currency: string;
  categoryBreakdown: { category: string; amount: number; transactionCount: number; percentageOfTotal: number }[];
  aiInsights: string[];
  anomalyCount: number;
}
