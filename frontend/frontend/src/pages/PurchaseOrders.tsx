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
import { PurchaseOrderEditor } from '../components/PurchaseOrderEditor';
import { PurchaseReceiptDialog } from '../components/PurchaseReceiptDialog';
import { RemoteEntitySelect } from '../components/RemoteEntitySelect';
import { getPurchaseOrder, listPurchaseOrders, purchaseStatusLabels, transitionPurchaseOrder } from '../purchaseOrders';
import type { PurchaseOrder, PurchaseQuery, PurchaseSort, PurchaseStatus } from '../purchaseOrders';
import { listSuppliers } from '../suppliers';
import type { Supplier } from '../suppliers';

const statuses = Object.keys(purchaseStatusLabels) as PurchaseStatus[];
const filterSuppliers = (search: string, page: number, signal?: AbortSignal) => listSuppliers({ search, page, pageSize: 50, sortBy: 'name', descending: false }, signal);

function PurchaseStatusChip({ status }: { status: PurchaseStatus }) {
  const color = status === 'Received' ? 'success' : status === 'Cancelled' ? 'default' : status === 'Draft' ? 'default' : status === 'Submitted' ? 'warning' : 'primary';
  return <Chip label={purchaseStatusLabels[status] ?? status} size="small" variant="outlined" color={color} />;
}
function canCancel(order: PurchaseOrder) {
  return order.receivedQuantity === 0 && ['Draft', 'Submitted', 'Approved'].includes(order.status);
}

