import { api } from './api';
import type { Page } from './types';

export type NotificationType = 'LowStock' | 'OutOfStock' | 'PurchaseOrder' | 'SalesOrder';
export interface UserNotification {
  id: string; type: NotificationType; title: string; message: string; targetType: string | null;
  targetId: string | null; createdAt: string; readAt: string | null; isRead: boolean;
}
export interface NotificationQuery { isRead?: boolean; type?: NotificationType; page: number; pageSize: number }
export async function getNotificationSummary(signal?: AbortSignal) {
  return (await api.get<{ unreadCount: number }>('/notifications/summary', { signal })).data;
}
export async function listNotifications(query: NotificationQuery, signal?: AbortSignal): Promise<Page<UserNotification>> {
  return (await api.get<Page<UserNotification>>('/notifications', { params: query, signal })).data;
}
export async function markNotificationRead(id: string) {
  await api.post(`/notifications/${id}/read`);
}
export async function markAllNotificationsRead() {
  await api.post('/notifications/read-all');
}
