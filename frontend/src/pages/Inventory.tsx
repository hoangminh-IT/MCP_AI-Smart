import { usePreferredPageSize } from '../settings';
import { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Alert, Box, Button, Chip, Dialog, DialogActions, DialogContent, DialogTitle, Divider, LinearProgress, MenuItem, Paper, Skeleton, Stack, Table, TableBody, TableCell, TableContainer, TableHead, TablePagination, TableRow, TextField, Typography } from '@mui/material';
import AddOutlined from '@mui/icons-material/AddOutlined';
import RemoveOutlined from '@mui/icons-material/RemoveOutlined';
import TuneOutlined from '@mui/icons-material/TuneOutlined';
import { api, errorMessage, money } from '../api';
import { useAuth } from '../auth';
import { FilterBar, EmptyState, PageHeading, useToast } from '../shared';
import { invalidateReportsAndNotifications } from '../queryInvalidation';
import type { Page, Product, StockSummary, StockOperation } from '../types';
import { Link } from 'react-router-dom';
import { useWarehouseScope, WarehouseScopeSelect, WarehouseScopeNote, WarehouseSelect } from '../warehouses';

type Operation = 'receipt' | 'issue' | 'adjustment';
const operationLabels: Record<Operation, string> = { receipt: 'Nhập kho', issue: 'Xuất kho', adjustment: 'Điều chỉnh tồn' };

