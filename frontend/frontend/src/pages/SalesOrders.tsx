import { usePreferredPageSize } from '../settings';
import { useWarehouseScope, WarehouseScopeSelect, WarehouseScopeNote } from '../warehouses';
import { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Accordion, AccordionDetails, AccordionSummary, Alert, Box, Button, Chip, Divider, Drawer, IconButton, InputAdornment, LinearProgress, MenuItem, Paper, Skeleton, Stack, Table, TableBody, TableCell, TableContainer, TableHead, TablePagination, TableRow, TableSortLabel, TextField, Typography } from '@mui/material';
import AddOutlined from '@mui/icons-material/AddOutlined';
import SearchOutlined from '@mui/icons-material/SearchOutlined';
import VisibilityOutlined from '@mui/icons-material/VisibilityOutlined';
import CloseOutlined from '@mui/icons-material/CloseOutlined';
import ExpandMoreOutlined from '@mui/icons-material/ExpandMoreOutlined';
import { dateTime, errorMessage, money } from '../api';
import { useAuth } from '../auth';
import { ConfirmDialog, EmptyState, FilterBar, PageHeading, useDebounced, useToast } from '../shared';
import { invalidateReportsAndNotifications } from '../queryInvalidation';
import { RemoteEntitySelect } from '../components/RemoteEntitySelect';
import { SalesOrderEditor } from '../components/SalesOrderEditor';
import { SalesFulfillmentDialog } from '../components/SalesFulfillmentDialog';
import { getSalesOrder, listSalesOrders, salesCustomerOptions, salesStatusLabels, transitionSalesOrder } from '../salesOrders';
import type { SalesOrder, SalesQuery, SalesSort, SalesStatus } from '../salesOrders';
import type { Customer } from '../customers';

