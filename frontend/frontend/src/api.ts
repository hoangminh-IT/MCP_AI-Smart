import axios from 'axios';
import type { Category, Page } from './types';

let accessToken: string | null = null;
export function setApiToken(value: string | null) { accessToken = value; }
export const api = axios.create({ baseURL: '/api', timeout: 20_000 });
api.interceptors.request.use((config) => {
  if (accessToken) config.headers.Authorization = `Bearer ${accessToken}`;
  return config;
});
api.interceptors.response.use((response) => response, (error: unknown) => {
  if (axios.isAxiosError(error) && error.response?.status === 401 &&
      error.config?.url !== '/auth/login' && accessToken &&
      error.config?.headers.Authorization === `Bearer ${accessToken}`) {
    window.dispatchEvent(new Event('inventory:session-expired'));
  }
  return Promise.reject(error);
});

export function errorMessage(error: unknown): string {
  if (axios.isCancel(error)) return 'Yêu cầu đã được hủy.';
  if (axios.isAxiosError(error)) {
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
