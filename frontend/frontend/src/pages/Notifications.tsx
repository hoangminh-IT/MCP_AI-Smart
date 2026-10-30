import { useState } from 'react';
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Alert, Button, Chip, Divider, LinearProgress, MenuItem, Paper, Stack, Table, TableBody, TableCell, TableContainer, TableHead, TablePagination, TableRow, TextField, Typography } from '@mui/material';
import DoneAllOutlined from '@mui/icons-material/DoneAllOutlined';
import NotificationsNoneOutlined from '@mui/icons-material/NotificationsNoneOutlined';
import RefreshOutlined from '@mui/icons-material/RefreshOutlined';
import { dateTime, errorMessage } from '../api';
import { FilterBar, EmptyState, PageHeading, useToast } from '../shared';
import { getNotificationSummary, listNotifications, markAllNotificationsRead, markNotificationRead } from '../notifications';
import type { NotificationQuery, NotificationType } from '../notifications';

const types: NotificationType[] = ['LowStock', 'OutOfStock', 'PurchaseOrder', 'SalesOrder'];
const typeLabels: Record<NotificationType, string> = { LowStock: 'Tồn kho thấp', OutOfStock: 'Hết hàng', PurchaseOrder: 'Đơn mua', SalesOrder: 'Đơn bán' };
export default function Notifications() {
  const cache = useQueryClient(); const toast = useToast();
  const [isRead, setIsRead] = useState<'all' | 'unread' | 'read'>('all');
  const [type, setType] = useState<NotificationType | ''>(''); const [page, setPage] = useState(0); const [pageSize, setPageSize] = useState(20);
  const query: NotificationQuery = { isRead: isRead === 'all' ? undefined : isRead === 'read', type: type || undefined, page: page + 1, pageSize };
  const items = useQuery({ queryKey: ['notifications', query], queryFn: ({ signal }) => listNotifications(query, signal), placeholderData: keepPreviousData });
  const summary = useQuery({ queryKey: ['notification-summary'], queryFn: ({ signal }) => getNotificationSummary(signal), staleTime: 0, refetchOnMount: 'always', refetchInterval: 30_000 });
  async function refresh() { await Promise.all([cache.invalidateQueries({ queryKey: ['notifications'] }), cache.invalidateQueries({ queryKey: ['notification-summary'] })]); }
  const markRead = useMutation({ mutationFn: markNotificationRead, onSuccess: async () => { toast('Đã đánh dấu thông báo đã đọc.'); await refresh(); }, onError: (error) => toast(errorMessage(error), 'error') });
  const markAll = useMutation({ mutationFn: markAllNotificationsRead, onSuccess: async () => { toast('Đã đánh dấu tất cả thông báo đã đọc.'); setIsRead('unread'); setPage(0); await refresh(); }, onError: (error) => toast(errorMessage(error), 'error') });
  const mutationError = markRead.error ?? markAll.error;
  const pending = markRead.isPending || markAll.isPending;
  const unreadLabel = summary.isError ? 'Chưa tải được số chưa đọc' : summary.isLoading ? 'Đang tải số chưa đọc…' : `${summary.data?.unreadCount ?? 0} chưa đọc`;
  return <>
    <PageHeading eyebrow="TRUNG TÂM THÔNG BÁO" title="Thông báo" description="Cảnh báo phát sinh từ các giao dịch kho và đơn hàng đã được lưu." actions={<Stack direction="row" spacing={1}><Button variant="outlined" startIcon={<RefreshOutlined />} disabled={items.isFetching || summary.isFetching || pending} onClick={() => void refresh()}>Làm mới thông báo</Button><Button variant="contained" startIcon={<DoneAllOutlined />} disabled={pending || summary.isLoading || summary.isError || !summary.data?.unreadCount} onClick={() => markAll.mutate()}>{markAll.isPending ? 'Đang cập nhật…' : 'Đánh dấu tất cả đã đọc'}</Button></Stack>} />
    {summary.isFetching && <LinearProgress sx={{ mb: 2 }} aria-label="Đang cập nhật số thông báo chưa đọc" />}
    {summary.isError && <Alert severity="error" sx={{ mb: 2 }} action={<Button color="inherit" onClick={() => void summary.refetch()}>Tải lại số chưa đọc</Button>}>Không tải được số thông báo chưa đọc. {errorMessage(summary.error)}</Alert>}
    {mutationError && <Alert severity="error" sx={{ mb: 2 }} onClose={() => { markRead.reset(); markAll.reset(); }}>{errorMessage(mutationError)}</Alert>}
    <FilterBar sx={{ border: 1, borderColor: 'divider', borderRadius: '12px', bgcolor: 'background.paper', mb: 2 }}><Chip icon={<NotificationsNoneOutlined />} label={unreadLabel} color={!summary.isError && (summary.data?.unreadCount ?? 0) > 0 ? 'primary' : 'default'} variant="outlined" /><TextField select label="Trạng thái" value={isRead} onChange={(event) => { setIsRead(event.target.value as typeof isRead); setPage(0); }} sx={{ minWidth: 180 }}><MenuItem value="unread">Chưa đọc</MenuItem><MenuItem value="read">Đã đọc</MenuItem><MenuItem value="all">Tất cả</MenuItem></TextField><TextField select label="Loại thông báo" value={type} onChange={(event) => { setType(event.target.value as NotificationType | ''); setPage(0); }} sx={{ minWidth: 220 }}><MenuItem value="">Tất cả loại</MenuItem>{types.map(item => <MenuItem key={item} value={item}>{typeLabels[item]}</MenuItem>)}</TextField></FilterBar>
    <Paper variant="outlined" sx={{ overflow: 'hidden' }}><Stack direction="row" justifyContent="space-between" sx={{ p: 2.5 }}><Typography fontWeight={750}>Danh sách thông báo</Typography><Typography variant="caption" color="text.secondary">{items.data?.totalCount ?? '—'} bản ghi</Typography></Stack><Divider />{items.isFetching && <LinearProgress />}
      {items.isError ? <Alert severity="error" sx={{ m: 2 }} action={<Button color="inherit" onClick={() => void items.refetch()}>Thử lại</Button>}>{errorMessage(items.error)}</Alert>
        : items.isLoading ? <LinearProgress /> : items.data?.items.length ? <><TableContainer><Table sx={{ minWidth: 760 }}><TableHead><TableRow><TableCell>TRẠNG THÁI</TableCell><TableCell>THÔNG BÁO</TableCell><TableCell>LOẠI</TableCell><TableCell>THỜI GIAN</TableCell><TableCell align="right">THAO TÁC</TableCell></TableRow></TableHead><TableBody>{items.data.items.map(item => <TableRow key={item.id} hover><TableCell><Chip size="small" color={item.isRead ? 'default' : 'primary'} label={item.isRead ? 'Đã đọc' : 'Mới'} variant={item.isRead ? 'outlined' : 'filled'} /></TableCell><TableCell><Typography fontWeight={item.isRead ? 500 : 750}>{item.title}</Typography><Typography variant="body2" color="text.secondary">{item.message}</Typography>{item.targetType && item.targetId && <Typography variant="caption" color="text.secondary">{item.targetType} #{item.targetId}</Typography>}</TableCell><TableCell><Chip size="small" variant="outlined" label={typeLabels[item.type] ?? item.type} /></TableCell><TableCell sx={{ whiteSpace: 'nowrap' }}>{dateTime(item.createdAt)}</TableCell><TableCell align="right">{!item.isRead && <Button size="small" disabled={markRead.isPending} onClick={() => markRead.mutate(item.id)}>Đánh dấu đã đọc</Button>}</TableCell></TableRow>)}</TableBody></Table></TableContainer><TablePagination component="div" count={items.data.totalCount} page={page} rowsPerPage={pageSize} rowsPerPageOptions={[10, 20, 50]} onPageChange={(_, value) => setPage(value)} onRowsPerPageChange={(event) => { setPageSize(Number(event.target.value)); setPage(0); }} labelRowsPerPage="Số dòng:" labelDisplayedRows={({ from, to, count }) => `${from}–${to} / ${count}`} /></> : <EmptyState title={isRead === 'unread' ? 'Không có thông báo chưa đọc' : 'Chưa có thông báo'} description="Thông báo mới xuất hiện khi có giao dịch vượt ngưỡng hoặc đơn hàng thay đổi trạng thái." />}
    </Paper>
  </>;
}
