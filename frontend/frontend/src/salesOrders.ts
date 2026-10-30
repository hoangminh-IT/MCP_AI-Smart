import { api } from './api';
import type { Page, Product, StockOperation } from './types';
import type { Customer } from './customers';

export type SalesStatus = 'Draft' | 'Confirmed' | 'PartiallyFulfilled' | 'Fulfilled' | 'Cancelled';
export type SalesSort = 'createdAt' | 'updatedAt' | 'code' | 'customerName' | 'status' | 'totalAmount';
export interface SalesOrderSummary {
  warehouseId: number; warehouseCode: string | null; warehouseName: string | null;
  id: number; code: string; customerId: number; customerName: string; status: SalesStatus;
  totalAmount: number; orderedQuantity: number; fulfilledQuantity: number; createdAt: string; updatedAt: string;
}
export interface SalesOrderItem {
  id: number; productId: number; sku: string; productName: string; quantity: number;
  fulfilledQuantity: number; unitPrice: number; lineTotal: number;
}
export interface SalesFulfillment {
  id: string; requestId: string; createdAt: string; createdBy: number;
  items: { salesOrderItemId: number; productId: number; quantity: number; movementId: number }[];
}
export interface SalesOrder extends SalesOrderSummary {
  notes: string | null; createdBy: number; items: SalesOrderItem[]; fulfillments: SalesFulfillment[];
}
export interface SalesOrderInput {
  warehouseId?: number;
  customerId: number; notes: string | null; items: { productId: number; quantity: number; unitPrice: string }[];
}
export interface FulfillmentInput {
  requestId: string; reason: string | null; items: { salesOrderItemId: number; quantity: number }[];
}
export interface FulfillmentResult {
  fulfillmentId: string; requestId: string; replayed: boolean; order: SalesOrder; movements: StockOperation[];
}
export interface SalesQuery {
  warehouseId?: number;
  search?: string; customerId?: number; status?: SalesStatus; page: number; pageSize: number;
  sortBy: SalesSort; descending: boolean;
}
export async function listSalesOrders(query: SalesQuery, signal?: AbortSignal): Promise<Page<SalesOrderSummary>> {
  return (await api.get<Page<SalesOrderSummary>>('/sales-orders', { params: query, signal })).data;
}
export async function getSalesOrder(id: number, signal?: AbortSignal): Promise<SalesOrder> {
  return (await api.get<SalesOrder>(`/sales-orders/${id}`, { signal })).data;
}
export async function saveSalesOrder(input: SalesOrderInput, id?: number): Promise<SalesOrder> {
  return id ? (await api.put<SalesOrder>(`/sales-orders/${id}`, input)).data
    : (await api.post<SalesOrder>('/sales-orders', input)).data;
}
export async function transitionSalesOrder(id: number, action: 'confirm' | 'cancel'): Promise<SalesOrder> {
  return (await api.post<SalesOrder>(`/sales-orders/${id}/${action}`)).data;
}
export async function fulfillSalesOrder(id: number, input: FulfillmentInput): Promise<FulfillmentResult> {
  return (await api.post<FulfillmentResult>(`/sales-orders/${id}/fulfillments`, input)).data;
}
export async function salesCustomerOptions(search: string, page: number, signal?: AbortSignal): Promise<Page<Customer>> {
  return (await api.get<Page<Customer>>('/customers', { params: { search, page, pageSize: 50, isActive: true, sortBy: 'name' }, signal })).data;
}
export async function salesProductOptions(search: string, page: number, signal?: AbortSignal): Promise<Page<Product>> {
  return (await api.get<Page<Product>>('/products', { params: { search, page, pageSize: 50, includeInactive: false, sortBy: 'name' }, signal })).data;
}
export const salesStatusLabels: Record<SalesStatus, string> = {
  Draft: 'Nháp', Confirmed: 'Đã xác nhận', PartiallyFulfilled: 'Giao một phần',
  Fulfilled: 'Đã hoàn tất', Cancelled: 'Đã hủy',
};
