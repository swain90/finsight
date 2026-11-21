import axios, { AxiosError } from 'axios';
import type {
  AppSettings, AuthResult, ImportBatch, ImportBatchSummary, ImportRow, ImportRowPatch,
  ReconciliationResult, ClosingBalanceInput,
} from '../types';

export const api = axios.create({
  baseURL: '/api/v1',
  headers: { 'Content-Type': 'application/json' },
  withCredentials: true,
});

// Token getter injected after store init (avoids circular import)
let getToken: (() => string | null) = () => null;
let setToken: ((token: string) => void) = () => {};
let onUnauthorized: (() => void) = () => {};
let refreshPromise: Promise<string | null> | null = null;

type RetriableRequestConfig = {
  _retry?: boolean;
  headers?: Record<string, string>;
  url?: string;
  [key: string]: unknown;
};

export const configureApi = (
  tokenGetter: () => string | null,
  tokenSetter: (token: string) => void,
  onUnauth: () => void
) => {
  getToken = tokenGetter;
  setToken = tokenSetter;
  onUnauthorized = onUnauth;
};

api.interceptors.request.use((config) => {
  const token = getToken();
  if (token) config.headers.Authorization = `Bearer ${token}`;
  return config;
});

api.interceptors.response.use(
  (res) => res,
  async (err: AxiosError) => {
    const status = err.response?.status;
    const config = (err.config ?? {}) as RetriableRequestConfig;
    const url = config.url ?? '';
    const isAuthRoute = url.includes('/auth/login')
      || url.includes('/auth/register')
      || url.includes('/auth/refresh-token');

    if (status === 401 && !config._retry && !isAuthRoute) {
      config._retry = true;

      if (!refreshPromise) {
        refreshPromise = api.post<AuthResult>('/auth/refresh-token')
          .then(({ data }) => {
            setToken(data.accessToken);
            return data.accessToken;
          })
          .catch(() => null)
          .finally(() => { refreshPromise = null; });
      }

      const refreshedToken = await refreshPromise;
      if (refreshedToken) {
        config.headers = config.headers ?? {};
        config.headers.Authorization = `Bearer ${refreshedToken}`;
        return api.request(config);
      }
    }

    if (status === 401) onUnauthorized();
    return Promise.reject(err);
  }
);

export type ApiError = AxiosError;

type RegisterPayload = {
  email: string;
  password: string;
  firstName: string;
  lastName: string;
};

type RecordTransactionPayload = {
  accountId: string;
  description: string;
  amount: number;
  currency: string;
  type: number;
  occurredAt: string;
  category?: number;
  merchantName?: string;
};

export const authApi = {
  async login(email: string, password: string): Promise<AuthResult> {
    const { data } = await api.post<AuthResult>('/auth/login', { email, password });
    return data;
  },
  async register(email: string, password: string, firstName: string, lastName: string): Promise<AuthResult> {
    const payload: RegisterPayload = { email, password, firstName, lastName };
    const { data } = await api.post<AuthResult>('/auth/register', payload);
    return data;
  },
};

export type AccountPayload = {
  name: string;
  type: number;
  institution: number;
  owner: number;
  externalAccountRef: string | null;
};

export const accountsApi = {
  async update(id: string, payload: AccountPayload): Promise<void> {
    await api.put(`/accounts/${id}`, payload);
  },
};

export const settingsApi = {
  async get(): Promise<AppSettings> {
    const { data } = await api.get<AppSettings>('/settings');
    return data;
  },
  async update(payload: AppSettings): Promise<AppSettings> {
    const { data } = await api.put<AppSettings>('/settings', payload);
    return data;
  },
};

export const transactionsApi = {
  async record(payload: RecordTransactionPayload): Promise<string> {
    const { data } = await api.post<string>('/transactions', payload);
    return data;
  },
};

/** Readable message from a failed request: ProblemDetails detail, then title, then a fallback. */
export const errorMessage = (err: unknown, fallback = 'Something went wrong. Please try again.'): string => {
  const e = err as AxiosError<{ detail?: string; title?: string }>;
  return e?.response?.data?.detail || e?.response?.data?.title || fallback;
};

/** ProblemDetails title carries the error code (e.g. "Import.AlreadyImported"). */
export const errorCode = (err: unknown): string | undefined =>
  (err as AxiosError<{ title?: string }>)?.response?.data?.title;

export const importsApi = {
  async upload(file: File, accountId?: string): Promise<ImportBatch> {
    const form = new FormData();
    form.append('file', file);
    if (accountId) form.append('accountId', accountId);
    // Let the browser set the multipart boundary.
    const { data } = await api.post<ImportBatch>('/imports', form, {
      headers: { 'Content-Type': undefined as unknown as string },
    });
    return data;
  },
  async list(): Promise<ImportBatchSummary[]> {
    const { data } = await api.get<ImportBatchSummary[]>('/imports');
    return data;
  },
  async get(id: string): Promise<ImportBatch> {
    const { data } = await api.get<ImportBatch>(`/imports/${id}`);
    return data;
  },
  async updateRow(id: string, rowId: string, patch: ImportRowPatch): Promise<ImportRow> {
    const { data } = await api.patch<ImportRow>(`/imports/${id}/rows/${rowId}`, patch);
    return data;
  },
  async reconciliation(id: string, accountId: string, closingBalance: number): Promise<ReconciliationResult> {
    const { data } = await api.get<ReconciliationResult>(`/imports/${id}/reconciliation`, {
      params: { accountId, closingBalance },
    });
    return data;
  },
  async commit(id: string, balances: ClosingBalanceInput[]): Promise<ImportBatch> {
    const { data } = await api.post<ImportBatch>(`/imports/${id}/commit`, { balances });
    return data;
  },
  async remove(id: string): Promise<void> {
    await api.delete(`/imports/${id}`);
  },
};