export default function PurchaseOrders() {
  const { warehouseId } = useWarehouseScope();
  const admin = useAuth().user?.role === 'Admin';
  const queryClient = useQueryClient();
  const toast = useToast();
  const [search, setSearch] = useState('');
  const debouncedSearch = useDebounced(search.trim());
  const [supplier, setSupplier] = useState<Supplier | null>(null);
  const [status, setStatus] = useState<PurchaseStatus | ''>('');
  const [page, setPage] = useState(0);
  const [pageSize, setPageSize] = usePreferredPageSize();
  const [sortBy, setSortBy] = useState<PurchaseSort>('createdAt');
  const [descending, setDescending] = useState(true);
  const [selectedId, setSelectedId] = useState<number | null>(null);
  const [editor, setEditor] = useState<{ order: PurchaseOrder | null } | null>(null);
  const [receiving, setReceiving] = useState<PurchaseOrder | null>(null);
  const [cancelling, setCancelling] = useState<PurchaseOrder | null>(null);
  useEffect(() => setPage(0), [debouncedSearch, warehouseId]);
  const query: PurchaseQuery = { warehouseId, search: debouncedSearch || undefined, supplierId: supplier?.id, status: status || undefined, page: page + 1, pageSize, sortBy, descending };
  const orders = useQuery({ queryKey: ['purchase-orders', query], queryFn: ({ signal }) => listPurchaseOrders(query, signal) });
  async function refresh(stockChanged = false) {
    const keys = ['purchase-orders', 'purchase-order'];
    if (stockChanged) keys.push('inventory', 'inventory-summary', 'products', 'product', 'stock-movements');
    await Promise.all([...keys.map((key) => queryClient.invalidateQueries({ queryKey: [key] })), invalidateReportsAndNotifications(queryClient)]);
  }
  const transition = useMutation({
    mutationFn: ({ order, action }: { order: PurchaseOrder; action: 'submit' | 'approve' | 'cancel' }) => transitionPurchaseOrder(order.id, action),
    onSuccess: async (updated, variables) => {
      queryClient.setQueryData(['purchase-order', updated.id], updated); setCancelling(null);
      toast(variables.action === 'submit' ? 'Đã gửi đơn mua hàng để duyệt.' : variables.action === 'approve' ? 'Đã phê duyệt đơn mua hàng.' : 'Đã hủy đơn mua hàng.');
      await refresh();
    },
    onError: async () => { await refresh(); },
  });
  function sort(field: PurchaseSort) { setDescending(sortBy === field ? !descending : field === 'createdAt' || field === 'updatedAt'); setSortBy(field); setPage(0); }
  function heading(field: PurchaseSort, label: string, align: 'left' | 'right' = 'left') {
    return <TableCell align={align} sortDirection={sortBy === field ? descending ? 'desc' : 'asc' : false}><TableSortLabel active={sortBy === field} direction={descending ? 'desc' : 'asc'} onClick={() => sort(field)}>{label}</TableSortLabel></TableCell>;
  }
  return <>
    <PageHeading eyebrow="QUẢN LÝ KHO" title="Đơn mua hàng" description="Theo dõi đặt hàng, phê duyệt và tiến độ nhận hàng."
      actions={<Button variant="contained" startIcon={<AddOutlined />} onClick={() => setEditor({ order: null })}>Tạo đơn mua hàng</Button>} />
    {transition.isError && <Alert severity="error" sx={{ mb: 2 }} onClose={() => transition.reset()}>{errorMessage(transition.error)} Trạng thái đã được tải lại từ máy chủ.</Alert>}
    <Paper variant="outlined" sx={{ overflow: 'hidden' }}>
      <FilterBar sx={{ alignItems: 'flex-start' }}>
        <TextField label="Tìm đơn mua hàng" placeholder="Tìm mã đơn hoặc nhà cung cấp…" value={search} onChange={(event) => setSearch(event.target.value)}
          slotProps={{ input: { startAdornment: <InputAdornment position="start"><SearchOutlined fontSize="small" /></InputAdornment> } }} />
        <Box sx={{ flex: '1 1 210px', minWidth: 0, '& .MuiFormHelperText-root:not(.Mui-error)': { display: 'none' } }}>
          <RemoteEntitySelect<Supplier> label="Lọc nhà cung cấp" value={supplier} onChange={(value) => { setSupplier(value); setPage(0); }}
            optionLabel={(item) => `${item.code} · ${item.name}`} loadPage={filterSuppliers} queryKey="purchase-filter-suppliers" />
        </Box>
        <TextField select label="Trạng thái đơn mua" value={status} onChange={(event) => { setStatus(event.target.value as PurchaseStatus | ''); setPage(0); }}>
          <MenuItem value="">Tất cả trạng thái</MenuItem>{statuses.map((item) => <MenuItem key={item} value={item}>{purchaseStatusLabels[item]}</MenuItem>)}
        </TextField>
        <Box sx={{ flex: '1 1 210px', minWidth: 0 }}><WarehouseScopeSelect /></Box>
      </FilterBar><Divider />
      <Stack direction="row" alignItems="center" justifyContent="space-between" spacing={2} sx={{ px: 2.5, py: 1.5 }}><Box><Typography fontWeight={650} fontSize={13}>Danh sách đơn mua hàng</Typography><WarehouseScopeNote compact /></Box><Typography variant="caption" color="text.secondary" sx={{ flexShrink: 0 }}>{orders.data?.totalCount ?? '—'} kết quả</Typography></Stack>
      {orders.isFetching && <LinearProgress />}
      {orders.isError ? <Alert severity="error" sx={{ m: 2.5 }} action={<Button color="inherit" onClick={() => void orders.refetch()}>Thử lại</Button>}>{errorMessage(orders.error)}</Alert>
        : orders.isLoading ? <Stack spacing={1} sx={{ p: 3 }}>{[1, 2, 3, 4, 5].map((value) => <Skeleton key={value} height={65} />)}</Stack>
        : orders.data?.items.length ? <>
          <TableContainer><Table sx={{ minWidth: 1150 }} aria-label="Danh sách đơn mua hàng">
            <TableHead><TableRow>{heading('code', 'MÃ ĐƠN')}{heading('supplierName', 'NHÀ CUNG CẤP')}<TableCell>KHO</TableCell>{heading('status', 'TRẠNG THÁI')}<TableCell align="right">ĐÃ NHẬN / ĐẶT</TableCell>{heading('totalAmount', 'GIÁ TRỊ MUA', 'right')}{heading('createdAt', 'NGÀY TẠO')}{heading('updatedAt', 'CẬP NHẬT')}<TableCell align="right">CHI TIẾT</TableCell></TableRow></TableHead>
            <TableBody>{orders.data.items.map((order) => <TableRow key={order.id} hover>
              <TableCell><Button onClick={() => { transition.reset(); setSelectedId(order.id); }} sx={{ p: 0, minHeight: 'auto', fontFamily: 'ui-monospace, monospace', fontSize: 12 }}>{order.code}</Button></TableCell>
              <TableCell sx={{ fontSize: 13, fontWeight: 650 }}>{order.supplierName}</TableCell>
              <TableCell>{order.warehouseCode || `#${order.warehouseId}`}</TableCell><TableCell><PurchaseStatusChip status={order.status} /></TableCell>
              <TableCell align="right" sx={{ fontVariantNumeric: 'tabular-nums' }}>{order.receivedQuantity} / {order.orderedQuantity}</TableCell>
              <TableCell align="right" sx={{ fontWeight: 700, fontSize: 13, whiteSpace: 'nowrap' }}>{money(order.totalAmount)}</TableCell>
              <TableCell sx={{ fontSize: 12, whiteSpace: 'nowrap', color: 'text.secondary' }}>{dateTime(order.createdAt)}</TableCell>
              <TableCell sx={{ fontSize: 12, whiteSpace: 'nowrap', color: 'text.secondary' }}>{dateTime(order.updatedAt)}</TableCell>
              <TableCell align="right"><IconButton aria-label={`Xem đơn mua hàng ${order.code}`} onClick={() => { transition.reset(); setSelectedId(order.id); }}><VisibilityOutlined fontSize="small" /></IconButton></TableCell>
            </TableRow>)}</TableBody>
          </Table></TableContainer>
          <TablePagination component="div" count={orders.data.totalCount} page={page} rowsPerPage={pageSize} rowsPerPageOptions={[10, 25, 50]}
            onPageChange={(_, value) => setPage(value)} onRowsPerPageChange={(event) => { setPageSize(Number(event.target.value)); setPage(0); }}
            labelRowsPerPage="Số dòng:" labelDisplayedRows={({ from, to, count }) => `${from}–${to} / ${count}`} />
        </> : <EmptyState title="Chưa có đơn mua hàng phù hợp" description="Tạo đơn mới hoặc thay đổi từ khóa và bộ lọc để tìm đơn đã lưu." />}
    </Paper>
    <PurchaseDetail id={selectedId} admin={admin} pending={transition.isPending} error={transition.isError ? errorMessage(transition.error) : undefined}
      onClose={() => setSelectedId(null)} onEdit={(order) => setEditor({ order })} onReceive={setReceiving}
      onSubmit={(order) => transition.mutate({ order, action: 'submit' })} onApprove={(order) => transition.mutate({ order, action: 'approve' })}
      onCancel={(order) => { transition.reset(); setCancelling(order); }} />
    {editor && <PurchaseOrderEditor order={editor.order} onClose={() => setEditor(null)} onSaved={(saved) => {
      queryClient.setQueryData(['purchase-order', saved.id], saved); setEditor(null); setSelectedId(saved.id); setPage(0); void refresh();
    }} />}
    {receiving && <PurchaseReceiptDialog order={receiving} onClose={() => setReceiving(null)} onCompleted={(updated) => {
      queryClient.setQueryData(['purchase-order', updated.id], updated); setReceiving(null); void refresh(true);
    }} />}
    <ConfirmDialog open={cancelling !== null} title="Hủy đơn mua hàng?" description={`Đơn ${cancelling?.code ?? ''} sẽ bị hủy. Chỉ được hủy khi chưa có hàng được nhận; thao tác này không thay đổi tồn kho.`}
      confirmLabel="Hủy đơn mua hàng" pending={transition.isPending} error={transition.isError ? errorMessage(transition.error) : undefined}
      onClose={() => setCancelling(null)} onConfirm={() => { if (cancelling) transition.mutate({ order: cancelling, action: 'cancel' }); }} />
  </>;
}

