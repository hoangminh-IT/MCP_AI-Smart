import { useEffect, useState } from 'react';
import axios from 'axios';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Alert, Button, Dialog, DialogActions, DialogContent, DialogTitle, Stack, Table, TableBody, TableCell, TableContainer, TableHead, TableRow, TextField, Typography } from '@mui/material';
import { errorMessage } from '../api';
import { useAuth } from '../auth';
import { useToast } from '../shared';
import { fulfillSalesOrder, getSalesOrder } from '../salesOrders';
import type { FulfillmentInput, SalesOrder } from '../salesOrders';

type Intent = { input: FulfillmentInput };
function restore(key: string): Intent | null {
  try {
    const value = JSON.parse(sessionStorage.getItem(key) ?? 'null') as FulfillmentInput | null;
    if (!value || typeof value.requestId !== 'string' || !/^[0-9a-f-]{36}$/i.test(value.requestId) ||
      !(value.reason === null || typeof value.reason === 'string') || !Array.isArray(value.items) || value.items.length < 1 ||
      value.items.some((line) => !Number.isInteger(line.salesOrderItemId) || line.salesOrderItemId <= 0 || !Number.isInteger(line.quantity) || line.quantity <= 0)) return null;
    return { input: value };
  } catch { return null; }
}
export function SalesFulfillmentDialog({ order, onClose, onCompleted }: {
  order: SalesOrder; onClose: () => void; onCompleted: (order: SalesOrder) => void;
}) {
  const actorId = useAuth().user?.id;
  const key = `inventory:pending-sales-fulfillment:${actorId}:${order.id}`;
  const cache = useQueryClient(); const toast = useToast();
  const [intent, setIntent] = useState<Intent | null>(() => restore(key));
  const [quantities, setQuantities] = useState<Record<number, string>>(() => Object.fromEntries(restore(key)?.input.items.map((item) => [item.salesOrderItemId, String(item.quantity)]) ?? []));
  const [reason, setReason] = useState(() => restore(key)?.input.reason ?? '');
  const [review, setReview] = useState(Boolean(restore(key)));
  const [uncertain, setUncertain] = useState(Boolean(restore(key)));
  const [checking, setChecking] = useState(false);
  const [checkFailed, setCheckFailed] = useState(false);
  const [notFound, setNotFound] = useState(false);
  const [current, setCurrent] = useState(order);
  function clear() { try { sessionStorage.removeItem(key); } catch { /* in-memory state still resolves */ } setUncertain(false); }
  function persist(input: FulfillmentInput) {
    setIntent({ input }); setUncertain(true);
    try { sessionStorage.setItem(key, JSON.stringify(input)); } catch { /* Keep the exact operation in memory. */ }
  }
  async function verify(requestId: string, authoritativeFailure = false) {
    setChecking(true); setCheckFailed(false); setNotFound(false);
    try {
      const fresh = await getSalesOrder(order.id); setCurrent(fresh); cache.setQueryData(['sales-order', fresh.id], fresh);
      if (fresh.fulfillments.some((item) => item.requestId === requestId)) {
        clear(); toast('Phiếu giao đã được ghi nhận. Tồn kho không bị trừ lại.'); onCompleted(fresh);
      } else { if (authoritativeFailure) clear(); setNotFound(true); }
    } catch { setCheckFailed(true); } finally { setChecking(false); }
  }
  const mutation = useMutation({
    onMutate: (input: FulfillmentInput) => persist(input),
    mutationFn: (input: FulfillmentInput) => fulfillSalesOrder(order.id, input),
    onSuccess: (result) => { clear(); toast(result.replayed ? 'Phiếu đã được xử lý trước đó; không trừ tồn lần nữa.' : 'Đã giao hàng và cập nhật tồn kho.'); onCompleted(result.order); },
    onError: async (error, input) => {
      const status = axios.isAxiosError(error) ? error.response?.status : undefined;
      const authoritative = status !== undefined && status >= 400 && status < 500;
      if (!authoritative) persist(input);
      await verify(input.requestId, authoritative);
    },
  });
  useEffect(() => { if (intent) void verify(intent.input.requestId); }, []);
  const busy = mutation.isPending || checking;
  const eligible = current.status === 'Confirmed' || current.status === 'PartiallyFulfilled';
  const remaining = current.items.filter((item) => item.fulfilledQuantity < item.quantity);
  const withinRemaining = intent?.input.items.every((line) => {
    const item = current.items.find((candidate) => candidate.id === line.salesOrderItemId);
    return item && line.quantity <= item.quantity - item.fulfilledQuantity;
  }) ?? false;
  function reviewInput() {
    if (uncertain) { setReview(true); return; }
    if (!eligible) return;
    const items: FulfillmentInput['items'] = [];
    for (const item of remaining) {
      const value = quantities[item.id]?.trim() ?? ''; if (!value) continue;
      const quantity = Number(value);
      if (!Number.isInteger(quantity) || quantity <= 0 || quantity > item.quantity - item.fulfilledQuantity) return;
      items.push({ salesOrderItemId: item.id, quantity });
    }
    if (!items.length) return;
    items.sort((a, b) => a.salesOrderItemId - b.salesOrderItemId);
    setIntent({ input: { requestId: crypto.randomUUID(), reason: reason.trim() || null, items } }); setReview(true); setNotFound(false); mutation.reset();
  }
  return <>
    <Dialog open fullWidth maxWidth="md" onClose={busy ? undefined : onClose}>
      <DialogTitle>Giao hàng · {current.code}<Typography variant="body2" color="text.secondary" sx={{ mt: .5 }}>Nhập số lượng giao lần này. Mỗi lần giao trừ tồn kho và tạo biến động nguyên tử.</Typography></DialogTitle>
      <DialogContent><Stack spacing={2.5} sx={{ pt: 1 }}><Alert severity="info" variant="outlined">Kho xuất: {order.warehouseCode || `#${order.warehouseId}`} · {order.warehouseName}. Kho đã cố định theo đơn.</Alert>
        {!eligible && <Alert severity="warning">Đơn không ở trạng thái cho phép giao hàng.</Alert>}
        {checkFailed && <Alert severity="warning" action={<Button color="inherit" onClick={() => intent && void verify(intent.input.requestId)}>Kiểm tra lại</Button>}>Không xác minh được kết quả trên máy chủ; thao tác mới bị khóa.</Alert>}
        {notFound && <Alert severity="info">Chưa thấy phiếu này trong dữ liệu mới. Thử lại chỉ được dùng cùng Request ID và nội dung.</Alert>}
        {uncertain && <Alert severity="warning">Kết quả chưa được xác nhận. Nội dung và Request ID đã khóa để tránh giao/trừ kho trùng.</Alert>}
        {mutation.isError && <Alert severity="error">{errorMessage(mutation.error)}</Alert>}
        <TableContainer><Table sx={{ minWidth: 600 }} aria-label="Số lượng giao hàng"><TableHead><TableRow><TableCell>SẢN PHẨM</TableCell><TableCell align="right">ĐẶT / ĐÃ GIAO</TableCell><TableCell align="right">CÒN LẠI</TableCell><TableCell>GIAO LẦN NÀY</TableCell></TableRow></TableHead>
          <TableBody>{remaining.map((item) => <TableRow key={item.id}><TableCell><Typography fontWeight={650}>{item.productName}</Typography><Typography variant="caption" color="text.secondary">{item.sku}</Typography></TableCell><TableCell align="right">{item.quantity} / {item.fulfilledQuantity}</TableCell><TableCell align="right">{item.quantity - item.fulfilledQuantity}</TableCell><TableCell><TextField label={`Số lượng giao ${item.sku}`} type="number" value={quantities[item.id] ?? ''} disabled={busy || uncertain || !eligible || checkFailed} onChange={(event) => setQuantities((previous) => ({ ...previous, [item.id]: event.target.value }))} sx={{ width: 160 }} slotProps={{ htmlInput: { min: 1, max: item.quantity - item.fulfilledQuantity, step: 1 } }} /></TableCell></TableRow>)}</TableBody>
        </Table></TableContainer>
        <TextField label="Lý do giao hàng" value={reason} onChange={(event) => setReason(event.target.value)} disabled={busy || uncertain || checkFailed} multiline minRows={2} slotProps={{ htmlInput: { maxLength: 1000 } }} />
        {intent && <Typography variant="caption" sx={{ overflowWrap: 'anywhere' }}>Request ID: {intent.input.requestId}</Typography>}
      </Stack></DialogContent><DialogActions sx={{ p: 2.5 }}><Button disabled={busy} onClick={onClose}>Đóng</Button><Button variant="contained" disabled={busy || checkFailed || !eligible || (uncertain && !intent)} onClick={reviewInput}>{uncertain ? 'Mở lại phiếu gốc' : 'Kiểm tra phiếu giao'}</Button></DialogActions>
    </Dialog>
    <Dialog open={review} fullWidth maxWidth="sm" onClose={busy ? undefined : () => setReview(false)}><DialogTitle>Xác nhận giao hàng · {current.code}</DialogTitle><DialogContent><Stack spacing={2}>
      <Alert severity="warning">Xác nhận sẽ trừ tồn kho và ghi Stock Movement. Không có thao tác tự động thử lại với Request ID mới.</Alert>
      {mutation.isError && <Alert severity="error">{errorMessage(mutation.error)}</Alert>}
      {checkFailed && <Alert severity="warning">Chưa xác minh được kết quả; kiểm tra trạng thái trước khi gửi tiếp.</Alert>}
      {notFound && <Alert severity="info">Thử lại sẽ sử dụng cùng Request ID.</Alert>}
      {uncertain && <Alert severity="warning">Chỉ gửi lại chính xác phiếu gốc với cùng Request ID.</Alert>}
      {intent?.input.items.map((line) => { const item = current.items.find((value) => value.id === line.salesOrderItemId); return <Stack key={line.salesOrderItemId} direction="row" justifyContent="space-between"><Typography>{item?.sku ?? `Order line #${line.salesOrderItemId}`}</Typography><Typography fontWeight={800}>−{line.quantity}</Typography></Stack>; })}
      <Typography variant="body2" color="text.secondary">Lý do: {intent?.input.reason || 'Theo mã đơn bán hàng'}</Typography><Typography variant="caption" sx={{ overflowWrap: 'anywhere' }}>Request ID: {intent?.input.requestId}</Typography>
      {intent && !withinRemaining && !uncertain && <Alert severity="warning">Số lượng còn lại đã thay đổi; hãy cập nhật phiếu trước khi gửi.</Alert>}
    </Stack></DialogContent><DialogActions sx={{ p: 2.5 }}><Button disabled={busy} onClick={() => setReview(false)}>Quay lại</Button><Button variant="contained" disabled={busy || checkFailed || !intent || (!uncertain && (!eligible || !withinRemaining))} onClick={() => intent && mutation.mutate(intent.input)}>{busy ? 'Đang giao…' : uncertain || mutation.isError ? 'Thử lại cùng Request ID' : 'Xác nhận giao hàng'}</Button></DialogActions></Dialog>
  </>;
}
