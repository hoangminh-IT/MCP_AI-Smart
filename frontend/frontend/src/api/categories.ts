import { http, USE_MOCK } from './client';
import { delay, mockDb } from './mockData';
import type { Category } from '../types';

export async function listCategories(): Promise<Category[]> {
  if (USE_MOCK) {
    await delay(100);
    return [...mockDb.categories];
  }
  const { data } = await http.get('/api/categories');
  return Array.isArray(data) ? data : data.items ?? [];
}