export default function Inventory() {
  const { warehouseId } = useWarehouseScope();
  const [operationWarehouseId, setOperationWarehouseId] = useState<number>();
  const admin = useAuth().user?.role === 'Admin';
  const client = useQueryClient();
  const toast = useToast();
  const [search, setSearch] = useState('');
  const [stockState, setStockState] = useState('all');
  const [page, setPage] = useState(0);
  const [pageSize, setPageSize] = usePreferredPageSize();
  const [operation, setOperation] = useState<Operation | null>(null);
  const [selected, setSelected] = useState<Product | null>(null);
  const [quantity, setQuantity] = useState('');
  const [reason, setReason] = useState('');
  const [requestId, setRequestId] = useState('');
  const [validation, setValidation] = useState('');
  const summary = useQuery({ queryKey: ['inventory-summary', warehouseId], queryFn: async ({ signal }) => (await api.get<StockSummary>('/inventory/summary', { params: { warehouseId }, signal })).data });
  const params = { warehouseId, search: search.trim() || undefined, stockState, page: page + 1, pageSize, sortBy: 'name' };
  const inventory = useQuery({ queryKey: ['inventory', params], queryFn: async ({ signal }) => (await api.get<Page<Product>>('/inventory', { params, signal })).data });
  const localStock = useQuery({ queryKey: ['inventory-action-stock', selected?.id, operationWarehouseId], enabled: Boolean(operation && selected && operationWarehouseId), queryFn: async ({ signal }) => (await api.get<Product>(`/inventory/${selected!.id}`, { params: { warehouseId: operationWarehouseId }, signal })).data });
  const submit = useMutation({
    mutationFn: async () => {
      if (!operation || !selected || !operationWarehouseId) throw new Error('Chọn sản phẩm trước khi thao tác.');
      const amount = Number(quantity);
      if (!Number.isSafeInteger(amount) || amount > 2_147_483_647 || amount < 0 || (operation !== 'adjustment' && amount < 1)) throw new Error(operation === 'adjustment' ? 'Số lượng mục tiêu phải là số nguyên không âm.' : 'Số lượng phải là số nguyên dương.');
      if (!reason.trim()) throw new Error('Nhập lý do thao tác.');
      const body = { warehouseId: operationWarehouseId, productId: selected.id, quantity: amount, reason: reason.trim(), requestId };
      return (await api.post<StockOperation>(`/inventory/${operation === 'receipt' ? 'receipts' : operation === 'issue' ? 'issues' : 'adjustments'}`, body)).data;
    },
    onSuccess: async (result) => {
      toast(`${operationLabels[operation!]} thành công · tồn kho ${result.warehouseCode || result.warehouseId}: ${result.warehouseQuantityBefore ?? '—'} → ${result.warehouseQuantityAfter ?? '—'} · tổng ${result.quantityBefore} → ${result.quantityAfter}${result.replayed ? ' · request đã được xác nhận trước đó' : ''}.`);
      setOperation(null); setSelected(null); setQuantity(''); setReason(''); setValidation('');
      await Promise.all([client.invalidateQueries({ queryKey: ['inventory'] }), client.invalidateQueries({ queryKey: ['inventory-summary'] }), client.invalidateQueries({ queryKey: ['products'] }), client.invalidateQueries({ queryKey: ['mcp-executions'] }), invalidateReportsAndNotifications(client)]);
    },
  });
  useEffect(() => setPage(0), [search, stockState, warehouseId]);
  function openAction(product: Product, action: Operation) {
    submit.reset(); setValidation(''); setOperationWarehouseId(warehouseId); setSelected(product); setOperation(action); setQuantity(''); setReason(''); setRequestId(crypto.randomUUID());
  }
  const metrics = summary.data ? [
    ['Sản phẩm hoạt động', summary.data.totalProducts.toLocaleString('vi-VN')],
    ['Tổng số lượng', summary.data.totalQuantity.toLocaleString('vi-VN')],
    ['Cảnh báo tồn thấp', summary.data.lowStockCount.toLocaleString('vi-VN')],
    ['Hết hàng', summary.data.outOfStockCount.toLocaleString('vi-VN')],
    ['Giá trị theo giá vốn', money(summary.data.inventoryCostValue)],
    ['Giá trị theo giá bán', money(summary.data.inventoryRetailValue)],
  ] : [];
  return <><PageHeading eyebrow="QUẢN LÝ KHO" title="Tồn kho" description="Theo dõi số lượng thực tế và ghi nhận nhập, xuất hoặc kiểm kê kho." actions={<Stack direction="row" spacing={1} flexWrap="wrap" useFlexGap><Button component={Link} to="/transfers" variant="outlined">Chuyển kho</Button><Button component={Link} to="/stock-movements" variant="outlined">Lịch sử biến động</Button></Stack>} />
    <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 1.5, fontSize: 11.5 }}>Định mức dùng chung cho từng sản phẩm; cảnh báo hệ thống dựa trên tổng tồn các kho.</Typography>
    {summary.isError && <Alert severity="error" sx={{ mb: 2 }} action={<Button color="inherit" onClick={() => void summary.refetch()}>Thử lại</Button>}>{errorMessage(summary.error)}</Alert>}
    <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr 1fr', md: 'repeat(3, 1fr)', xl: 'repeat(6, 1fr)' }, gap: 1.5, mb: 2.5 }}>{summary.isLoading ? Array.from({ length: 6 }, (_, i) => <Paper key={i} variant="outlined" sx={{ p: 2 }}><Skeleton /><Skeleton width="60%" height={38} /></Paper>) : metrics.map(([label, value]) => <Paper key={label} variant="outlined" sx={{ p: 2 }}><Typography variant="caption" color="text.secondary">{label}</Typography><Typography fontWeight={800} fontSize={18} sx={{ mt: 1 }}>{value}</Typography></Paper>)}</Box>
    <Paper variant="outlined" sx={{ overflow: 'hidden' }}><FilterBar><TextField label="Tìm sản phẩm hoặc SKU" value={search} onChange={(event) => setSearch(event.target.value)} fullWidth /><TextField select label="Tình trạng tồn kho" value={stockState} onChange={(event) => setStockState(event.target.value)} sx={{ minWidth: 200 }}><MenuItem value="all">Tất cả</MenuItem><MenuItem value="low">Dưới định mức</MenuItem><MenuItem value="out">Hết hàng</MenuItem></TextField><Box sx={{ flex: '1 1 220px', maxWidth: { md: 300 }, minWidth: 0 }}><WarehouseScopeSelect /></Box></FilterBar><Box sx={{ px: { xs: 2, md: 2.5 }, pb: 1.5 }}><WarehouseScopeNote compact /></Box><Divider /><Stack direction="row" justifyContent="space-between" sx={{ px: 2.5, py: 1.8 }}><Typography fontWeight={700}>Danh sách tồn kho</Typography><Typography variant="caption" color="text.secondary">{inventory.data?.totalCount ?? 0} sản phẩm</Typography></Stack>{inventory.isFetching && <LinearProgress />}
      {inventory.isError ? <Alert severity="error" sx={{ m: 2 }} action={<Button color="inherit" onClick={() => void inventory.refetch()}>Thử lại</Button>}>{errorMessage(inventory.error)}</Alert> : inventory.isLoading ? <Stack sx={{ p: 2 }}>{[1, 2, 3, 4].map((i) => <Skeleton key={i} height={60} />)}</Stack> : inventory.data?.items.length ? <><TableContainer><Table sx={{ minWidth: 850 }}><TableHead><TableRow><TableCell>SKU</TableCell><TableCell>SẢN PHẨM</TableCell><TableCell>DANH MỤC</TableCell><TableCell align="right">TỒN KHO</TableCell><TableCell align="right">ĐỊNH MỨC</TableCell><TableCell align="right">THAO TÁC</TableCell></TableRow></TableHead><TableBody>{inventory.data.items.map((product) => <TableRow hover key={product.id}><TableCell>{product.sku}</TableCell><TableCell><Typography fontWeight={650}>{product.name}</Typography></TableCell><TableCell>{product.categoryName}</TableCell><TableCell align="right"><Chip size="small" label={product.quantity} color={product.quantity === 0 ? 'error' : product.quantity < product.minStock ? 'warning' : 'success'} /></TableCell><TableCell align="right">{product.minStock}</TableCell><TableCell align="right"><Stack direction="row" justifyContent="flex-end" spacing={0.5}><Button size="small" startIcon={<AddOutlined />} onClick={() => openAction(product, 'receipt')}>Nhập</Button><Button size="small" startIcon={<RemoveOutlined />} disabled={product.quantity === 0} onClick={() => openAction(product, 'issue')}>Xuất</Button>{admin && <Button size="small" startIcon={<TuneOutlined />} onClick={() => openAction(product, 'adjustment')}>Kiểm kê</Button>}</Stack></TableCell></TableRow>)}</TableBody></Table></TableContainer><TablePagination component="div" count={inventory.data.totalCount} page={page} onPageChange={(_, value) => setPage(value)} rowsPerPage={pageSize} onRowsPerPageChange={(event) => { setPageSize(Number(event.target.value)); setPage(0); }} rowsPerPageOptions={[10, 25, 50]} /></> : <EmptyState title="Chưa có sản phẩm tồn kho" description="Tạo sản phẩm trước, sau đó ghi nhận số lượng nhập kho." />}
    </Paper>
    <Dialog open={Boolean(operation)} onClose={submit.isPending ? undefined : () => setOperation(null)} fullWidth maxWidth="sm"><DialogTitle>{operation && operationLabels[operation]} · {selected?.name}</DialogTitle><DialogContent><Stack spacing={2.5} sx={{ pt: 1 }}>{(validation || submit.isError) && <Alert severity="error">{validation || errorMessage(submit.error)}</Alert>}<WarehouseSelect label="Kho thao tác" value={operationWarehouseId} onChange={setOperationWarehouseId} activeOnly disabled={submit.isPending} /><Alert severity="info">Tồn tại kho thao tác: {localStock.isFetching ? 'Đang tải…' : localStock.data?.quantity ?? 'Chưa xác định'}. {operation === 'adjustment' ? 'Nhập số lượng kiểm kê mục tiêu.' : 'Request ID được giữ nguyên khi thử lại để ngăn ghi trùng.'}</Alert>{localStock.isError && <Alert severity="error" action={<Button color="inherit" onClick={() => void localStock.refetch()}>Tải lại tồn kho</Button>}>{errorMessage(localStock.error)}</Alert>}<TextField label={operation === 'adjustment' ? 'Số lượng thực tế sau kiểm kê' : 'Số lượng'} type="number" autoFocus value={quantity} onChange={(event) => setQuantity(event.target.value)} slotProps={{ htmlInput: { min: operation === 'adjustment' ? 0 : 1, step: 1 } }} /><TextField label="Lý do" value={reason} onChange={(event) => setReason(event.target.value)} multiline minRows={2} required inputProps={{ maxLength: 1000 }} /><Typography variant="caption" color="text.secondary">Request ID: {requestId}</Typography></Stack></DialogContent><DialogActions sx={{ p: 2.5 }}><Button onClick={() => setOperation(null)} disabled={submit.isPending}>Hủy</Button><Button variant="contained" disabled={submit.isPending || !operationWarehouseId || localStock.isFetching || localStock.isError || !quantity || !reason.trim()} onClick={() => { setValidation(''); submit.mutate(); }}>{submit.isPending ? 'Đang ghi nhận…' : 'Xác nhận thao tác'}</Button></DialogActions></Dialog>
  </>;
}
