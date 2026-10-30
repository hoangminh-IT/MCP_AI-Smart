import { api } from './api';
import type { Page, StockMovement } from './types';

export interface ReportPeriod { from: string; to: string; warehouseId?: number }
export interface TrendPoint { date: string; salesOrdersValue: number; fulfilledSalesValue: number; purchaseOrdersValue: number; receivedPurchaseCost: number }
export interface DashboardReport {
  inventory: { totalProducts: number; totalCategories: number; totalQuantity: number; inventoryCostValue: number; inventoryRetailValue: number; lowStockCount: number; outOfStockCount: number; stockMovementCount: number; asOf: string };
  totalSuppliers: number; totalCustomers: number; pendingPurchaseOrders: number; pendingSalesOrders: number;
  salesOrdersValue: number; fulfilledSalesValue: number; purchaseOrdersValue: number; receivedPurchaseCost: number;
  periodFrom: string; periodTo: string; trend: TrendPoint[];
}
export interface InventoryReportRow { productId: number; sku: string; productName: string; categoryName: string; quantity: number; minimumStock: number; costPrice: number; sellingPrice: number; costValuation: number; retailValuation: number }
export interface SalesReportRow { orderId: number; code: string; customerName: string; status: string; createdAt: string; orderedQuantity: number; fulfilledQuantity: number; orderValue: number; fulfilledValue: number; warehouseId: number; warehouseCode: string | null; warehouseName: string | null }
export interface PurchaseReportRow { orderId: number; code: string; supplierName: string; status: string; createdAt: string; orderedQuantity: number; receivedQuantity: number; orderedCost: number; receivedCost: number; warehouseId: number; warehouseCode: string | null; warehouseName: string | null }
export interface ProductPerformanceRow { productId: number; sku: string; productName: string; orderedQuantity: number; fulfilledQuantity: number; fulfilledSalesValue: number }
export interface StockMovementReportRow extends StockMovement { }

export async function getDashboard(period: ReportPeriod, signal?: AbortSignal) {
  return (await api.get<DashboardReport>('/reports/dashboard', { params: period, signal })).data;
}
export async function getInventoryReport(page: number, pageSize: number, search: string, stockState: string, signal?: AbortSignal, warehouseId?: number) {
  return (await api.get<Page<InventoryReportRow>>('/reports/inventory', { params: { warehouseId, page, pageSize, search: search || undefined, stockState }, signal })).data;
}
export async function getSalesReport(period: ReportPeriod, page: number, pageSize: number, signal?: AbortSignal) {
  return (await api.get<Page<SalesReportRow>>('/reports/sales', { params: { ...period, page, pageSize }, signal })).data;
}
export async function getPurchaseReport(period: ReportPeriod, page: number, pageSize: number, signal?: AbortSignal) {
  return (await api.get<Page<PurchaseReportRow>>('/reports/purchases', { params: { ...period, page, pageSize }, signal })).data;
}
export async function getMovementReport(period: ReportPeriod, page: number, pageSize: number, signal?: AbortSignal) {
  return (await api.get<Page<StockMovementReportRow>>('/reports/stock-movements', { params: { ...period, page, pageSize }, signal })).data;
}
export async function getProductPerformance(period: ReportPeriod, take: number, signal?: AbortSignal) {
  return (await api.get<ProductPerformanceRow[]>('/reports/product-performance', { params: { ...period, take }, signal })).data;
}
export type ReportExportKind = 'dashboard' | 'inventory' | 'sales' | 'purchases' | 'stock-movements' | 'product-performance';
export type ReportExportFormat = 'csv' | 'xlsx' | 'pdf';
export interface ReportExportFilters extends Partial<ReportPeriod> { search?: string; stockState?: string; categoryId?: number; productId?: number; type?: string; take?: number }
export async function exportReportFile(report: ReportExportKind, format: ReportExportFormat, filters: ReportExportFilters, signal?: AbortSignal) {
  return (await api.get<Blob>(`/reports/export/${report}`, { params: { ...filters, format }, responseType: 'blob', signal, timeout: 60_000 })).data;
}
