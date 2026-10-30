import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { api } from './api';
import type { Page } from './types';

export interface ApplicationSettings {
  companyName: string; contactEmail: string | null; phone: string | null; address: string | null;
  defaultMinStock: number; reportDefaultDays: number; revision: number; updatedAt: string;
}
export interface UserPreferences { defaultPageSize: 10 | 25 | 50; revision: number; updatedAt?: string }
export interface AiProviderStatus { configured: boolean; provider: string; model: string; reason?: string; requestTimeoutSeconds?: number }
export function useApplicationSettings(enabled = true) {
  return useQuery({ queryKey: ['application-settings'], enabled, queryFn: async ({ signal }) => (await api.get<ApplicationSettings>('/settings', { signal })).data });
}
export function useUserPreferences(enabled = true) {
  return useQuery({ queryKey: ['user-preferences'], enabled, queryFn: async ({ signal }) => (await api.get<UserPreferences>('/preferences/me', { signal })).data });
}
export function usePreferredPageSize(): [number, (value: number) => void] {
  const preferences = useUserPreferences();
  const [selection, setSelection] = useState<number | null>(null);
  return [selection ?? preferences.data?.defaultPageSize ?? 10, setSelection];
}
export async function saveApplicationSettings(input: Omit<ApplicationSettings, 'updatedAt'>): Promise<ApplicationSettings> {
  return (await api.put<ApplicationSettings>('/settings', input)).data;
}
export async function saveUserPreferences(input: Pick<UserPreferences, 'defaultPageSize' | 'revision'>): Promise<UserPreferences> {
  return (await api.put<UserPreferences>('/preferences/me', input)).data;
}
export async function getAiProviderStatus(signal?: AbortSignal): Promise<AiProviderStatus> {
  return (await api.get<AiProviderStatus>('/ai/status', { signal })).data;
}
export type AuditSource = 'audit' | 'mcp' | 'ai';
export interface AuditEntry { id: string; source: string; actorId?: number; actorUsername?: string; action: string; entityName: string; targetId?: string; status: string; createdAt: string }
export interface AuditQuery { actorId?: number; action?: string; entityName?: string; status?: string; source?: AuditSource; from?: string; to?: string; page: number; pageSize: number }
export async function listAuditEntries(query: AuditQuery, signal?: AbortSignal): Promise<Page<AuditEntry>> {
  return (await api.get<Page<AuditEntry>>('/audit-logs', { params: query, signal })).data;
}