function PurchaseDetail({ id, admin, pending, error, onClose, onEdit, onReceive, onSubmit, onApprove, onCancel }: {
  id: number | null; admin: boolean; pending: boolean; error?: string; onClose: () => void;
  onEdit: (order: PurchaseOrder) => void; onReceive: (order: PurchaseOrder) => void;
  onSubmit: (order: PurchaseOrder) => void; onApprove: (order: PurchaseOrder) => void; onCancel: (order: PurchaseOrder) => void;
}) {
  const detail = useQuery({ queryKey: ['purchase-order', id], enabled: id !== null, queryFn: ({ signal }) => getPurchaseOrder(id!, signal) });
  const order = detail.data;
  const busy = pending || detail.isFetching;
  return <Drawer anchor="right" open={id !== null} onClose={pending ? undefined : onClose} sx={{ '& .MuiDrawer-paper': { width: { xs: '100%', lg: 880 } } }}>
    <Stack direction="row" justifyContent="space-between" alignItems="center" sx={{ p: 3 }}><Box><Typography variant="overline" color="primary">PURCHASE ORDER</Typography><Typography variant="h6">{order?.code ?? 'Chi tiết đơn mua hàng'}</Typography></Box><IconButton aria-label="Đóng chi tiết đơn mua hàng" onClick={onClose} disabled={pending}><CloseOutlined /></IconButton></Stack><Divider />
    {detail.isLoading ? <Stack spacing={2} sx={{ p: 3 }}><Skeleton height={90} /><Skeleton height={180} /></Stack>
      : detail.isError ? <Alert severity="error" sx={{ m: 3 }} action={<Button color="inherit" onClick={() => void detail.refetch()}>Thử lại</Button>}>{errorMessage(detail.error)}</Alert>
      : order && <Stack spacing={3} sx={{ p: 3 }}>
        {error && <Alert severity="error">{error} Trạng thái được tải lại từ máy chủ.</Alert>}
        <Stack direction="row" justifyContent="space-between" spacing={2} alignItems="center"><Box><Typography variant="h6">{order.supplierName}</Typography><Typography variant="caption" color="text.secondary">Người lập #{order.createdBy} · {dateTime(order.createdAt)}</Typography></Box><PurchaseStatusChip status={order.status} /></Stack>
        <Alert severity="info" variant="outlined">Kho: {order.warehouseCode || `#${order.warehouseId}`} · {order.warehouseName}. Kho được cố định sau khi rời trạng thái nháp.</Alert>
        <Stack direction="row" spacing={1} flexWrap="wrap" useFlexGap>
          {order.status === 'Draft' && <><Button variant="outlined" disabled={busy} onClick={() => onEdit(order)}>Chỉnh sửa đơn</Button><Button variant="contained" disabled={busy} onClick={() => onSubmit(order)}>Gửi duyệt</Button></>}
          {order.status === 'Submitted' && admin && <Button variant="contained" disabled={busy} onClick={() => onApprove(order)}>Phê duyệt</Button>}
          {['Approved', 'PartiallyReceived'].includes(order.status) && <Button variant="contained" disabled={busy} onClick={() => onReceive(order)}>Nhận hàng</Button>}
          {canCancel(order) && <Button variant="outlined" color="error" disabled={busy} onClick={() => onCancel(order)}>Hủy đơn</Button>}
          <Button disabled={busy} onClick={() => void detail.refetch()}>Làm mới đơn</Button>
        </Stack>
        <Box sx={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 2 }}><Paper variant="outlined" sx={{ p: 2 }}><Typography variant="caption" color="text.secondary">GIÁ TRỊ MUA HÀNG</Typography><Typography variant="h6" sx={{ mt: 1 }}>{money(order.totalAmount)}</Typography></Paper><Paper variant="outlined" sx={{ p: 2 }}><Typography variant="caption" color="text.secondary">SỐ LƯỢNG ĐÃ NHẬN / ĐẶT</Typography><Typography variant="h6" sx={{ mt: 1 }}>{order.receivedQuantity} / {order.orderedQuantity}</Typography></Paper></Box>
        {order.notes && <Box><Typography variant="caption" color="text.secondary">Ghi chú</Typography><Typography sx={{ whiteSpace: 'pre-wrap', mt: 0.5, fontSize: 13 }}>{order.notes}</Typography></Box>}
        <TableContainer sx={{ border: 1, borderColor: 'divider', borderRadius: 2 }}><Table sx={{ minWidth: 680 }} aria-label="Chi tiết dòng mua hàng"><TableHead><TableRow><TableCell>SẢN PHẨM</TableCell><TableCell align="right">ĐẶT / ĐÃ NHẬN</TableCell><TableCell align="right">ĐƠN GIÁ</TableCell><TableCell align="right">THÀNH TIỀN</TableCell></TableRow></TableHead><TableBody>{order.items.map((item) => <TableRow key={item.id}><TableCell><Typography fontWeight={650} fontSize={13}>{item.productName}</Typography><Typography variant="caption" color="text.secondary">{item.sku}</Typography></TableCell><TableCell align="right">{item.quantity} / {item.receivedQuantity}</TableCell><TableCell align="right" sx={{ whiteSpace: 'nowrap', fontSize: 12 }}>{money(item.unitCost)}</TableCell><TableCell align="right" sx={{ whiteSpace: 'nowrap', fontWeight: 650, fontSize: 12 }}>{money(item.lineTotal)}</TableCell></TableRow>)}</TableBody></Table></TableContainer>
        <Box><Typography variant="h6" sx={{ mb: 2 }}>Lịch sử nhận hàng</Typography>{!order.receipts.length ? <Typography variant="body2" color="text.secondary">Chưa ghi nhận phiếu nhận hàng.</Typography> : order.receipts.map((receipt) => <Accordion key={receipt.id} disableGutters elevation={0} sx={{ border: 1, borderColor: 'divider', borderRadius: '10px !important', mb: 1, '&::before': { display: 'none' } }}><AccordionSummary expandIcon={<ExpandMoreOutlined />}><Box><Typography fontSize={13} fontWeight={650}>Phiếu nhận {receipt.id}</Typography><Typography variant="caption" color="text.secondary">{dateTime(receipt.createdAt)} · Người nhận #{receipt.createdBy}</Typography></Box></AccordionSummary><AccordionDetails><Stack spacing={1.5}><Typography variant="caption" color="text.secondary" sx={{ overflowWrap: 'anywhere' }}>Request ID: {receipt.requestId}</Typography>{receipt.reason && <Typography variant="body2">{receipt.reason}</Typography>}{receipt.items.map((line) => {
          const item = order.items.find((candidate) => candidate.id === line.purchaseOrderItemId);
          return <Stack key={line.purchaseOrderItemId} direction="row" justifyContent="space-between" spacing={2}><Typography fontSize={13}>{item?.sku ?? `Sản phẩm #${line.productId}`} · +{line.quantity}</Typography><Typography variant="caption" color="text.secondary">Stock movement #{line.movementId}</Typography></Stack>;
        })}</Stack></AccordionDetails></Accordion>)}</Box>
        <Typography variant="caption" color="text.secondary">Cập nhật: {dateTime(order.updatedAt)}</Typography>
      </Stack>}
  </Drawer>;
}
