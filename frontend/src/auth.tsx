import { useCallback, useContext, createContext, useEffect, useRef, useState } from 'react';
import type { ReactNode } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import axios from 'axios';
import { api, apiSessionSnapshot, errorMessage, isApiSessionCurrent, loginWithSession, logoutCookieSession, refreshAccessSession, setApiSession } from './api';
import { StaleSessionError } from './authSessionCoordinator';
import type { LoginResult, User } from './types';

const STORAGE_KEY = 'smart-inventory-session'; const BLOCKED_KEY = 'smart-inventory-session-blocked'; const TAB_EVENT_KEY = 'smart-inventory-auth-event';
type Session = Pick<LoginResult, 'accessToken' | 'expiresAt'> & { userId?: number };
type TabEvent = { sender: string; type: 'logout' | 'account-changed'; userId: number; timestamp: number };
interface AuthState {
  user: User | null; authenticated: boolean; loading: boolean; loadError: string; refreshError: string; refreshing: boolean;
  notice: string; login: (username: string, password: string) => Promise<void>; logout: () => Promise<void>;
  clearSession: (message?: string) => void; updateUser: (user: User) => void; retry: () => void; retryRefresh: () => void;
}
const AuthContext = createContext<AuthState | null>(null);
function storedUserId(token: string): number | undefined {
  try { const payload = JSON.parse(atob(token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/'))); const id = Number(payload.sub); return Number.isSafeInteger(id) && id > 0 ? id : undefined; }
  catch { return undefined; } // Only an expected-account hint; the server validates every credential.
}
function initialState(): { session: Session | null; blocked: boolean } {
  try {
    const blocked = sessionStorage.getItem(BLOCKED_KEY) === '1';
    const value = JSON.parse(sessionStorage.getItem(STORAGE_KEY) ?? 'null') as Session | null;
    if (value && typeof value.accessToken === 'string' && value.accessToken && Number.isFinite(Date.parse(value.expiresAt)))
      return { session: { ...value, userId: value.userId ?? storedUserId(value.accessToken) }, blocked: false };
    sessionStorage.removeItem(STORAGE_KEY); return { session: null, blocked };
  } catch { return { session: null, blocked: false }; }
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient(); const [initial] = useState(initialState);
  const [session, setSession] = useState<Session | null>(initial.session); const sessionRef = useRef(session); sessionRef.current = session;
  const initialized = useRef(false); if (!initialized.current) { setApiSession(initial.session?.accessToken ?? null, initial.session?.userId); initialized.current = true; }
  const [user, setUser] = useState<User | null>(null); const [loading, setLoading] = useState(!initial.blocked || Boolean(initial.session));
  const [loadError, setLoadError] = useState(''); const loadErrorRef = useRef(loadError); loadErrorRef.current = loadError;
  const [notice, setNotice] = useState(''); const [refreshError, setRefreshError] = useState(''); const [refreshing, setRefreshing] = useState(false); const refreshingRef = useRef(refreshing); refreshingRef.current = refreshing;
  const channel = useRef<BroadcastChannel | null>(null); const tabId = useRef(crypto.randomUUID());
  const publish = useCallback((type: TabEvent['type'], userId?: number) => {
    if (!userId) return;
    const event: TabEvent = { sender: tabId.current, type, userId, timestamp: Date.now() };
    if (channel.current) channel.current.postMessage(event);
    else try { localStorage.setItem(TAB_EVENT_KEY, JSON.stringify(event)); } catch { /* No secret or session is written to localStorage. */ }
  }, []);
  const clearLocal = useCallback((message = '', broadcast = true) => {
    const id = apiSessionSnapshot().userId; setApiSession(null); sessionRef.current = null;
    setSession(null); setUser(null); setLoading(false); setLoadError(''); setRefreshError(''); setRefreshing(false); setNotice(message); queryClient.clear();
    try { sessionStorage.removeItem(STORAGE_KEY); sessionStorage.setItem(BLOCKED_KEY, '1'); } catch { /* Memory is already cleared. */ }
    if (broadcast) publish('logout', id);
  }, [publish, queryClient]);
  const clearSession = useCallback((message = '') => clearLocal(message), [clearLocal]);
  const applySession = useCallback((result: LoginResult) => {
    const next: Session = { accessToken: result.accessToken, expiresAt: result.expiresAt, userId: result.user.id }; sessionRef.current = next;
    refreshingRef.current = false; setRefreshing(false);
    setSession(next); setUser(result.user); setLoading(false); setLoadError(''); setRefreshError(''); setNotice('');
    try { sessionStorage.setItem(STORAGE_KEY, JSON.stringify(next)); sessionStorage.removeItem(BLOCKED_KEY); } catch { /* Keep access only in memory if storage is disabled. */ }
  }, []);

  useEffect(() => {
    function receive(value: unknown) {
      if (!value || typeof value !== 'object') return;
      const event = value as Partial<TabEvent>; if (event.sender === tabId.current || !Number.isSafeInteger(event.userId) || !event.userId) return;
      const current = apiSessionSnapshot().userId;
      if (event.type === 'logout' && (current === undefined || current === event.userId)) clearLocal('Tài khoản đã đăng xuất hoặc bị thu hồi phiên ở tab khác.', false);
      if (event.type === 'account-changed' && current !== undefined && current !== event.userId) clearLocal('Tài khoản đăng nhập đã thay đổi ở tab khác. Vui lòng đăng nhập lại.', false);
    }
    if (typeof BroadcastChannel !== 'undefined') { channel.current = new BroadcastChannel('smart-inventory:auth'); channel.current.onmessage = (event) => receive(event.data); }
    const storage = (event: StorageEvent) => { if (event.key === TAB_EVENT_KEY && event.newValue) { try { receive(JSON.parse(event.newValue)); } catch { /* Ignore malformed unrelated storage. */ } } };
    window.addEventListener('storage', storage);
    return () => { channel.current?.close(); channel.current = null; window.removeEventListener('storage', storage); };
  }, [clearLocal]);
  useEffect(() => {
    const refreshed = (event: Event) => applySession((event as CustomEvent<LoginResult>).detail);
    const invalid = (event: Event) => {
      const detail = (event as CustomEvent<{ reason: string; accountChanged: boolean }>).detail;
      clearLocal(detail.accountChanged ? 'Tài khoản của cookie đã thay đổi. Vui lòng đăng nhập lại tài khoản mong muốn.' : detail.reason === 'bootstrap' && !sessionRef.current ? '' : 'Phiên đăng nhập không còn hợp lệ. Vui lòng đăng nhập lại.', !detail.accountChanged);
    };
    const failed = (event: Event) => {
      const message = (event as CustomEvent<{ message: string }>).detail.message;
      if (sessionRef.current && Date.parse(sessionRef.current.expiresAt) > Date.now()) setRefreshError(`Chưa gia hạn được phiên: ${message}`);
      else { setLoadError(`Chưa khôi phục được phiên: ${message}`); setLoading(false); }
    };
    const started = () => { setRefreshing(true); refreshingRef.current = true; };
    const ended = () => { setRefreshing(false); refreshingRef.current = false; };
    window.addEventListener('inventory:session-refreshed', refreshed); window.addEventListener('inventory:session-invalid', invalid); window.addEventListener('inventory:session-refresh-error', failed);
    window.addEventListener('inventory:session-refresh-start', started); window.addEventListener('inventory:session-refresh-end', ended);
    return () => { window.removeEventListener('inventory:session-refreshed', refreshed); window.removeEventListener('inventory:session-invalid', invalid); window.removeEventListener('inventory:session-refresh-error', failed); window.removeEventListener('inventory:session-refresh-start', started); window.removeEventListener('inventory:session-refresh-end', ended); };
  }, [applySession, clearLocal]);

  const recover = useCallback(async (forceRefresh = false, bootstrap = false) => {
    const generation = apiSessionSnapshot().generation; setLoading(true); setLoadError('');
    try {
      const current = sessionRef.current;
      if (forceRefresh || !current || Date.parse(current.expiresAt) <= Date.now()) { await refreshAccessSession(bootstrap ? 'bootstrap' : 'manual'); return; }
      const { data } = await api.get<User>('/auth/me');
      if (isApiSessionCurrent(generation)) { setUser(data); setLoading(false); }
    } catch (error) {
      if (!(error instanceof StaleSessionError) && isApiSessionCurrent(generation)) { setLoadError(errorMessage(error)); setLoading(false); }
    }
  }, []);
  useEffect(() => { if (!initial.blocked || initial.session) void recover(false, true); }, [initial, recover]);
  useEffect(() => {
    if (!session) return;
    const remaining = Date.parse(session.expiresAt) - Date.now(); if (remaining <= 0) return;
    const generation = apiSessionSnapshot().generation; const lead = Math.min(60_000, remaining * .2);
    const renewal = window.setTimeout(() => { if (isApiSessionCurrent(generation) && !loadErrorRef.current) void refreshAccessSession('renew').catch(() => {}); }, Math.min(remaining - lead, 2_147_483_647));
    const expiry = window.setTimeout(() => {
      if (isApiSessionCurrent(generation) && sessionRef.current && Date.parse(sessionRef.current.expiresAt) <= Date.now()) {
        if (refreshingRef.current) setLoading(true);
        else { setLoading(false); setLoadError('Phiên truy cập đã hết hạn. Thử gia hạn phiên hoặc đăng xuất để đăng nhập lại.'); }
      }
    }, Math.min(remaining, 2_147_483_647));
    return () => { window.clearTimeout(renewal); window.clearTimeout(expiry); };
  }, [session]);

  async function login(username: string, password: string) {
    setLoading(true); setLoadError(''); setRefreshError('');
    const operation = loginWithSession(username, password); const generation = apiSessionSnapshot().generation;
    try { const result = await operation; queryClient.clear(); applySession(result); publish('account-changed', result.user.id); }
    catch (error) { if (isApiSessionCurrent(generation)) setLoading(false); throw error; }
  }
  async function logout() {
    const snapshot = apiSessionSnapshot(); clearLocal('Bạn đã đăng xuất.'); const generation = apiSessionSnapshot().generation;
    try { await logoutCookieSession(snapshot.userId, snapshot.accessToken); }
    catch (error) {
      if (isApiSessionCurrent(generation)) setNotice(axios.isAxiosError(error) && error.response?.status === 409 ? 'Cookie thuộc tài khoản khác và được giữ nguyên. Phiên trên tab này đã kết thúc.' : 'Đã thoát trên trình duyệt. Máy chủ chưa xác nhận thu hồi phiên.');
    }
  }
  return <AuthContext.Provider value={{ user, authenticated: Boolean(session), loading, loadError, refreshError, refreshing, notice, login, logout, clearSession, updateUser: setUser, retry: () => void recover(), retryRefresh: () => { setRefreshError(''); if (!sessionRef.current || Date.parse(sessionRef.current.expiresAt) <= Date.now()) { setLoading(true); setLoadError(''); } void refreshAccessSession('manual').catch(() => {}); } }}>{children}</AuthContext.Provider>;
}
export function useAuth() { const value = useContext(AuthContext); if (!value) throw new Error('AuthProvider is required.'); return value; }
