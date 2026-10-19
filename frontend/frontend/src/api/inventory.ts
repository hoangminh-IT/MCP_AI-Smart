import { http, USE_MOCK } from './client';
import { delay, mockDb } from './mockData';
import type { InventoryItem } from '../types';

// GET /api/inventory -> [{ productId, quantity, ... }]
export async function listInventory(): Promise<InventoryItem[]> {
  if (USE_MOCK) {
    await delay(150);
    return mockDb.inventory.map((i) => ({ ...i }));
  }
  const { data } = await http.get('/api/inventory');
  return Array.isArray(data) ? data : data.items ?? [];
}