const statuses = Object.keys(salesStatusLabels) as SalesStatus[];
function StatusChip({ status }: { status: SalesStatus }) {
  const color = status === 'Fulfilled' ? 'success' : status === 'Cancelled' ? 'default' : status === 'Draft' ? 'default' : status === 'Confirmed' ? 'primary' : 'warning';
  return <Chip size="small" variant="outlined" color={color} label={salesStatusLabels[status] ?? status} />;
}
function canCancel(order: SalesOrder) { return order.fulfilledQuantity === 0 && ['Draft', 'Confirmed'].includes(order.status); }
export default function SalesOrders() {
  const { warehouseId } = useWarehouseScope();
  const client = useQueryClient(); const toast = useToast();
  const role = useAuth().user?.role;
  const canManage = role === 'Admin' || role === 'Staff';
  const [search, setSearch] = useState(''); const debounced = useDebounced(search.trim());
  const [customer, setCustomer] = useState<Customer | null>(null); const [status, setStatus] = useState<SalesStatus | ''>('');
  const [page, setPage] = useState(0); const [pageSize, setPageSize] = usePreferredPageSize();
  const [sortBy, setSortBy] = useState<SalesSort>('createdAt'); const [descending, setDescending] = useState(true);
  const [selectedId, setSelectedId] = useState<number | null>(null); const [editor, setEditor] = useState<{ order: SalesOrder | null } | null>(null);
  const [fulfilling, setFulfilling] = useState<SalesOrder | null>(null); const [cancelling, setCancelling] = useState<SalesOrder | null>(null);
  useEffect(() => setPage(0), [debounced, warehouseId]);
  const query: SalesQuery = { warehouseId, search: debounced || undefined, customerId: customer?.id, status: status || undefined, page: page + 1, pageSize, sortBy, descending };
  const orders = useQuery({ queryKey: ['sales-orders', query], queryFn: ({ signal }) => listSalesOrders(query, signal) });
  async function refresh(stockChanged = false) {
    const keys = ['sales-orders', 'sales-order']; if (stockChanged) keys.push('inventory', 'inventory-summary', 'products', 'product', 'stock-movements');
    await Promise.all([...keys.map((key) => client.invalidateQueries({ queryKey: [key] })), invalidateReportsAndNotifications(client)]);
  }
  const transition = useMutation({ mutationFn: ({ order, action }: { order: SalesOrder; action: 'confirm' | 'cancel' }) => transitionSalesOrder(order.id, action),
    onSuccess: async (updated, variables) => { client.setQueryData(['sales-order', updated.id], updated); setCancelling(null); toast(variables.action === 'confirm' ? 'Đã xác nhận đơn bán hàng.' : 'Đã hủy đơn bán hàng.'); await refresh(); },
    onError: async () => { await refresh(); },
  });
  function sort(field: SalesSort) { setDescending(sortBy === field ? !descending : field === 'createdAt' || field === 'updatedAt'); setSortBy(field); setPage(0); }
  function heading(field: SalesSort, label: string, align: 'left' | 'right' = 'left') {
    return <TableCell align={align} sortDirection={sortBy === field ? descending ? 'desc' : 'asc' : false}><TableSortLabel active={sortBy === field} direction={descending ? 'desc' : 'asc'} onClick={() => sort(field)}>{label}</TableSortLabel></TableCell>;
  }
  return <>
    <PageHeading eyebrow="BÁN HÀNG" title="Đơn bán hàng" description="Quản lý đơn bán và theo dõi tiến độ giao hàng."
      actions={canManage && <Button variant="contained" startIcon={<AddOutlined />} onClick={() => setEditor({ order: null })}>Tạo đơn bán hàng</Button>} />
    {transition.isError && <Alert severity="error" sx={{ mb: 2 }} onClose={() => transition.reset()}>{errorMessage(transition.error)} Trạng thái đã được tải lại từ máy chủ.</Alert>}
    <Paper variant="outlined" sx={{ overflow: 'hidden' }}>
      <FilterBar sx={{ alignItems: 'flex-start' }}>
        <TextField label="Tìm đơn bán hàng" placeholder="Tìm mã đơn hoặc khách hàng…" value={search} onChange={(event) => setSearch(event.target.value)} slotProps={{ input: { startAdornment: <InputAdornment position="start"><SearchOutlined fontSize="small" /></InputAdornment> } }} />
        <Box sx={{ flex: '1 1 210px', minWidth: 0, '& .MuiFormHelperText-root:not(.Mui-error)': { display: 'none' } }}><RemoteEntitySelect<Customer> label="Lọc khách hàng" value={customer} onChange={(value) => { setCustomer(value); setPage(0); }} optionLabel={(item) => item.code ? `${item.code} · ${item.name}` : item.name} loadPage={salesCustomerOptions} queryKey="sales-filter-customers" /></Box>
        <TextField select label="Trạng thái đơn bán" value={status} onChange={(event) => { setStatus(event.target.value as SalesStatus | ''); setPage(0); }}><MenuItem value="">Tất cả trạng thái</MenuItem>{statuses.map((item) => <MenuItem key={item} value={item}>{salesStatusLabels[item]}</MenuItem>)}</TextField>
        <Box sx={{ flex: '1 1 210px', minWidth: 0 }}><WarehouseScopeSelect /></Box>
      </FilterBar><Divider />
      <Stack direction="row" alignItems="center" justifyContent="space-between" spacing={2} sx={{ px: 2.5, py: 1.5 }}><Box><Typography fontWeight={650} fontSize={13}>Danh sách đơn bán hàng</Typography><WarehouseScopeNote compact /></Box><Typography variant="caption" color="text.secondary" sx={{ flexShrink: 0 }}>{orders.data?.totalCount ?? '—'} kết quả</Typography></Stack>
      {orders.isFetching && <LinearProgress />}
      {orders.isError ? <Alert severity="error" sx={{ m: 2.5 }} action={<Button color="inherit" onClick={() => void orders.refetch()}>Thử lại</Button>}>{errorMessage(orders.error)}</Alert>
        : orders.isLoading ? <Stack spacing={1} sx={{ p: 3 }}>{[1, 2, 3, 4, 5].map((value) => <Skeleton key={value} height={65} />)}</Stack>
        : orders.data?.items.length ? <>
          <TableContainer><Table sx={{ minWidth: 1100 }} aria-label="Danh sách đơn bán hàng"><TableHead><TableRow>{heading('code', 'MÃ ĐƠN')}{heading('customerName', 'KHÁCH HÀNG')}<TableCell>KHO</TableCell>{heading('status', 'TRẠNG THÁI')}<TableCell align="right">ĐÃ GIAO / ĐẶT</TableCell>{heading('totalAmount', 'GIÁ TRỊ BÁN', 'right')}{heading('createdAt', 'NGÀY TẠO')}{heading('updatedAt', 'CẬP NHẬT')}<TableCell align="right">CHI TIẾT</TableCell></TableRow></TableHead>
            <TableBody>{orders.data.items.map((order) => <TableRow key={order.id} hover><TableCell><Button onClick={() => { transition.reset(); setSelectedId(order.id); }} sx={{ p: 0, minHeight: 'auto', fontFamily: 'ui-monospace, monospace', fontSize: 12 }}>{order.code}</Button></TableCell><TableCell sx={{ fontSize: 13, fontWeight: 650 }}>{order.customerName}</TableCell><TableCell>{order.warehouseCode || `#${order.warehouseId}`}</TableCell><TableCell><StatusChip status={order.status} /></TableCell><TableCell align="right">{order.fulfilledQuantity} / {order.orderedQuantity}</TableCell><TableCell align="right" sx={{ fontWeight: 700, whiteSpace: 'nowrap' }}>{money(order.totalAmount)}</TableCell><TableCell sx={{ whiteSpace: 'nowrap', color: 'text.secondary', fontSize: 12 }}>{dateTime(order.createdAt)}</TableCell><TableCell sx={{ whiteSpace: 'nowrap', color: 'text.secondary', fontSize: 12 }}>{dateTime(order.updatedAt)}</TableCell><TableCell align="right"><IconButton aria-label={`Xem đơn bán hàng ${order.code}`} onClick={() => { transition.reset(); setSelectedId(order.id); }}><VisibilityOutlined fontSize="small" /></IconButton></TableCell></TableRow>)}</TableBody>
          </Table></TableContainer>
          <TablePagination component="div" count={orders.data.totalCount} page={page} rowsPerPage={pageSize} rowsPerPageOptions={[10, 25, 50]} onPageChange={(_, value) => setPage(value)} onRowsPerPageChange={(event) => { setPageSize(Number(event.target.value)); setPage(0); }} labelRowsPerPage="Số dòng:" labelDisplayedRows={({ from, to, count }) => `${from}–${to} / ${count}`} />
        </> : <EmptyState title="Chưa có đơn bán hàng phù hợp" description="Tạo đơn mới hoặc thay đổi từ khóa và bộ lọc để tìm đơn." action={canManage && <Button startIcon={<AddOutlined />} onClick={() => setEditor({ order: null })}>Tạo đơn bán hàng</Button>} />}
    </Paper>
    <SalesDetail id={selectedId} pending={transition.isPending} error={transition.isError ? errorMessage(transition.error) : undefined} onClose={() => setSelectedId(null)}
      canManage={canManage} onEdit={(order) => setEditor({ order })} onFulfill={setFulfilling} onConfirm={(order) => transition.mutate({ order, action: 'confirm' })} onCancel={(order) => { transition.reset(); setCancelling(order); }} />
    {canManage && editor && <SalesOrderEditor order={editor.order} onClose={() => setEditor(null)} onSaved={(saved) => { client.setQueryData(['sales-order', saved.id], saved); setEditor(null); setSelectedId(saved.id); setPage(0); void refresh(); }} />}
    {canManage && fulfilling && <SalesFulfillmentDialog order={fulfilling} onClose={() => setFulfilling(null)} onCompleted={(updated) => { client.setQueryData(['sales-order', updated.id], updated); setFulfilling(null); void refresh(true); }} />}
    <ConfirmDialog open={cancelling !== null} title="Hủy đơn bán hàng?" description={`Đơn ${cancelling?.code ?? ''} sẽ bị hủy. Chỉ được hủy trước khi giao bất kỳ hàng hóa nào.`}
      confirmLabel="Hủy đơn bán hàng" pending={transition.isPending} error={transition.isError ? errorMessage(transition.error) : undefined}
      onClose={() => setCancelling(null)} onConfirm={() => { if (cancelling) transition.mutate({ order: cancelling, action: 'cancel' }); }} />
  </>;
}

