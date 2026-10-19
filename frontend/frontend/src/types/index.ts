// Khớp với entities backend (SmartInventory.Application/Entities).
export interface Category {
  id: number;
  name: string;
}

export interface Product {
  id: number;
  categoryId: number;
  sku: string;
  name: string;
  price: number;
  minStock: number;
  // Backend chưa có cột này (T014 sẽ thêm). undefined = đang hoạt động.
  isActive?: boolean;
}

export type ProductInput = Omit<Product, 'id' | 'isActive'>;

export interface InventoryItem {
  productId: number;
  quantity: number;
}

export interface PagedResult<T> {
  items: T[];
  total: number;
  page: number; // 0-based ở phía UI
  pageSize: number;
}
