import { api } from './api';
import type { Page } from './types';

export interface Customer {
  id: number; code: string; name: string; email: string | null; phone: string | null;
  address: string | null; isActive: boolean; createdAt: string; updatedAt: string;
}
export type CustomerInput = Omit<Customer, 'id' | 'createdAt' | 'updatedAt'>;
export type CustomerSort = 'name' | 'code' | 'email' | 'isActive' | 'createdAt' | 'updatedAt';
export interface CustomerQuery {
  search?: string; isActive?: boolean; page: number; pageSize: number; sortBy: CustomerSort; descending: boolean;
}
export async function listCustomers(query: CustomerQuery, signal?: AbortSignal): Promise<Page<Customer>> {
  return (await api.get<Page<Customer>>('/customers', { params: query, signal })).data;
}
export async function getCustomer(id: number, signal?: AbortSignal): Promise<Customer> {
  return (await api.get<Customer>(`/customers/${id}`, { signal })).data;
}
export async function createCustomer(input: CustomerInput): Promise<Customer> {
  return (await api.post<Customer>('/customers', input)).data;
}
export async function updateCustomer(id: number, input: CustomerInput): Promise<Customer> {
  return (await api.put<Customer>(`/customers/${id}`, input)).data;
}
export async function deactivateCustomer(id: number): Promise<void> {
  await api.delete(`/customers/${id}`);
}
export function customerInput(customer: Customer): CustomerInput {
  return { code: customer.code, name: customer.name, email: customer.email, phone: customer.phone, address: customer.address, isActive: customer.isActive };
}
