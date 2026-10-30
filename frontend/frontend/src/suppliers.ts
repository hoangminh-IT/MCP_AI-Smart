import { api } from './api';
import type { Page } from './types';

export interface Supplier {
  id: number;
  code: string;
  name: string;
  contactPerson: string | null;
  email: string | null;
  phone: string | null;
  address: string | null;
  taxCode: string | null;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
}

export type SupplierInput = Omit<Supplier, 'id' | 'createdAt' | 'updatedAt'>;
export type SupplierSort = 'code' | 'name' | 'createdAt' | 'updatedAt';

export interface SupplierQuery {
  search?: string;
  isActive?: boolean;
  page: number;
  pageSize: number;
  sortBy: SupplierSort;
  descending: boolean;
}

export async function listSuppliers(query: SupplierQuery, signal?: AbortSignal): Promise<Page<Supplier>> {
  return (await api.get<Page<Supplier>>('/suppliers', { params: query, signal })).data;
}

export async function getSupplier(id: number, signal?: AbortSignal): Promise<Supplier> {
  return (await api.get<Supplier>(`/suppliers/${id}`, { signal })).data;
}

export async function createSupplier(input: SupplierInput): Promise<Supplier> {
  return (await api.post<Supplier>('/suppliers', input)).data;
}

export async function updateSupplier(id: number, input: SupplierInput): Promise<Supplier> {
  return (await api.put<Supplier>(`/suppliers/${id}`, input)).data;
}

export async function deactivateSupplier(id: number): Promise<void> {
  await api.delete(`/suppliers/${id}`);
}

export function supplierInput(supplier: Supplier): SupplierInput {
  return {
    code: supplier.code,
    name: supplier.name,
    contactPerson: supplier.contactPerson,
    email: supplier.email,
    phone: supplier.phone,
    address: supplier.address,
    taxCode: supplier.taxCode,
    isActive: supplier.isActive,
  };
}
