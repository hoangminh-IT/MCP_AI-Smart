import { api } from './api';
import type { Page, User } from './types';

export interface ManagedUser extends User { isActive: boolean; createdAt: string; updatedAt: string }
export type UserRole = User['role'];
export type UserSort = 'username' | 'fullName' | 'email' | 'role' | 'isActive' | 'createdAt' | 'updatedAt';
export interface UserQuery { search?: string; role?: UserRole; isActive?: boolean; page: number; pageSize: number; sortBy: UserSort; descending: boolean }
export interface UserInput { email: string; fullName: string; role: UserRole; isActive: boolean }
export interface CreateUserInput extends UserInput { username: string; password: string }
export interface RegisterInput { username: string; email: string; fullName: string; password: string; confirmPassword: string }

/** Public signup always creates a Customer; account roles are assigned by an administrator. */
export async function registerAccount(input: RegisterInput): Promise<User> {
  return (await api.post<User>('/auth/register', input)).data;
}

export async function listUsers(query: UserQuery, signal?: AbortSignal): Promise<Page<ManagedUser>> {
  return (await api.get<Page<ManagedUser>>('/users', { params: query, signal })).data;
}
export async function getUser(id: number, signal?: AbortSignal): Promise<ManagedUser> {
  return (await api.get<ManagedUser>(`/users/${id}`, { signal })).data;
}
export async function createUser(input: CreateUserInput): Promise<ManagedUser> {
  return (await api.post<ManagedUser>('/users', input)).data;
}
export async function updateUser(id: number, input: UserInput): Promise<ManagedUser> {
  return (await api.put<ManagedUser>(`/users/${id}`, input)).data;
}
export async function resetUserPassword(id: number, newPassword: string): Promise<void> {
  await api.post(`/users/${id}/reset-password`, { newPassword });
}
export function userInput(user: ManagedUser): UserInput {
  return { email: user.email, fullName: user.fullName, role: user.role, isActive: user.isActive };
}
