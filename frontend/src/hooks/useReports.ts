import { useQuery } from 'react-query';
import { reportsApi } from '../services/api';
import type { CorporationSummary, NetWorth, PersonalIncome } from '../types';

export const useNetWorth = (months = 12) =>
  useQuery<NetWorth>(['reports', 'net-worth', months], () => reportsApi.netWorth(months));

export const useCorporationSummary = (enabled: boolean) =>
  useQuery<CorporationSummary>(['reports', 'corporation'], () => reportsApi.corporation(), { enabled });

export const usePersonalIncome = () =>
  useQuery<PersonalIncome>(['reports', 'personal-income'], () => reportsApi.personalIncome());
