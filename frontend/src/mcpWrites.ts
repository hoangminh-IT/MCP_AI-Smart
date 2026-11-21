import { useQuery } from '@tanstack/react-query';
import type { QueryClient } from '@tanstack/react-query';
import { api } from './api';
import type { McpExecution } from './mcp';
import { invalidateReportsAndNotifications } from './queryInvalidation';

export type McpWriteStatus = 'Pending' | 'Approved' | 'Succeeded' | 'Cancelled' | 'Expired';
export interface McpWriteIntent {
  id: string; toolName: string; arguments: Record<string, unknown>; status: McpWriteStatus;
  createdAt: string; expiresAt: string; sessionId?: string | null; result?: unknown; errorMessage?: string | null;
}
export interface McpWriteConfirmationResult { intent: McpWriteIntent; execution: McpExecution | null }
export function useMcpWriteIntents(sessionId?: string, enabled = true) {
  return useQuery({ queryKey: ['mcp-write-intents', sessionId ?? 'all'], enabled,
    queryFn: async ({ signal }) => (await api.get<McpWriteIntent[]>('/mcp/write-intents', { params: { sessionId }, signal })).data,
    staleTime: 0, refetchOnMount: 'always',
    refetchInterval: (query) => query.state.data?.some((intent) => intent.status === 'Pending' || intent.status === 'Approved') ? 10_000 : false,
  });
}
export async function getMcpWriteIntent(id: string, signal?: AbortSignal): Promise<McpWriteIntent> {
  return (await api.get<McpWriteIntent>(`/mcp/write-intents/${encodeURIComponent(id)}`, { signal })).data;
}
export async function prepareMcpWrite(toolName: string, arguments_: Record<string, unknown>, sessionId?: string): Promise<McpWriteIntent> {
  return (await api.post<McpWriteIntent>('/mcp/write-intents', { toolName, arguments: arguments_, ...(sessionId ? { sessionId } : {}) })).data;
}
export async function confirmMcpWrite(id: string): Promise<McpWriteConfirmationResult> {
  return (await api.post<McpWriteConfirmationResult>(`/mcp/write-intents/${encodeURIComponent(id)}/confirm`, {}, { timeout: 45_000 })).data;
}
export async function cancelMcpWrite(id: string): Promise<McpWriteIntent> {
  return (await api.post<McpWriteIntent>(`/mcp/write-intents/${encodeURIComponent(id)}/cancel`, {})).data;
}
export async function invalidateMcpWriteData(client: QueryClient): Promise<void> {
  const keys = ['mcp-write-intents', 'mcp-executions', 'mcp-status', 'inventory', 'inventory-summary', 'products', 'product',
    'stock-movements', 'inventory-action-stock', 'transfer-source-stock', 'stock-transfers', 'purchase-orders', 'purchase-order', 'audit-logs', 'ai-conversation', 'ai-sessions'];
  await Promise.all([...keys.map((key) => client.invalidateQueries({ queryKey: [key] })), invalidateReportsAndNotifications(client)]);
}
export const writeStatusLabels: Record<McpWriteStatus, string> = {
  Pending: 'Chờ xác nhận', Approved: 'Đã cho phép · chưa xác định hoàn tất', Succeeded: 'Đã thực thi', Cancelled: 'Đã hủy', Expired: 'Đã hết hạn',
};
