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
  kind: TransactionKind;
}

export type TransactionKind = 'Regular' | 'Transfer' | 'OwnerPay' | 'InvestmentTrade' | 'PendingTransfer';

export interface TransferCandidate {
  transactionId: string;
  accountName: string;
  date: string;
  description: string;
  amount: number;
}

export interface NeedsAttentionItem {
  transactionId: string;
  accountId: string;
  accountName: string;
  date: string;
  description: string;
  amount: number;
  type: string;
  candidates: TransferCandidate[];
}

export interface NetWorthPoint { monthEnd: string; personal: number; includingCorporation: number }
export interface NetWorth {
  personal: number;
  includingCorporation: number;
  currency: string;
  history: NetWorthPoint[];
}
export interface CorporationSummary {
  fiscalYearStart: string;
  fiscalYearEnd: string;
  revenue: number;
  paidToOwner: number;
  expenses: number;
  closingBalance: number;
}
export interface PersonalIncome {
  year: number;
  payFromCorporation: number;
  otherTaxableIncome: number;
  taxShelteredInvestmentIncome: number;
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

// ─── Statement import ──────────────────────────────────────────────────────
export interface ImportAccount {
  accountId: string;
  name: string;
  type: string;
  isLiability: boolean;
  isInvestment: boolean;
  previousBalance: number | null;
  previousBalanceAsOf: string | null;
}

export interface ImportRow {
  id: string;
  lineNumber: number;
  accountId: string | null;
  date: string;
  description: string;
  amount: number;
  type: string; // 'Debit' | 'Credit'
  category: string; // TransactionCategory name
  kindHint: string;
  isDuplicate: boolean;
  isExcluded: boolean;
}

export interface ImportBatch {
  id: string;
  institution: string;
  fileName: string;
  status: string; // 'PendingReview' | 'Committed' | 'Discarded'
  periodStart: string | null;
  periodEnd: string | null;
  reconciliationWarning: string | null;
  accounts: ImportAccount[];
  rows: ImportRow[];
}

export interface ImportBatchSummary {
  id: string;
  institution: string;
  fileName: string;
  status: string;
  periodStart: string | null;
  periodEnd: string | null;
  /** All parsed rows in the file. */
  rowCount: number;
  /** Rows not excluded; for a committed batch, the transactions it imported. */
  includedRowCount: number;
  createdAt: string;
  committedAt: string | null;
}

export interface ReconciliationResult {
  skipped: boolean;
  skipReason: string | null;
  expected: number | null;
  difference: number | null;
  isBalanced: boolean;
}

export interface ImportRowPatch {
  category?: number;
  isExcluded?: boolean;
  accountId?: string;
}

export interface ClosingBalanceInput {
  accountId: string;
  closingBalance: number;
  asOf: string;
}