function SalesDetail({ id, canManage, pending, error, onClose, onEdit, onFulfill, onConfirm, onCancel }: {
  id: number | null; canManage: boolean; pending: boolean; error?: string; onClose: () => void; onEdit: (order: SalesOrder) => void;
  onFulfill: (order: SalesOrder) => void; onConfirm: (order: SalesOrder) => void; onCancel: (order: SalesOrder) => void;
}) {
  const detail = useQuery({ queryKey: ['sales-order', id], enabled: id !== null, queryFn: ({ signal }) => getSalesOrder(id!, signal) });
  const order = detail.data; const busy = pending || detail.isFetching;
  return <Drawer anchor="right" open={id !== null} onClose={pending ? undefined : onClose} sx={{ '& .MuiDrawer-paper': { width: { xs: '100%', lg: 880 } } }}>
    <Stack direction="row" justifyContent="space-between" alignItems="center" sx={{ p: 3 }}><Box><Typography variant="overline" color="primary">SALES ORDER</Typography><Typography variant="h6">{order?.code ?? 'Chi tiết đơn bán hàng'}</Typography></Box><IconButton aria-label="Đóng chi tiết đơn bán hàng" onClick={onClose} disabled={pending}><CloseOutlined /></IconButton></Stack><Divider />
    {detail.isLoading ? <Stack spacing={2} sx={{ p: 3 }}><Skeleton height={90} /><Skeleton height={180} /></Stack>
      : detail.isError ? <Alert severity="error" sx={{ m: 3 }} action={<Button color="inherit" onClick={() => void detail.refetch()}>Thử lại</Button>}>{errorMessage(detail.error)}</Alert>
      : order && <Stack spacing={3} sx={{ p: 3 }}>{error && <Alert severity="error">{error} Trạng thái được tải lại từ máy chủ.</Alert>}
        <Stack direction="row" justifyContent="space-between" spacing={2} alignItems="center"><Box><Typography variant="h6">{order.customerName}</Typography><Typography variant="caption" color="text.secondary">Người lập #{order.createdBy} · {dateTime(order.createdAt)}</Typography></Box><StatusChip status={order.status} /></Stack>
        <Alert severity="info" variant="outlined">Kho: {order.warehouseCode || `#${order.warehouseId}`} · {order.warehouseName}. Kho được cố định sau khi rời trạng thái nháp.</Alert>
        <Stack direction="row" spacing={1} flexWrap="wrap" useFlexGap>{canManage && order.status === 'Draft' && <><Button variant="outlined" disabled={busy} onClick={() => onEdit(order)}>Chỉnh sửa đơn</Button><Button variant="contained" disabled={busy} onClick={() => onConfirm(order)}>Xác nhận đơn</Button></>}{canManage && ['Confirmed', 'PartiallyFulfilled'].includes(order.status) && <Button variant="contained" disabled={busy} onClick={() => onFulfill(order)}>Giao hàng</Button>}{canManage && canCancel(order) && <Button variant="outlined" color="error" disabled={busy} onClick={() => onCancel(order)}>Hủy đơn</Button>}<Button disabled={busy} onClick={() => void detail.refetch()}>Làm mới đơn</Button></Stack>
        <Box sx={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 2 }}><Paper variant="outlined" sx={{ p: 2 }}><Typography variant="caption" color="text.secondary">GIÁ TRỊ ĐƠN BÁN</Typography><Typography variant="h6" sx={{ mt: 1 }}>{money(order.totalAmount)}</Typography></Paper><Paper variant="outlined" sx={{ p: 2 }}><Typography variant="caption" color="text.secondary">SỐ LƯỢNG ĐÃ GIAO / ĐẶT</Typography><Typography variant="h6" sx={{ mt: 1 }}>{order.fulfilledQuantity} / {order.orderedQuantity}</Typography></Paper></Box>
        {order.notes && <Box><Typography variant="caption" color="text.secondary">Ghi chú</Typography><Typography sx={{ whiteSpace: 'pre-wrap', mt: .5, fontSize: 13 }}>{order.notes}</Typography></Box>}
        <TableContainer sx={{ border: 1, borderColor: 'divider', borderRadius: 2 }}><Table sx={{ minWidth: 680 }} aria-label="Chi tiết dòng bán hàng"><TableHead><TableRow><TableCell>SẢN PHẨM</TableCell><TableCell align="right">ĐẶT / ĐÃ GIAO</TableCell><TableCell align="right">GIÁ BÁN</TableCell><TableCell align="right">THÀNH TIỀN</TableCell></TableRow></TableHead><TableBody>{order.items.map((item) => <TableRow key={item.id}><TableCell><Typography fontWeight={650} fontSize={13}>{item.productName}</Typography><Typography variant="caption" color="text.secondary">{item.sku}</Typography></TableCell><TableCell align="right">{item.quantity} / {item.fulfilledQuantity}</TableCell><TableCell align="right" sx={{ whiteSpace: 'nowrap', fontSize: 12 }}>{money(item.unitPrice)}</TableCell><TableCell align="right" sx={{ whiteSpace: 'nowrap', fontWeight: 650, fontSize: 12 }}>{money(item.lineTotal)}</TableCell></TableRow>)}</TableBody></Table></TableContainer>
        <Box><Typography variant="h6" sx={{ mb: 2 }}>Lịch sử giao hàng</Typography>{!order.fulfillments.length ? <Typography variant="body2" color="text.secondary">Chưa ghi nhận lần giao hàng.</Typography> : order.fulfillments.map((fulfillment) => <Accordion key={fulfillment.id} disableGutters elevation={0} sx={{ border: 1, borderColor: 'divider', borderRadius: '10px !important', mb: 1, '&::before': { display: 'none' } }}><AccordionSummary expandIcon={<ExpandMoreOutlined />}><Box><Typography fontSize={13} fontWeight={650}>Phiếu giao {fulfillment.id}</Typography><Typography variant="caption" color="text.secondary">{dateTime(fulfillment.createdAt)} · Người giao #{fulfillment.createdBy}</Typography></Box></AccordionSummary><AccordionDetails><Stack spacing={1.5}><Typography variant="caption" color="text.secondary" sx={{ overflowWrap: 'anywhere' }}>Request ID: {fulfillment.requestId}</Typography>{fulfillment.items.map((line) => { const item = order.items.find((candidate) => candidate.id === line.salesOrderItemId); return <Stack key={line.salesOrderItemId} direction="row" justifyContent="space-between" spacing={2}><Typography fontSize={13}>{item?.sku ?? `Sản phẩm #${line.productId}`} · −{line.quantity}</Typography><Typography variant="caption" color="text.secondary">Stock movement #{line.movementId}</Typography></Stack>; })}</Stack></AccordionDetails></Accordion>)}</Box>
        <Alert severity="info" variant="outlined">Giá trị này là doanh thu theo đơn bán (chưa trừ giá vốn/chiết khấu); không phải lợi nhuận. Giá vốn sản phẩm không bị thay đổi khi lập đơn.</Alert>
        <Typography variant="caption" color="text.secondary">Cập nhật: {dateTime(order.updatedAt)}</Typography>
      </Stack>}
  </Drawer>;
}
