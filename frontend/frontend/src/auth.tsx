import { createContext, useCallback, useContext, useEffect, useState } from 'react';
import type { ReactNode } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { api, errorMessage, setApiToken } from './api';
import type { LoginResult, User } from './types';

const STORAGE_KEY = 'smart-inventory-session';
type Session = Pick<LoginResult, 'accessToken' | 'expiresAt'>;
interface AuthState {
  user: User | null; authenticated: boolean; loading: boolean; loadError: string;
  notice: string; login: (username: string, password: string) => Promise<void>;
  logout: () => Promise<void>; clearSession: (message?: string) => void;
  updateUser: (user: User) => void; retry: () => void;
}
const AuthContext = createContext<AuthState | null>(null);

function readSession(): Session | null {
  try {
    const value = JSON.parse(sessionStorage.getItem(STORAGE_KEY) ?? 'null') as Session | null;
    if (value && typeof value.accessToken === 'string' && Date.parse(value.expiresAt) > Date.now()) return value;
    sessionStorage.removeItem(STORAGE_KEY);
  } catch { /* Storage can be disabled. The active login then stays in memory. */ }
  return null;
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient();
  const [session, setSession] = useState<Session | null>(() => {
    const value = readSession(); setApiToken(value?.accessToken ?? null); return value;
  });
  const [user, setUser] = useState<User | null>(null);
  const [loading, setLoading] = useState(Boolean(session));
  const [loadError, setLoadError] = useState('');
  const [notice, setNotice] = useState('');
  const [revision, setRevision] = useState(0);

  const clearSession = useCallback((message = '') => {
    setApiToken(null); setSession(null); setUser(null); setLoading(false); setLoadError(''); setNotice(message);
    queryClient.clear();
    try { sessionStorage.removeItem(STORAGE_KEY); } catch { /* Memory state is already cleared. */ }
  }, [queryClient]);

  useEffect(() => {
    const expired = () => clearSession('Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.');
    window.addEventListener('inventory:session-expired', expired);
    return () => window.removeEventListener('inventory:session-expired', expired);
  }, [clearSession]);

  useEffect(() => {
    if (!session) return;
    const remaining = Date.parse(session.expiresAt) - Date.now();
    if (remaining <= 0) { clearSession('Phiên đăng nhập đã hết hạn.'); return; }
    const timer = window.setTimeout(() => clearSession('Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.'), Math.min(remaining, 2_147_483_647));
    const controller = new AbortController();
    setLoading(true); setLoadError('');
    api.get<User>('/auth/me', { signal: controller.signal }).then(({ data }) => setUser(data)).catch((error: unknown) => {
      if (!controller.signal.aborted) setLoadError(errorMessage(error));
    }).finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => { window.clearTimeout(timer); controller.abort(); };
  }, [session, revision, clearSession]);

  async function login(username: string, password: string) {
    const { data } = await api.post<LoginResult>('/auth/login', { username, password });
    const next = { accessToken: data.accessToken, expiresAt: data.expiresAt };
    queryClient.clear(); setApiToken(next.accessToken); setUser(data.user); setSession(next); setNotice('');
    try { sessionStorage.setItem(STORAGE_KEY, JSON.stringify(next)); } catch { /* Retain only in-memory session. */ }
  }
  async function logout() {
    try { await api.post('/auth/logout'); clearSession('Bạn đã đăng xuất.'); }
    catch { clearSession('Đã thoát trên trình duyệt. Máy chủ chưa xác nhận thu hồi phiên.'); }
  }
  return <AuthContext.Provider value={{ user, authenticated: Boolean(session), loading, loadError, notice, login, logout, clearSession, updateUser: setUser, retry: () => setRevision((value) => value + 1) }}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const value = useContext(AuthContext);
  if (!value) throw new Error('AuthProvider is required.');
  return value;
}
