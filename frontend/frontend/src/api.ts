import axios from 'axios';
import type { InternalAxiosRequestConfig } from 'axios';
import { AccountChangedError, allowsSessionRetry, assertSessionGeneration, SessionCoordinator, StaleSessionError } from './authSessionCoordinator';
import type { Category, LoginResult, Page } from './types';

type SessionRequest = InternalAxiosRequestConfig & { sessionGeneration?: number; sessionRetried?: boolean };
export type SessionRefreshReason = 'bootstrap' | 'renew' | 'manual' | '401';
const coordinator = new SessionCoordinator<LoginResult>();
const authHttp = axios.create({ baseURL: '/api', timeout: 20_000, withCredentials: true });
export const api = axios.create({ baseURL: '/api', timeout: 20_000 });
export const apiSessionSnapshot = () => coordinator.snapshot();
export const setApiSession = (accessToken: string | null, userId?: number) => coordinator.replace(accessToken, userId);
export const isApiSessionCurrent = (generation: number) => coordinator.isCurrent(generation);

export async function withAuthSessionLock<T>(operation: () => Promise<T>, requireLock = false): Promise<T> {
  if (typeof navigator !== 'undefined' && navigator.locks) return navigator.locks.request('smart-inventory:auth-session', { mode: 'exclusive' }, operation);
  if (requireLock) throw new Error('Trình duyệt cần hỗ trợ Web Locks để gia hạn phiên an toàn. Vui lòng đăng nhập lại.');
  return operation();
}
function sessionEvent(name: string, detail: unknown) { window.dispatchEvent(new CustomEvent(name, { detail })); }
export async function refreshAccessSession(reason: SessionRefreshReason): Promise<LoginResult> {
  const generation = coordinator.snapshot().generation;
  sessionEvent('inventory:session-refresh-start', { generation });
  try {
    return await coordinator.refresh(async (expectedUserId) => (await authHttp.post<LoginResult>('/auth/refresh', expectedUserId === undefined ? {} : { expectedUserId }, { headers: { 'X-Inventory-Session': '1' } })).data,
      (operation) => withAuthSessionLock(operation, true), (result) => sessionEvent('inventory:session-refreshed', result));
  } catch (error) {
    if (coordinator.isCurrent(generation) && !(error instanceof StaleSessionError)) {
      const status = axios.isAxiosError(error) ? error.response?.status : undefined;
      if (status === 401 || status === 403 || status === 409 || error instanceof AccountChangedError)
        sessionEvent('inventory:session-invalid', { reason, accountChanged: status === 409 || error instanceof AccountChangedError });
      else sessionEvent('inventory:session-refresh-error', { message: error instanceof Error && !axios.isAxiosError(error) ? error.message : errorMessage(error) });
    }
    throw error;
  } finally { if (coordinator.isCurrent(generation)) sessionEvent('inventory:session-refresh-end', { generation }); }
}
export async function loginWithSession(username: string, password: string): Promise<LoginResult> {
  const generation = coordinator.replace(null);
  const result = await withAuthSessionLock(async () => {
    if (!coordinator.isCurrent(generation)) throw new StaleSessionError();
    return (await authHttp.post<LoginResult>('/auth/login', { username, password })).data;
  });
  coordinator.accept(result, generation); return result;
}
export async function logoutCookieSession(expectedUserId?: number, capturedAccessToken?: string | null): Promise<void> {
  await withAuthSessionLock(async () => {
    await authHttp.post('/auth/session/logout', expectedUserId === undefined ? {} : { expectedUserId }, { headers: { 'X-Inventory-Session': '1', ...(capturedAccessToken ? { Authorization: `Bearer ${capturedAccessToken}` } : {}) } });
  });
}
api.interceptors.request.use((config) => {
  const request = config as SessionRequest; const snapshot = coordinator.snapshot();
  // A queued retry must never be reassigned to an account which logged in later.
  if (request.sessionGeneration !== undefined) assertSessionGeneration(request.sessionGeneration, snapshot.generation);
  request.sessionGeneration = snapshot.generation;
  if (snapshot.accessToken) request.headers.Authorization = `Bearer ${snapshot.accessToken}`;
  else request.headers.delete('Authorization');
  return request;
}, (error: unknown) => { throw error; }, { synchronous: true });
api.interceptors.response.use((response) => {
  assertSessionGeneration((response.config as SessionRequest).sessionGeneration, coordinator.snapshot().generation);
  return response;
}, async (error: unknown) => {
  if (!axios.isAxiosError(error) || !error.config) return Promise.reject(error);
  const config = error.config as SessionRequest; const snapshot = coordinator.snapshot();
  assertSessionGeneration(config.sessionGeneration, snapshot.generation);
  const hasBearer = typeof config.headers.Authorization === 'string' && config.headers.Authorization.startsWith('Bearer ');
  if (allowsSessionRetry({ path: config.url, status: error.response?.status, retried: config.sessionRetried, requestGeneration: config.sessionGeneration, currentGeneration: snapshot.generation, hasBearer })) {
    config.sessionRetried = true;
    try {
      if (config.headers.Authorization === `Bearer ${snapshot.accessToken}` || !snapshot.accessToken) await refreshAccessSession('401');
      if (!coordinator.isCurrent(snapshot.generation)) throw new StaleSessionError();
      return await api.request(config);
    } catch (failure) { return Promise.reject(failure); }
  }
  if (error.response?.status === 401 && config.sessionRetried && coordinator.isCurrent(config.sessionGeneration ?? -1))
    sessionEvent('inventory:session-invalid', { reason: '401', accountChanged: false });
  return Promise.reject(error);
});

export function errorMessage(error: unknown): string {
  if (axios.isCancel(error)) return 'Yêu cầu đã được hủy.';
  if (axios.isAxiosError(error)) {
    if (error.code === 'ECONNABORTED' || error.code === 'ETIMEDOUT') return 'Máy chủ phản hồi quá thời gian chờ.';
    const problem = error.response?.data as { detail?: string; title?: string; errors?: Record<string, string[]> } | undefined;
    if (problem?.errors) return Object.values(problem.errors).flat().join(' ');
    if (problem?.detail) return problem.detail;
    if (error.response?.status === 403) return 'Bạn không có quyền thực hiện thao tác này.';
    if (error.response?.status === 401) return 'Thông tin đăng nhập không hợp lệ hoặc phiên đã hết hạn.';
    if (problem?.title) return problem.title;
    if (!error.response) return 'Không kết nối được máy chủ. Kiểm tra API và thử lại.';
  }
  return 'Không thể hoàn tất thao tác. Vui lòng thử lại.';
}

export async function allCategories(signal?: AbortSignal): Promise<Category[]> {
  const items: Category[] = [];
  for (let page = 1; ; page += 1) {
    const { data } = await api.get<Page<Category>>('/categories', { params: { page, pageSize: 100, sortBy: 'name' }, signal });
    items.push(...data.items);
    if (items.length >= data.totalCount || data.items.length === 0) return items;
  }
}

export const money = (value: number) => new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND', maximumFractionDigits: 2 }).format(value);
export const dateTime = (value: string) => new Intl.DateTimeFormat('vi-VN', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value));
