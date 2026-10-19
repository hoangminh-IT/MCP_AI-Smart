import axios from 'axios';

export const USE_MOCK = import.meta.env.VITE_USE_MOCK === 'true';

export const http = axios.create({ baseURL: import.meta.env.VITE_API_URL ?? '' });

// Đính kèm JWT (T056 sẽ lưu token sau khi đăng nhập).
http.interceptors.request.use((config) => {
  const token = localStorage.getItem('token');
  if (token) config.headers.Authorization = `Bearer ${token}`;
  return config;
});

export function errorMessage(e: unknown): string {
  if (axios.isAxiosError(e)) {
    if (!e.response) return 'Không kết nối được máy chủ. Kiểm tra backend đã chạy chưa.';
    const d = e.response.data;
    if (typeof d === 'string' && d) return d;
    if (d?.message) return String(d.message);
    if (d?.title) return String(d.title);
    if (d?.error) return String(d.error);
    return `Lỗi ${e.response.status}`;
  }
  return e instanceof Error ? e.message : 'Đã có lỗi xảy ra';
}
