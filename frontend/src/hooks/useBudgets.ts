import { useQuery, useMutation, useQueryClient } from 'react-query';
import { api } from '../services/api';
import type { Budget, BudgetSummary } from '../types';

const now = new Date();

export const useBudgets = (month = now.getMonth() + 1, year = now.getFullYear()) =>
  useQuery<Budget[]>(['budgets', month, year], async () => {
    const { data } = await api.get(`/budgets/${year}/${month}`);
    return data;
  });

export const useBudgetSummary = (month = now.getMonth() + 1, year = now.getFullYear()) =>
  useQuery<BudgetSummary>(['budget-summary', month, year], async () => {
    const { data } = await api.get(`/budgets/${year}/${month}/summary`);
    return data;
  });

export const useCreateBudget = () => {
  const qc = useQueryClient();
  return useMutation(
    async (body: { category: number; limitAmount: number; currency: string; month: number; year: number }) => {
      const { data } = await api.post('/budgets', body);
      return data;
    },
    { onSuccess: () => { qc.invalidateQueries('budgets'); qc.invalidateQueries('budget-summary'); } }
  );
};
