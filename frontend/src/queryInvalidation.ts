import type { QueryClient } from '@tanstack/react-query';

export async function invalidateReportsAndNotifications(client: QueryClient): Promise<void> {
  const keys = ['notification-summary', 'notifications', 'dashboard-report', 'report-inventory',
    'report-sales', 'report-purchases', 'report-movements', 'report-performance'];
  await Promise.all(keys.map((key) => client.invalidateQueries({ queryKey: [key] })));
}
