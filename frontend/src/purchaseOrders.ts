import { api } from './api';
import type { Page, Product, StockOperation } from './types';
import type { Supplier } from './suppliers';

export type PurchaseStatus = 'Draft' | 'Submitted' | 'Approved' | 'PartiallyReceived' | 'Received' | 'Cancelled';
export type PurchaseSort = 'createdAt' | 'updatedAt' | 'code' | 'supplierName' | 'status' | 'totalAmount';
export interface PurchaseOrderSummary {
  warehouseId: number; warehouseCode: string | null; warehouseName: string | null;
  id: number; code: string; supplierId: number; supplierName: string; status: PurchaseStatus;
  totalAmount: number; orderedQuantity: number; receivedQuantity: number; createdAt: string; updatedAt: string;
}
export interface PurchaseOrderItem {
  id: number; productId: number; sku: string; productName: string; quantity: number;
  unitCost: number; lineTotal: number; receivedQuantity: number;
}
export interface PurchaseReceipt {
  id: string; requestId: string; createdAt: string; createdBy: number; reason?: string;
  items: { purchaseOrderItemId: number; productId: number; quantity: number; movementId: number }[];
}
export interface PurchaseOrder extends PurchaseOrderSummary {
  notes: string | null; createdBy: number; items: PurchaseOrderItem[]; receipts: PurchaseReceipt[];
}
export interface PurchaseOrderInput {
  warehouseId?: number;
  supplierId: number; notes: string | null;
  // Exact decimal strings prevent browser floating-point conversion before .NET decimal parsing.
  items: { productId: number; quantity: number; unitCost: string }[];
}
export interface ReceiptInput {
  requestId: string; reason: string | null;
  items: { purchaseOrderItemId: number; quantity: number }[];
}
export interface ReceiptResult {
  receiptId: string; requestId: string; replayed: boolean; order: PurchaseOrder; movements: StockOperation[];
}
export interface PurchaseQuery {
  warehouseId?: number;
  search?: string; supplierId?: number; status?: PurchaseStatus; page: number; pageSize: number;
  sortBy: PurchaseSort; descending: boolean;
}
export async function listPurchaseOrders(query: PurchaseQuery, signal?: AbortSignal): Promise<Page<PurchaseOrderSummary>> {
  return (await api.get<Page<PurchaseOrderSummary>>('/purchase-orders', { params: query, signal })).data;
}
export async function getPurchaseOrder(id: number, signal?: AbortSignal): Promise<PurchaseOrder> {
  return (await api.get<PurchaseOrder>(`/purchase-orders/${id}`, { signal })).data;
}
export async function savePurchaseOrder(input: PurchaseOrderInput, id?: number): Promise<PurchaseOrder> {
  return id
    ? (await api.put<PurchaseOrder>(`/purchase-orders/${id}`, input)).data
    : (await api.post<PurchaseOrder>('/purchase-orders', input)).data;
}
export async function transitionPurchaseOrder(id: number, action: 'submit' | 'approve' | 'cancel'): Promise<PurchaseOrder> {
  return (await api.post<PurchaseOrder>(`/purchase-orders/${id}/${action}`)).data;
}
export async function receivePurchaseOrder(id: number, input: ReceiptInput): Promise<ReceiptResult> {
  return (await api.post<ReceiptResult>(`/purchase-orders/${id}/receipts`, input)).data;
}
export async function purchaseSupplierOptions(search: string, page: number, signal?: AbortSignal): Promise<Page<Supplier>> {
  return (await api.get<Page<Supplier>>('/suppliers', { params: { search, page, pageSize: 50, isActive: true, sortBy: 'name' }, signal })).data;
}
export async function purchaseProductOptions(search: string, page: number, signal?: AbortSignal): Promise<Page<Product>> {
  return (await api.get<Page<Product>>('/products', { params: { search, page, pageSize: 50, includeInactive: false, sortBy: 'name' }, signal })).data;
}

export const purchaseStatusLabels: Record<PurchaseStatus, string> = {
  Draft: 'Nháp', Submitted: 'Đã gửi duyệt', Approved: 'Đã phê duyệt',
  PartiallyReceived: 'Nhận một phần', Received: 'Đã nhận đủ', Cancelled: 'Đã hủy',
};

export function decimalCents(input: string): bigint | null {
  const normalized = input.trim();
  if (!/^(?:\d+|\d*\.\d{1,2})$/.test(normalized)) return null;
  const [whole = '0', fraction = ''] = normalized.split('.');
  return BigInt(whole || '0') * 100n + BigInt(fraction.padEnd(2, '0') || '0');
}
export function decimalString(cents: bigint): string {
  return `${cents / 100n}.${String(cents % 100n).padStart(2, '0')}`;
}
export function previewMoney(cents: bigint): string {
  const fraction = cents % 100n;
  return `${new Intl.NumberFormat('vi-VN').format(cents / 100n)}${fraction ? `,${String(fraction).padStart(2, '0')}` : ''} ₫`;
}
