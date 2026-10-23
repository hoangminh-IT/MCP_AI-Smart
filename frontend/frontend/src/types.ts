export interface User { id: number; username: string; email: string; fullName: string; role: 'Admin' | 'Staff' }
export interface LoginResult { accessToken: string; expiresAt: string; user: User }
export interface Page<T> { items: T[]; totalCount: number; page: number; pageSize: number }
export interface Category { id: number; name: string; description: string | null; createdAt: string; productCount: number }
export interface Product {
  id: number; categoryId: number; categoryName: string; sku: string; name: string;
  description: string | null; price: number; costPrice: number; minStock: number;
  imageUrl: string | null; isActive: boolean; quantity: number; createdAt: string; updatedAt: string;
}
export interface ProductInput {
  categoryId: number; sku: string; name: string; description: string;
  price: number; costPrice: number; minStock: number; imageUrl: string;
}
export interface StockSummary {
  totalProducts: number; totalCategories: number; totalQuantity: number;
  inventoryCostValue: number; inventoryRetailValue: number; lowStockCount: number;
  outOfStockCount: number; stockMovementCount: number; asOf: string;
}
export interface StockMovement {
  id: number; productId: number; sku: string; productName: string; type: 'Receipt' | 'Issue' | 'Adjustment';
  quantity: number; createdAt: string; quantityBefore: number | null; quantityAfter: number | null;
  reason: string | null; createdBy: number | null; requestId: string | null;
  referenceType: string | null; referenceId: string | null;
}
export interface StockOperation {
  movementId: number; productId: number; type: string; quantity: number; quantityBefore: number;
  quantityAfter: number; reason: string; createdBy: number; createdAt: string; requestId: string; replayed: boolean;
}
