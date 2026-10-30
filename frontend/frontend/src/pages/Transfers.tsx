import { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import axios from 'axios';
import { Alert, Box, Button, Chip, Divider, Paper, Stack, TableCell, TableRow, TextField, Typography } from '@mui/material';
import { api, dateTime, errorMessage } from '../api';
import { useAuth } from '../auth';
import { ConfirmDialog, PageHeading, useToast } from '../shared';
import { ServerDataTable } from '../components/ServerDataTable';
import { RemoteEntitySelect } from '../components/RemoteEntitySelect';
import { purchaseProductOptions } from '../purchaseOrders';
import { invalidateReportsAndNotifications } from '../queryInvalidation';
import { listTransfers, transferStock, useWarehouseOptions, useWarehouseScope, WarehouseScopeNote, WarehouseScopeSelect, WarehouseSelect } from '../warehouses';
import type { StockTransfer, TransferInput } from '../warehouses';
import type { Product } from '../types';
import { usePreferredPageSize } from '../settings';

type PendingTransfer = { userId: number; input: TransferInput; product: Product };
function restoredIntent(key: string, userId?: number): PendingTransfer | null {
  try { const saved = JSON.parse(sessionStorage.getItem(key) ?? 'null') as PendingTransfer | null; return saved?.userId === userId && saved?.input.requestId && saved.input.quantity > 0 && saved.product.id === saved.input.productId ? saved : null; } catch { return null; }
}
export default function Transfers() {
  const { user } = useAuth(); const cache = useQueryClient(); const toast = useToast(); const { warehouseId } = useWarehouseScope();
  const storageKey = `smart-inventory-transfer-intent-${user?.id}`; const [restored] = useState(() => restoredIntent(storageKey, user?.id));
  const [pending, setPending] = useState<PendingTransfer | null>(restored); const [review, setReview] = useState<TransferInput | null>(null);
  const [product, setProduct] = useState<Product | null>(restored?.product ?? null);
  const [source, setSource] = useState<number | undefined>(restored?.input.sourceWarehouseId ?? warehouseId);
  const [destination, setDestination] = useState<number | undefined>(restored?.input.destinationWarehouseId);
  const [quantity, setQuantity] = useState(restored ? String(restored.input.quantity) : ''); const [reason, setReason] = useState(restored?.input.reason ?? '');
  const [validation, setValidation] = useState(''); const [recoveryMessage, setRecoveryMessage] = useState(''); const [recovering, setRecovering] = useState(false);
  const [result, setResult] = useState<StockTransfer | null>(null); const [detail, setDetail] = useState<StockTransfer | null>(null);
  const [page, setPage] = useState(0); const [pageSize, setPageSize] = usePreferredPageSize();
  const warehouses = useWarehouseOptions(); const warehouseName = (id: number) => { const item = warehouses.data?.find((row) => row.id === id); return item ? `${item.code} · ${item.name}` : `Kho #${id}`; };
  const sourceStock = useQuery({ queryKey: ['transfer-source-stock', product?.id, source], enabled: Boolean(product && source), queryFn: async ({ signal }) => (await api.get<Product>(`/inventory/${product!.id}`, { params: { warehouseId: source }, signal })).data });
  const history = useQuery({ queryKey: ['stock-transfers', warehouseId, page, pageSize], queryFn: ({ signal }) => listTransfers({ warehouseId, page: page + 1, pageSize }, signal) });
  useEffect(() => setPage(0), [warehouseId]);
  function clearPending() { setPending(null); try { sessionStorage.removeItem(storageKey); } catch { /* State remains in memory. */ } }
  async function refresh() {
    await Promise.all(['stock-transfers', 'inventory', 'inventory-summary', 'products', 'product', 'stock-movements', 'transfer-source-stock'].map((key) => cache.invalidateQueries({ queryKey: [key] })));
    await invalidateReportsAndNotifications(cache);
  }
  async function complete(value: StockTransfer) { clearPending(); setReview(null); setResult(value); setRecoveryMessage(''); toast(value.replayed ? 'Đã khôi phục phiếu chuyển kho trước đó.' : 'Đã chuyển kho và ghi hai biến động.'); await refresh(); }
  async function recover(input: TransferInput) {
    setRecovering(true);
    try { const found = await listTransfers({ requestId: input.requestId, page: 1, pageSize: 10 });
      if (found.items[0]) await complete(found.items[0]);
      else setRecoveryMessage('Chưa tìm thấy phiếu hoàn tất. Yêu cầu cũ có thể còn xử lý; nội dung được khóa, chỉ thử lại cùng Request ID.');
    } catch (error) { setRecoveryMessage(`Chưa kiểm tra được kết quả. ${errorMessage(error)} Chỉ thử lại cùng Request ID.`); }
    finally { setRecovering(false); }
  }
  useEffect(() => { if (restored) void recover(restored.input); }, []);
  const transfer = useMutation({ retry: false, mutationFn: transferStock,
    onMutate: (input) => { const saved = { userId: user!.id, input, product: product! }; setPending(saved); try { sessionStorage.setItem(storageKey, JSON.stringify(saved)); } catch { /* In-memory request ID remains stable until the page closes. */ } },
    onSuccess: complete,
    onError: async (error, input) => {
      const status = axios.isAxiosError(error) ? error.response?.status : undefined;
      if (status && status >= 400 && status < 500 && status !== 408) { clearPending(); setReview(null); await refresh(); }
      else { setReview(null); await recover(input); }
    },
  });
  const locked = Boolean(pending) || transfer.isPending || recovering;
  function prepare() {
    setValidation(''); setResult(null); transfer.reset();
    const amount = Number(quantity);
    if (!product || !source || !destination || source === destination) { setValidation('Chọn sản phẩm và hai kho khác nhau.'); return; }
    if (!Number.isInteger(amount) || amount < 1 || amount > 2_147_483_647) { setValidation('Số lượng chuyển phải là số nguyên dương.'); return; }
    if (!reason.trim()) { setValidation('Nhập lý do chuyển kho.'); return; }
    if (sourceStock.data && amount > sourceStock.data.quantity) { setValidation('Số lượng vượt tồn kho nguồn đã tải. Hãy kiểm tra lại tồn.'); return; }
    setReview({ productId: product.id, sourceWarehouseId: source, destinationWarehouseId: destination, quantity: amount, reason: reason.trim(), requestId: crypto.randomUUID() });
  }
  return <><PageHeading eyebrow="QUẢN LÝ KHO" title="Chuyển kho" description="Chuyển hàng giữa hai kho trong một giao dịch, giữ nguyên tổng tồn toàn hệ thống." actions={<WarehouseScopeSelect />} /><WarehouseScopeNote />
    <Paper variant="outlined" sx={{ p: { xs: 2, md: 2.5 }, mb: 2.5 }}><Stack spacing={2}>
      <Typography variant="h6">Lập phiếu chuyển kho</Typography>
      {(validation || transfer.isError && !result) && <Alert severity="error">{validation || errorMessage(transfer.error)}</Alert>}
      {pending && <Alert severity="warning">Đang giữ yêu cầu {pending.input.requestId}. Chưa coi thao tác thành công cho đến khi máy chủ trả phiếu thực tế. {recoveryMessage}</Alert>}
      <RemoteEntitySelect<Product> label="Sản phẩm chuyển kho" value={product} onChange={setProduct} optionLabel={(item) => `${item.sku} · ${item.name}`} loadPage={purchaseProductOptions} queryKey="transfer-products" disabled={locked} />
      <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', sm: '1fr 1fr' }, gap: 2 }}><WarehouseSelect label="Kho nguồn" value={source} onChange={setSource} activeOnly disabled={locked} /><WarehouseSelect label="Kho đích" value={destination} onChange={setDestination} activeOnly disabled={locked} defaultToMain={false} /></Box>
      {product && <Stack direction="row" spacing={1} alignItems="center"><Typography variant="body2" color="text.secondary">Tồn kho nguồn: {sourceStock.isFetching ? 'Đang tải…' : sourceStock.isError ? 'Chưa xác định' : sourceStock.data?.quantity ?? '—'}</Typography><Button disabled={sourceStock.isFetching || !source} onClick={() => void sourceStock.refetch()}>Kiểm tra tồn nguồn</Button></Stack>}
      {sourceStock.isError && <Alert severity="error">{errorMessage(sourceStock.error)}</Alert>}
      <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', sm: 'minmax(170px, .65fr) minmax(0, 1.35fr)' }, gap: 2, alignItems: 'start' }}><TextField label="Số lượng chuyển" type="number" value={quantity} onChange={(event) => setQuantity(event.target.value)} disabled={locked} slotProps={{ htmlInput: { min: 1, max: 2_147_483_647, step: 1 } }} />
      <TextField label="Lý do chuyển kho" value={reason} onChange={(event) => setReason(event.target.value)} multiline minRows={2} disabled={locked} slotProps={{ htmlInput: { maxLength: 1000 } }} /></Box>
      <Stack direction="row" spacing={1} flexWrap="wrap" useFlexGap justifyContent="flex-end">{pending ? <><Button variant="contained" disabled={transfer.isPending || recovering} onClick={() => transfer.mutate(pending.input)}>Thử lại cùng Request ID</Button><Button disabled={transfer.isPending || recovering} onClick={() => void recover(pending.input)}>Kiểm tra kết quả chuyển kho</Button></> : <Button variant="contained" disabled={!product || !source || !destination || !quantity || !reason.trim() || sourceStock.isFetching || sourceStock.isError} onClick={prepare}>Xem và xác nhận chuyển kho</Button>}</Stack>
      {result && <TransferResult transfer={result} warehouseName={warehouseName} />}
    </Stack></Paper>
    <Paper variant="outlined" sx={{ overflow: 'hidden' }}><Typography variant="h6" sx={{ p: 2.5 }}>Lịch sử chuyển kho</Typography><Divider /><ServerDataTable<StockTransfer> label="Lịch sử chuyển kho" rows={history.data?.items ?? []} rowKey={(row) => row.transferId} loading={history.isLoading} fetching={history.isFetching} error={history.isError ? errorMessage(history.error) : undefined} onRetry={() => void history.refetch()} totalCount={history.data?.totalCount ?? 0} page={page} pageSize={pageSize} onPageChange={setPage} onPageSizeChange={(value) => { setPageSize(value); setPage(0); }} emptyTitle="Chưa có phiếu chuyển kho" emptyDescription="Phiếu chuyển thành công sẽ xuất hiện với hai stock movements thực tế." head={<TableRow><TableCell>THỜI GIAN</TableCell><TableCell>NGUỒN → ĐÍCH</TableCell><TableCell>SẢN PHẨM</TableCell><TableCell>SỐ LƯỢNG</TableCell><TableCell>REQUEST ID</TableCell><TableCell /></TableRow>} renderRow={(row) => <TableRow hover><TableCell sx={{ whiteSpace: 'nowrap' }}>{dateTime(row.createdAt)}</TableCell><TableCell>{warehouseName(row.sourceWarehouseId)} → {warehouseName(row.destinationWarehouseId)}</TableCell><TableCell>#{row.productId}</TableCell><TableCell>{row.quantity}</TableCell><TableCell sx={{ maxWidth: 180, overflowWrap: 'anywhere' }}>{row.requestId}</TableCell><TableCell><Button aria-label={`Xem phiếu chuyển ${row.transferId}`} onClick={() => setDetail(detail?.transferId === row.transferId ? null : row)}>Chi tiết</Button></TableCell></TableRow>} />{detail && <Box sx={{ p: 2.5 }}><TransferResult transfer={detail} warehouseName={warehouseName} /></Box>}</Paper>
    <ConfirmDialog open={Boolean(review)} title="Xác nhận chuyển kho?" description={review ? `${product?.sku}: chuyển ${review.quantity} từ ${warehouseName(review.sourceWarehouseId)} đến ${warehouseName(review.destinationWarehouseId)}. Tổng tồn toàn hệ thống không đổi. Lý do: ${review.reason}. Request ID: ${review.requestId}` : ''} confirmLabel="Xác nhận chuyển kho" pending={transfer.isPending} onClose={() => setReview(null)} onConfirm={() => { if (review) transfer.mutate(review); }} />
  </>;
}
function TransferResult({ transfer, warehouseName }: { transfer: StockTransfer; warehouseName: (id: number) => string }) {
  return <Stack spacing={1.5}><Alert severity="success">Phiếu chuyển kho {transfer.transferId} · {transfer.replayed ? 'Kết quả đã lưu, không ghi trùng' : 'Đã ghi nhận'}</Alert><Typography variant="caption" color="text.secondary" sx={{ overflowWrap: 'anywhere' }}>Request ID: {transfer.requestId} · {dateTime(transfer.createdAt)} · Người tạo #{transfer.createdBy}</Typography>{transfer.movements.map((movement) => <Paper key={movement.movementId} variant="outlined" sx={{ p: 2 }}><Stack direction="row" spacing={1} alignItems="center" flexWrap="wrap" useFlexGap><Chip size="small" label={movement.type} variant="outlined" /><Typography fontWeight={650}>{warehouseName(movement.warehouseId)} · {movement.warehouseQuantityBefore ?? '—'} → {movement.warehouseQuantityAfter ?? '—'}</Typography></Stack><Typography variant="caption" color="text.secondary">Stock movement #{movement.movementId} · Tổng toàn hệ thống {movement.quantityBefore} → {movement.quantityAfter}</Typography></Paper>)}</Stack>;
}
