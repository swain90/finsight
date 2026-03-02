import React, { createContext, useContext, useState, useEffect, useCallback } from 'react';
import { authApi } from '../services/api';

interface AuthUser {
  userId: string;
  email: string;
  fullName: string;
}

interface AuthContextValue {
  user: AuthUser | null;
  isLoading: boolean;
  login: (email: string, password: string) => Promise<void>;
  register: (email: string, password: string, firstName: string, lastName: string) => Promise<void>;
  logout: () => void;
}

const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [user, setUser] = useState<AuthUser | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    const token = localStorage.getItem('finsight_token');
    const userStr = localStorage.getItem('finsight_user');
    if (token && userStr) {
      try {
        setUser(JSON.parse(userStr));
      } catch {
        localStorage.clear();
      }
    }
    setIsLoading(false);
  }, []);

  const login = useCallback(async (email: string, password: string) => {
    const res = await authApi.login(email, password);
    localStorage.setItem('finsight_token', res.accessToken);
    const u = { userId: res.userId, email: res.email, fullName: res.fullName };
    localStorage.setItem('finsight_user', JSON.stringify(u));
    setUser(u);
  }, []);

  const register = useCallback(async (email: string, password: string, firstName: string, lastName: string) => {
    const res = await authApi.register(email, password, firstName, lastName);
    localStorage.setItem('finsight_token', res.accessToken);
    const u = { userId: res.userId, email: res.email, fullName: res.fullName };
    localStorage.setItem('finsight_user', JSON.stringify(u));
    setUser(u);
  }, []);

  const logout = useCallback(() => {
    localStorage.removeItem('finsight_token');
    localStorage.removeItem('finsight_user');
    setUser(null);
  }, []);

  return (
    <AuthContext.Provider value={{ user, isLoading, login, register, logout }}>
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth must be used within AuthProvider');
  return ctx;
}
