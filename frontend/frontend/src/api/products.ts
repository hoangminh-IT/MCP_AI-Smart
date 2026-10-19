import { http, USE_MOCK } from './client';
import { delay, mockDb } from './mockData';
import type { PagedResult, Product, ProductInput } from '../types';

export interface ListParams {
  search?: string;
  page?: number; // 0-based
  pageSize?: number;
}

const norm = (s: string) => s.trim().toLowerCase();
const matches = (p: Product, q: string) =>
  !q || norm(p.name).includes(q) || norm(p.sku).includes(q);

export async function listProducts({
  search = '',
  page = 0,
  pageSize = 10,
}: ListParams = {}): Promise<PagedResult<Product>> {
  const q = norm(search);

  if (USE_MOCK) {
    await delay();
    const all = mockDb.products.filter((p) => p.isActive !== false && matches(p, q));
    return { items: all.slice(page * pageSize, (page + 1) * pageSize), total: all.length, page, pageSize };
  }

  // GIẢ ĐỊNH hợp đồng (T006): query ?search=&page=(từ 1)&pageSize=
  // Trả về { items, total } hoặc mảng thuần. Sửa tại đây nếu TV1/TV2 chốt khác.
  const { data } = await http.get('/api/products', {
    params: { search: search || undefined, page: page + 1, pageSize },
  });
  if (Array.isArray(data)) {
    const all = data.filter((p: Product) => p.isActive !== false && matches(p, q));
    return { items: all.slice(page * pageSize, (page + 1) * pageSize), total: all.length, page, pageSize };
  }
  return { items: data.items ?? [], total: data.total ?? data.totalCount ?? 0, page, pageSize };
}

export async function createProduct(input: ProductInput): Promise<Product> {
  if (USE_MOCK) {
    await delay();
    if (mockDb.products.some((p) => norm(p.sku) === norm(input.sku))) throw new Error('SKU đã tồn tại');
    const p: Product = { ...input, id: Math.max(0, ...mockDb.products.map((x) => x.id)) + 1 };
    mockDb.products.push(p);
    mockDb.inventory.push({ productId: p.id, quantity: 0 });
    return p;
  }
  const { data } = await http.post('/api/products', input);
  return data;
}

export async function updateProduct(id: number, input: ProductInput): Promise<void> {
  if (USE_MOCK) {
    await delay();
    if (mockDb.products.some((p) => p.id !== id && norm(p.sku) === norm(input.sku)))
      throw new Error('SKU đã tồn tại');
    const i = mockDb.products.findIndex((p) => p.id === id);
    if (i < 0) throw new Error('Không tìm thấy sản phẩm');
    mockDb.products[i] = { ...mockDb.products[i], ...input };
    return;
  }
  await http.put(`/api/products/${id}`, input);
}

// Ẩn mềm (T014): DELETE /api/products/{id}
export async function hideProduct(id: number): Promise<void> {
  if (USE_MOCK) {
    await delay();
    const p = mockDb.products.find((x) => x.id === id);
    if (!p) throw new Error('Không tìm thấy sản phẩm');
    p.isActive = false;
    return;
  }
  await http.delete(`/api/products/${id}`);
}
